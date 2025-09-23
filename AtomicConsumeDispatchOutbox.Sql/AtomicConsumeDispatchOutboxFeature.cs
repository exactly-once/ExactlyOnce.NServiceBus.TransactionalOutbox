using Azure.Messaging.ServiceBus;
using NServiceBus.ConsistencyGuarantees;
using NServiceBus.Features;
using NServiceBus.Transport;

namespace ExactlyOnce.NServiceBus.AtomicConsumeDispatchOutbox.Sql;

class AtomicConsumeDispatchOutboxFeature : Feature
{
    protected override void Setup(FeatureConfigurationContext context)
    {
        var recordOperations = context.Settings.Get<IPersistenceIntegration>();

        var brokerOperations = context.Settings.Get<ITransportIntegration>();
        var localAddress = context.LocalQueueAddress().BaseAddress; //TODO: Add other properties etc.

        context.Pipeline.Register(sp => new AttemptRecordStateBehavior(), "Creates the state object");
        context.Pipeline.Register(sp => new AttemptRecordBehavior(recordOperations, brokerOperations, localAddress), "Creates the state object");
        context.Pipeline.Register(sp => new CleanupCommittedBehavior(recordOperations), "Cleanup committed attempts.");
        context.Pipeline.Register(new ForceBatchDispatchToBeNonIsolatedBehavior(), "Forces atomic-send-with-receive dispatch of outbox operations, even if deserialized");

        if (context.Settings.GetRequiredTransactionModeForReceives() != TransportTransactionMode.SendsAtomicWithReceive)
        {
            throw new Exception(
                $"Zero data Outbox requires transport to be running in `{nameof(TransportTransactionMode.SendsAtomicWithReceive)}` mode. Use the `{nameof(TransportDefinition.TransportTransactionMode)}` property on the transport definition to specify the transaction mode.");
        }
    }
}