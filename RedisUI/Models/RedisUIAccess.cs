namespace RedisUI;

public enum RedisUIOperation { View, Edit, Delete, Audit }

public sealed record RedisUIAccess(string Connection, int Database, RedisUIOperation Operation);

/// <summary>Metadata only: values, credentials and exception messages are never recorded.</summary>
public sealed record RedisUIAuditEntry(DateTimeOffset Timestamp, string User, string Connection,
    int Database, string Operation, string Key, bool Succeeded);
