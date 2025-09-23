using NServiceBus.Logging;
using NServiceBus.Pipeline;
using NServiceBus.Routing;
using NServiceBus.Transport;
using TransportOperation = NServiceBus.Transport.TransportOperation;

namespace ExactlyOnce.NServiceBus.AtomicConsumeDispatchOutbox.Sql;

public class AttemptRecordBehavior : Behavior<IInvokeHandlerContext>
{
    private IPersistenceIntegration attempts;
    private ITransportIntegration broker;
    private readonly string localAddress;

    private readonly ILog logger = LogManager.GetLogger<AttemptRecordBehavior>();

    public AttemptRecordBehavior(IPersistenceIntegration attempts, ITransportIntegration broker, string localAddress)
    {
        this.attempts = attempts;
        this.broker = broker;
        this.localAddress = localAddress;
    }

    public override async Task Invoke(IInvokeHandlerContext context, Func<Task> next)
    {
        await next().ConfigureAwait(false);

        //It needs to be executed like this because we need access to SynchronizedSessionContext and we want to execute only once
        var state = context.Extensions.Get<State>();
        if (!state.Executed)
        {
            await CreateAttempt(context, state);
            state.Executed = true;
        }
    }

    private async Task CreateAttempt(IInvokeHandlerContext context, State state)
    {
        await EnsureAttemptExists(context.MessageId, state.ThreadId);
        await RenewMessageLock(context, state.ThreadId);
        await CommitAttempt(context, state.ThreadId);
        AddCleanupMessageToPendingBatch(context);
    }

    private async Task CommitAttempt(IInvokeHandlerContext context, Guid threadId)
    {
        //Fails if another attempt committed it before or attempt record has been removed as uncommitted
        var attemptId = Guid.NewGuid().ToString();
        var affected = await attempts.CommitAttempt(context.MessageId, attemptId, context.SynchronizedStorageSession).ConfigureAwait(false);
        if (affected != 1)
        {
            throw new Exception($"Concurrency violation by thread {threadId}. Other attempt completed first. Abandoning processing.");
        }
    }

    private async Task RenewMessageLock(IInvokeHandlerContext context, Guid threadId)
    {
        try
        {
            await broker.RenewLock(context);
            logger.Info($"Lock renewed for message {context.MessageId} by thread {threadId}");
        }
        catch (Exception e)
        {
            throw new Exception($"Lock lost by thread {threadId}. Message has been handled by another process. Abandoning processing.", e);
        }
    }

    private async Task EnsureAttemptExists(string messageId, Guid threadId)
    {
        //TODO: Clean uncommitted attempts to avoid accumulating garbage
        //This should only happen when this thread has been paused here for a long time and another thread has completed processing
        var record = await attempts.Load(messageId).ConfigureAwait(false);
        if (record == null)
        {
            await attempts.Create(messageId).ConfigureAwait(false);
        }
        else if (record.AttemptId != null) //Committed record. We need to abort. Message has been processed
        {
            throw new Exception($"Another attempt has committed the outbox transaction. Thread {threadId} is abandoning processing.");
        }
    }

    private void AddCleanupMessageToPendingBatch(IInvokeHandlerContext context)
    {
        var pendingOps = context.Extensions.Get<PendingTransportOperations>();
        var headers = new Dictionary<string, string>
        {
            ["ExactlyOnce.NServiceBus.AtomicConsumeDispatchOutbox.Sql.CleanupCommitted"] = context.MessageId
        };
        var cleanupMessage = new OutgoingMessage(Guid.NewGuid().ToString(), headers, Array.Empty<byte>());
        pendingOps.Add(new TransportOperation(cleanupMessage, new UnicastAddressTag(localAddress)));
    }

    public class State
    {
        public bool Executed { get; set; }
        public Guid ThreadId { get; } = Guid.NewGuid();
    }
}