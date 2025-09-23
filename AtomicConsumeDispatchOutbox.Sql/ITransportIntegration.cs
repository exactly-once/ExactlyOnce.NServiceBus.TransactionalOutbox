using NServiceBus.Pipeline;

namespace ExactlyOnce.NServiceBus.AtomicConsumeDispatchOutbox.Sql;

public interface ITransportIntegration
{
    Task RenewLock(IInvokeHandlerContext context);
}