using NServiceBus.Persistence;

namespace ExactlyOnce.NServiceBus.AtomicConsumeDispatchOutbox.Sql;

public interface IPersistenceIntegration
{
    /// <summary>
    /// Load existing attempt record
    /// </summary>
    Task<AttemptRecord?> Load(string messageId);
    /// <summary>
    /// Insert attempt record
    /// </summary>
    Task Create(string messageId);
    /// <summary>
    /// Update the attempt record with attempt Id if empty. Otherwise throw
    /// </summary>
    Task<int> CommitAttempt(string messageId, string attemptId, ISynchronizedStorageSession synchronizedStorageSession);
    /// <summary>
    /// Remove attempt record
    /// Remove outbox record
    /// </summary>
    Task CleanCommitted(string messageId);
}