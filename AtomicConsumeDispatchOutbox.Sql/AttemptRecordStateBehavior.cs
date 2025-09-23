using NServiceBus.Pipeline;

namespace ExactlyOnce.NServiceBus.AtomicConsumeDispatchOutbox.Sql;

class AttemptRecordStateBehavior : Behavior<IIncomingLogicalMessageContext>
{
    public override async Task Invoke(IIncomingLogicalMessageContext context, Func<Task> next)
    {
        var state = new AttemptRecordBehavior.State();
        context.Extensions.Set(state);
        await next().ConfigureAwait(false);
    }
}