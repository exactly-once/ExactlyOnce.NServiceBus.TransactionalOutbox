using NServiceBus.Pipeline;

namespace ExactlyOnce.NServiceBus.AtomicConsumeDispatchOutbox.Sql;

class CleanupCommittedBehavior : Behavior<ITransportReceiveContext>
{
    private IPersistenceIntegration persistenceIntegration;

    public CleanupCommittedBehavior(IPersistenceIntegration persistenceIntegration)
    {
        this.persistenceIntegration = persistenceIntegration;
    }

    public override Task Invoke(ITransportReceiveContext context, Func<Task> next)
    {
        if (!context.Message.Headers.TryGetValue("ExactlyOnce.NServiceBus.AtomicConsumeDispatchOutbox.Sql.CleanupCommitted", out var id))
        {
            return next();
        }

        return persistenceIntegration.CleanCommitted(id);
    }
}