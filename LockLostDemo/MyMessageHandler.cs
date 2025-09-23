using System.Collections.Concurrent;
using Microsoft.Azure.Amqp.Framing;
using static ExactlyOnce.NServiceBus.AtomicConsumeDispatchOutbox.Sql.AttemptRecordBehavior;

public class MyMessageHandler : IHandleMessages<MyMessage>
{
    static ConcurrentDictionary<string, bool> processedDictionary = new();

    public async Task Handle(MyMessage message, IMessageHandlerContext context)
    {
        var state = context.Extensions.Get<State>();

        Console.WriteLine($"Attempting at processing MyMessage {context.MessageId} by thread {state.ThreadId}...");

        var session = context.SynchronizedStorageSession.SqlPersistenceSession()!;

        await using var command = session.Connection.CreateCommand();
        command.Transaction = session.Transaction;
        command.CommandText = "insert into public.\"Data\" (\"Id\", \"Val\") values (@MessageId, 1);";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@MessageId";
        parameter.Value = context.MessageId;
        command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync(context.CancellationToken).ConfigureAwait(false);

        if (processedDictionary.ContainsKey(context.MessageId))
        {
            Console.WriteLine($"Second attempt at processing MyMessage {context.MessageId} by thread {state.ThreadId}. No delay");
            processedDictionary.Remove(context.MessageId, out _);
        }
        else
        {
            Console.WriteLine($"First attempt at processing MyMessage {context.MessageId} by thread {state.ThreadId}. Simulating delay to trigger lock-lost behavior.");
            processedDictionary[context.MessageId] = true;
            await Task.Delay(TimeSpan.FromMinutes(1), context.CancellationToken).ConfigureAwait(false);
            Console.WriteLine($"First attempt at processing MyMessage {context.MessageId} by thread {state.ThreadId}. Done waiting.");
        }

        //await context.Send(new MyFollowUpMessage());
    }
}