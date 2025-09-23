using Azure.Messaging.ServiceBus;
using NServiceBus.Pipeline;

namespace ExactlyOnce.NServiceBus.AtomicConsumeDispatchOutbox.Sql;

public class ServiceBusTransportIntegration : ITransportIntegration
{
    private string serviceNamespace;
    private string queuePath;
    private readonly string connectionString;
    private HttpClient client = new();

    public ServiceBusTransportIntegration(string serviceNamespace, string queuePath, string connectionString)
    {
        this.serviceNamespace = serviceNamespace;
        this.queuePath = queuePath.ToLowerInvariant();
        this.connectionString = connectionString;
    }

    public async Task RenewLock(IInvokeHandlerContext context)
    {
        var receivedMessage = context.Extensions.Get<ServiceBusReceivedMessage>();

        // Build the SAS audience as the *entity* (queue or subscription) URL
        var urlBase = $"https://{serviceNamespace}.servicebus.windows.net/{queuePath}";
        var requestUri = $"{urlBase}/messages/{receivedMessage.MessageId}/{receivedMessage.LockToken}";

        var sasToken = ServiceBusSas.GenerateTokenFromConnectionString(connectionString, urlBase, TimeSpan.FromMinutes(60));

        using var req = new HttpRequestMessage(HttpMethod.Post, requestUri);
        req.Headers.TryAddWithoutValidation("Authorization", sasToken);
        req.Content = new ByteArrayContent(Array.Empty<byte>());

        var response = await client.SendAsync(req, context.CancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Lock lost. Message has probably been processed.");
        }
    }
}