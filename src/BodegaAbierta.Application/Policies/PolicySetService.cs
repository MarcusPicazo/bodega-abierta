using BodegaAbierta.Application.Abstractions;
using BodegaAbierta.Application.Common;
using BodegaAbierta.Domain.Policies;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BodegaAbierta.Application.Policies;

public sealed class PolicySetService(
    IBodegaDbContext db,
    ITenantContext tenant,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IValidator<CreatePolicySetRequest> createValidator,
    IValidator<UpdatePolicySetRequest> updateValidator,
    IValidator<RejectPolicySetRequest> rejectValidator)
{
    public async Task<PolicySetResponse> CreateDraftAsync(CreatePolicySetRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var foodBankId = tenant.RequireFoodBankId();
        var actor = currentUser.RequireUserId();
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        string? description = request.Description;
        IEnumerable<RuleDefinition> rules;
        if (request.CopyFromPolicySetId is { } sourceId)
        {
            var source = await LoadAsync(sourceId, cancellationToken);
            description ??= source.Description;
            rules = source.Rules.Select(r => new RuleDefinition(r.Type, r.ParametersJson)).ToList();
        }
        else
        {
            rules = request.Rules!.ToDefinitions();
        }

        // Concurrent creates could compute the same number; the unique (FoodBankId, Version) index turns that into 409.
        var nextVersion = (await db.PolicySets.MaxAsync(p => (int?)p.Version, cancellationToken) ?? 0) + 1;
        var now = timeProvider.GetUtcNow();

        var draft = new PolicySet(foodBankId, nextVersion, description, actor, now);
        draft.Edit(description, rules, now);
        db.PolicySets.Add(draft);
        await db.SaveChangesAsync(cancellationToken);

        return draft.ToResponse();
    }

    public async Task<PolicySetResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        tenant.RequireFoodBankId();
        return (await LoadAsync(id, cancellationToken, tracking: false)).ToResponse();
    }

    public async Task<PolicySetResponse> GetActiveAsync(CancellationToken cancellationToken)
    {
        tenant.RequireFoodBankId();
        var active = await db.PolicySets.AsNoTracking().Include(p => p.Rules)
            .FirstOrDefaultAsync(p => p.Status == PolicySetStatus.Active, cancellationToken)
            ?? throw new NotFoundException("This food bank has no active policy set.");

        return active.ToResponse();
    }

    public async Task<IReadOnlyList<PolicySetResponse>> ListAsync(CancellationToken cancellationToken)
    {
        tenant.RequireFoodBankId();
        var all = await db.PolicySets.AsNoTracking().Include(p => p.Rules)
            .OrderByDescending(p => p.Version)
            .ToListAsync(cancellationToken);

        return all.Select(p => p.ToResponse()).ToList();
    }

    public async Task<PolicySetResponse> UpdateDraftAsync(
        Guid id, UpdatePolicySetRequest request, byte[] expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        tenant.RequireFoodBankId();
        currentUser.RequireUserId();
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var policy = await LoadAsync(id, cancellationToken);
        db.SetExpectedRowVersion(policy, expectedVersion);
        policy.Edit(request.Description, request.Rules.ToDefinitions(), timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        return policy.ToResponse();
    }

    public async Task<PolicySetResponse> SubmitAsync(Guid id, byte[] expectedVersion, CancellationToken cancellationToken)
    {
        tenant.RequireFoodBankId();
        var actor = currentUser.RequireUserId();

        var policy = await LoadAsync(id, cancellationToken);
        db.SetExpectedRowVersion(policy, expectedVersion);
        policy.Submit(actor, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        return policy.ToResponse();
    }

    /// <summary>
    /// Activates a pending version and retires the currently active one, atomically.
    /// Two SaveChanges in one transaction, in this order, because the filtered unique index
    /// "one Active per food bank" is checked per statement: retire first, then activate.
    /// </summary>
    public async Task<PolicySetResponse> ApproveAsync(Guid id, byte[] expectedVersion, CancellationToken cancellationToken)
    {
        tenant.RequireFoodBankId();
        var approver = currentUser.RequireRole(Roles.Coordinator); // the controller checks too; defense in depth

        var policy = await LoadAsync(id, cancellationToken);
        db.SetExpectedRowVersion(policy, expectedVersion);
        policy.EnsureCanBeApprovedBy(approver); // fail fast, before opening the transaction

        var currentlyActive = await db.PolicySets
            .FirstOrDefaultAsync(p => p.Status == PolicySetStatus.Active && p.Id != id, cancellationToken);
        var now = timeProvider.GetUtcNow();

        await db.ExecuteInTransactionAsync(async ct =>
        {
            if (currentlyActive is not null)
            {
                currentlyActive.RetireInFavorOf(policy, now);
                await db.SaveChangesAsync(ct);
            }

            policy.Approve(approver, now);
            await db.SaveChangesAsync(ct);
        }, cancellationToken);

        return policy.ToResponse();
    }

    public async Task<PolicySetResponse> RejectAsync(
        Guid id, RejectPolicySetRequest request, byte[] expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        tenant.RequireFoodBankId();
        var reviewer = currentUser.RequireRole(Roles.Coordinator);
        await rejectValidator.ValidateAndThrowAsync(request, cancellationToken);

        var policy = await LoadAsync(id, cancellationToken);
        db.SetExpectedRowVersion(policy, expectedVersion);
        policy.Reject(reviewer, request.Reason, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        return policy.ToResponse();
    }

    private async Task<PolicySet> LoadAsync(Guid id, CancellationToken cancellationToken, bool tracking = true)
    {
        var query = db.PolicySets.Include(p => p.Rules).AsQueryable();
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("Policy set", id);
    }
}
