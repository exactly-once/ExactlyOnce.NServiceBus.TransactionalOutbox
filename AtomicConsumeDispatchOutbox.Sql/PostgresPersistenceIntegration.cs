using System.Data.Common;
using NServiceBus.Persistence;

namespace ExactlyOnce.NServiceBus.AtomicConsumeDispatchOutbox.Sql;

public class PostgresPersistenceIntegration : IPersistenceIntegration
{
    private string outboxTableName;
    private Func<DbConnection> connectionFactory;

    public PostgresPersistenceIntegration(string outboxTableName, Func<DbConnection> connectionFactory)
    {
        this.outboxTableName = outboxTableName;
        this.connectionFactory = connectionFactory;
    }

    public async Task<AttemptRecord?> Load(string messageId)
    {
        await using var connection = connectionFactory();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "select \"AttemptId\" from public.\"Attempts\" where \"MessageId\" = @MessageId for update;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@MessageId";
        parameter.Value = messageId;
        command.Parameters.Add(parameter);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync().ConfigureAwait(false))
        {
            return null;
        }

        if (reader.IsDBNull(0))
        {
            return new AttemptRecord();
        }
        var attemptId = reader.GetString(0);
        return new AttemptRecord
        {
            AttemptId = attemptId
        };
    }

    public async Task Create(string messageId)
    {
        await using var connection = connectionFactory();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "insert into public.\"Attempts\" (\"MessageId\", \"AttemptId\") values (@MessageId, null);";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@MessageId";
        parameter.Value = messageId;
        command.Parameters.Add(parameter);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<int> CommitAttempt(string messageId, string attemptId, ISynchronizedStorageSession synchronizedStorageSession)
    {
        var connection = synchronizedStorageSession.SqlPersistenceSession().Connection;
        var transaction = synchronizedStorageSession.SqlPersistenceSession().Transaction;

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "update public.\"Attempts\" set \"AttemptId\" = @AttemptId where \"MessageId\" = @MessageId and \"AttemptId\" is null;";
        
        var messageIdParameter = command.CreateParameter();
        messageIdParameter.ParameterName = "@MessageId";
        messageIdParameter.Value = messageId;
        command.Parameters.Add(messageIdParameter);

        var attemptIdParameter = command.CreateParameter();
        attemptIdParameter.ParameterName = "@AttemptId";
        attemptIdParameter.Value = messageId;
        command.Parameters.Add(attemptIdParameter);

        var affected = await command.ExecuteNonQueryAsync();
        return affected;
    }

    public async Task CleanCommitted(string messageId)
    {
        await using var connection = connectionFactory();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "delete from public.\"Attempts\" where \"MessageId\" = @MessageId;" 
                              + $"delete from public.\"{outboxTableName}\" where \"MessageId\" = @MessageId;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@MessageId";
        parameter.Value = messageId;
        command.Parameters.Add(parameter);

        await command.ExecuteNonQueryAsync();
    }

    public async Task CleanUncommitted(string messageId)
    {
    }
}