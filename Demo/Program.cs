using Azure.Messaging.ServiceBus;
using ExactlyOnce.NServiceBus.AtomicConsumeDispatchOutbox.Sql;
using Npgsql;
using NpgsqlTypes;

OutboxPatcher.EnableOutboxForSendsAtomicWithReceive();

var receiverConfig = new EndpointConfiguration("Receiver2");
receiverConfig.UseSerialization<SystemJsonSerializer>();

var pgConnectionString = "User ID=user;Password=admin;Host=localhost;Port=54320;Database=nservicebus;Pooling=true;Connection Lifetime=0;Include Error Detail=true";
var serviceBusConnectionString = Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString");

var transport = new AzureServiceBusTransport(serviceBusConnectionString)
{
    TransportTransactionMode = TransportTransactionMode.SendsAtomicWithReceive
};
var receiverRouting = receiverConfig.UseTransport(transport);
receiverRouting.RouteToEndpoint(typeof(MyFollowUpMessage), "Sender");

receiverConfig.Recoverability().Immediate(x => x.NumberOfRetries(100));

var persistence = receiverConfig.UsePersistence<SqlPersistence>();
var dialect = persistence.SqlDialect<SqlDialect.PostgreSql>();
dialect.JsonBParameterModifier(
    modifier: parameter =>
    {
        var npgsqlParameter = (NpgsqlParameter)parameter;
        npgsqlParameter.NpgsqlDbType = NpgsqlDbType.Jsonb;
    });
persistence.ConnectionBuilder(() => new NpgsqlConnection(pgConnectionString));

receiverConfig.SendFailedMessagesTo("error");
var outboxSettings = receiverConfig.EnableOutbox();
outboxSettings.EnableZeroStateOutbox(
    new FailWhenCreatingAttemptBehavior(
        new PostgresPersistenceIntegration("Receiver2_OutboxData", () => new NpgsqlConnection(pgConnectionString))),
    new FailWhenRenewingLockBehavior(
        new ServiceBusTransportIntegration("szymonp-2", "Receiver2", serviceBusConnectionString)));

receiverConfig.EnableInstallers();

receiverConfig.Pipeline.Register(new FailBeforeAckBehavior(), "Fail before ACK");
receiverConfig.Pipeline.Register(new FailBeforeDispatchingBehavior(), "Fail before dispatching");
receiverConfig.Pipeline.Register(new FailAfterDispatchingBehavior(), "Fail after dispatching");

receiverConfig.LimitMessageProcessingConcurrencyTo(1);

var receiver = await Endpoint.Start(receiverConfig);

var senderConfig = new EndpointConfiguration("Sender");
var senderRouting = senderConfig.UseTransport(new AzureServiceBusTransport(serviceBusConnectionString)
{
    TransportTransactionMode = TransportTransactionMode.SendsAtomicWithReceive
});
senderRouting.RouteToEndpoint(typeof(MyMessage), "Receiver2");
senderConfig.EnableInstallers();
senderConfig.UseSerialization<SystemJsonSerializer>();
senderConfig.SendFailedMessagesTo("error");

var sender = await Endpoint.Start(senderConfig);
//await sender.Send(new MyMessage());

Console.WriteLine("Press <enter> to exit");
Console.ReadLine();

