using NServiceBus.Pipeline;
using NServiceBus.Transport;

namespace ExactlyOnce.NServiceBus.AtomicConsumeDispatchOutbox.Sql;

class ForceBatchDispatchToBeNonIsolatedBehavior : IBehavior<IBatchDispatchContext, IBatchDispatchContext>
{
    public Task Invoke(IBatchDispatchContext context, Func<IBatchDispatchContext, Task> next)
    {
        foreach (var operation in context.Operations)
        {
            operation.RequiredDispatchConsistency = DispatchConsistency.Default;
        }
        return next(context);
    }
}