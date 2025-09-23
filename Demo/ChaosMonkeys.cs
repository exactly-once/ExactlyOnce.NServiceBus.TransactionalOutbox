using ExactlyOnce.NServiceBus.AtomicConsumeDispatchOutbox.Sql;
using NServiceBus.Logging;
using NServiceBus.Persistence;
using NServiceBus.Pipeline;

public static class ChaosMonkey
{
    public const int FailureProbability = 30;
}

public class FailBeforeAckBehavior : Behavior<ITransportReceiveContext>
{
    public override async Task Invoke(ITransportReceiveContext context, Func<Task> next)
    {
        await next();

        //if (!context.Message.Headers.ContainsKey("ExactlyOnce.NServiceBus.AtomicConsumeDispatchOutbox.Sql.CleanupCommitted")
        //    && Random.Shared.Next(100) < ChaosMonkey.FailureProbability)
        //{
        //    throw new Exception("Before ACK");
        //}
    }
}

public class FailBeforeDispatchingBehavior : Behavior<IBatchDispatchContext>
{
    public override Task Invoke(IBatchDispatchContext context, Func<Task> next)
    {
        //if (Random.Shared.Next(100) < ChaosMonkey.FailureProbability)
        //{
        //    throw new Exception("Before dispatching");
        //}

        return next();
    }
}

public class FailAfterDispatchingBehavior : Behavior<IBatchDispatchContext>
{
    public override async Task Invoke(IBatchDispatchContext context, Func<Task> next)
    {
        await next();

        if (Random.Shared.Next(100) < ChaosMonkey.FailureProbability)
        {
            throw new Exception("After dispatching");
        }
    }
}


public class FailWhenRenewingLockBehavior : ITransportIntegration
{
    private ITransportIntegration transportIntegrationImplementation;

    public FailWhenRenewingLockBehavior(ITransportIntegration transportIntegrationImplementation)
    {
        this.transportIntegrationImplementation = transportIntegrationImplementation;
    }

    public async Task RenewLock(IInvokeHandlerContext context)
    {
        if (Random.Shared.Next(100) < ChaosMonkey.FailureProbability)
        {
            throw new Exception("Before renew lock");
        }

        await transportIntegrationImplementation.RenewLock(context);

        if (Random.Shared.Next(100) < ChaosMonkey.FailureProbability)
        {
            throw new Exception("After renew lock");
        }
    }
}

public class FailWhenCreatingAttemptBehavior : IPersistenceIntegration
{
    public static SemaphoreSlim CompletedSemaphore = new SemaphoreSlim(1);
    private ILog log = LogManager.GetLogger<FailWhenCreatingAttemptBehavior>();

    private IPersistenceIntegration persistenceIntegrationImplementation;

    public FailWhenCreatingAttemptBehavior(IPersistenceIntegration persistenceIntegrationImplementation)
    {
        this.persistenceIntegrationImplementation = persistenceIntegrationImplementation;
    }

    public Task<AttemptRecord?> Load(string messageId)
    {
        return persistenceIntegrationImplementation.Load(messageId);
    }

    public async Task Create(string messageId)
    {
        if (Random.Shared.Next(100) < ChaosMonkey.FailureProbability)
        {
            throw new Exception("Before create");
        }

        await persistenceIntegrationImplementation.Create(messageId);

        if (Random.Shared.Next(100) < ChaosMonkey.FailureProbability)
        {
            throw new Exception("After create");
        }
    }

    public async Task<int> CommitAttempt(string messageId, string attemptId, ISynchronizedStorageSession synchronizedStorageSession)
    {
        if (Random.Shared.Next(100) < ChaosMonkey.FailureProbability)
        {
            throw new Exception("Before commit");
        }

        var result = await persistenceIntegrationImplementation.CommitAttempt(messageId, attemptId, synchronizedStorageSession);
        log.Info($"Committed: {messageId}");

        if (Random.Shared.Next(100) < ChaosMonkey.FailureProbability)
        {
            throw new Exception("After commit");
        }

        return result;
    }

    public Task CleanCommitted(string messageId)
    {
        log.Info($"Cleaned: {messageId}");
        CompletedSemaphore.Release();
        return persistenceIntegrationImplementation.CleanCommitted(messageId);
    }
}