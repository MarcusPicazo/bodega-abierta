using BodegaAbierta.Domain.Common;

namespace BodegaAbierta.Domain.Auditing;

public enum AuditAction
{
    Created = 0,
    Updated = 1,
    Deleted = 2,
}

/// <summary>
/// Immutable record of one change to one entity, written automatically by the persistence layer in the
/// same transaction as the change itself.
/// </summary>
public sealed class AuditEntry : Entity, ITenantOwned
{
    public const int EntityTypeMaxLength = 100;
    public const int EntityIdMaxLength = 100;
    public const int ActorMaxLength = 200;

    private AuditEntry() { } // EF Core

    public AuditEntry(
        Guid foodBankId,
        string entityType,
        string entityId,
        AuditAction action,
        string? beforeJson,
        string? afterJson,
        string actor,
        DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);

        FoodBankId = foodBankId;
        EntityType = entityType;
        EntityId = entityId;
        Action = action;
        BeforeJson = beforeJson;
        AfterJson = afterJson;
        Actor = actor;
        Timestamp = timestamp;
    }

    public Guid FoodBankId { get; private set; }

    public string EntityType { get; private set; } = string.Empty;

    public string EntityId { get; private set; } = string.Empty;

    public AuditAction Action { get; private set; }

    /// <summary>Values before the change (changed properties only for updates; null for creates).</summary>
    public string? BeforeJson { get; private set; }

    /// <summary>Values after the change (changed properties only for updates; null for deletes).</summary>
    public string? AfterJson { get; private set; }

    public string Actor { get; private set; } = string.Empty;

    public DateTimeOffset Timestamp { get; private set; }
}
