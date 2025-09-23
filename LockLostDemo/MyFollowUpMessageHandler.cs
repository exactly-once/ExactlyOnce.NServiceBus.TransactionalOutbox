public class MyFollowUpMessageHandler : IHandleMessages<MyFollowUpMessage>
{
    public Task Handle(MyFollowUpMessage message, IMessageHandlerContext context)
    {
        return context.Send(new MyMessage());
    }
}