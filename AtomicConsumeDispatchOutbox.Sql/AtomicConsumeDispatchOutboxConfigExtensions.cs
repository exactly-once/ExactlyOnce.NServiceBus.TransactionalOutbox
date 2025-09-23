using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Features;
using NServiceBus.Outbox;

namespace ExactlyOnce.NServiceBus.AtomicConsumeDispatchOutbox.Sql;

public static class AtomicConsumeDispatchOutboxConfigExtensions
{
    public static void EnableZeroStateOutbox(this OutboxSettings outboxSettings, 
        IPersistenceIntegration recordOperations, 
        ITransportIntegration transportIntegration)
    {
        outboxSettings.GetSettings().Set(recordOperations);
        outboxSettings.GetSettings().Set(transportIntegration);
        outboxSettings.GetSettings().EnableFeatureByDefault<AtomicConsumeDispatchOutboxFeature>();
    }
}