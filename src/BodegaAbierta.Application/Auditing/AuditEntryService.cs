using BodegaAbierta.Application.Abstractions;
using BodegaAbierta.Application.Common;
using BodegaAbierta.Domain.Auditing;
using Microsoft.EntityFrameworkCore;

namespace BodegaAbierta.Application.Auditing;

public sealed record ListAuditEntriesQuery(
    string? EntityType = null,
    string? EntityId = null,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize);

public sealed record AuditEntryResponse(
    Guid Id,
    string EntityType,
    string EntityId,
    AuditAction Action,
    string? BeforeJson,
    string? AfterJson,
    string Actor,
    DateTimeOffset Timestamp);

/// <summary>Read side of the audit log. Entries are written only by the persistence layer.</summary>
public sealed class AuditEntryService(IBodegaDbContext db, ITenantContext tenant, ICurrentUser currentUser)
{
    public Task<PagedResult<AuditEntryResponse>> ListAsync(ListAuditEntriesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        tenant.RequireFoodBankId();
        currentUser.RequireRole(Roles.Coordinator);

        IQueryable<AuditEntry> entries = db.AuditEntries.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            entries = entries.Where(e => e.EntityType == query.EntityType);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityId))
        {
            entries = entries.Where(e => e.EntityId == query.EntityId);
        }

        return entries
            .OrderByDescending(e => e.Timestamp).ThenByDescending(e => e.Id)
            .ToPagedResultAsync(
                query.Page,
                query.PageSize,
                e => new AuditEntryResponse(e.Id, e.EntityType, e.EntityId, e.Action, e.BeforeJson, e.AfterJson, e.Actor, e.Timestamp),
                cancellationToken);
    }
}
