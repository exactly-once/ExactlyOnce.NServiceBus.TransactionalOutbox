using Microsoft.Azure.Amqp.Framing;

public class MyMessageHandler : IHandleMessages<MyMessage>
{
    public async Task Handle(MyMessage message, IMessageHandlerContext context)
    {
        Console.WriteLine($"Processing MyMessage {context.MessageId}");

        var session = context.SynchronizedStorageSession.SqlPersistenceSession()!;

        await using var command = session.Connection.CreateCommand();
        command.Transaction = session.Transaction;
        command.CommandText = "insert into public.\"Data\" (\"Id\", \"Val\") values (@MessageId, 1);";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@MessageId";
        parameter.Value = context.MessageId;
        command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync(context.CancellationToken).ConfigureAwait(false);

        await context.Send(new MyFollowUpMessage());
    }
}