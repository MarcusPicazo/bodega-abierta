using BodegaAbierta.Domain.Common;

namespace BodegaAbierta.Domain.Policies;

public enum PolicySetStatus
{
    Draft = 0,
    PendingApproval = 1,
    Active = 2,
    Retired = 3,
}

public enum RuleType
{
    FefoAllocation = 0,
    ExpiryAlert = 1,
    PickupWindow = 2,
    BeneficiaryLimit = 3,
    DonorDropOffHours = 4,
}

/// <summary>Input for a rule: its type plus its parameters as a JSON object (validated per type in Phase 4).</summary>
public sealed record RuleDefinition(RuleType Type, string ParametersJson);

/// <summary>
/// One numbered version of a food bank's operating rules. Lifecycle (a small state machine):
/// Draft -&gt; PendingApproval -&gt; Active -&gt; Retired, with PendingApproval -&gt; Draft on rejection.
/// At most one version per food bank is Active (enforced here, in the service and by a filtered unique index).
/// </summary>
public sealed class PolicySet : VersionedEntity, ITenantOwned
{
    public const int DescriptionMaxLength = 500;
    public const int ActorMaxLength = 200;
    public const int ReasonMaxLength = 500;

    /// <summary>The whole lifecycle in one table: from each state, which states can follow.</summary>
    private static readonly Dictionary<PolicySetStatus, PolicySetStatus[]> AllowedTransitions = new()
    {
        [PolicySetStatus.Draft] = [PolicySetStatus.PendingApproval],
        [PolicySetStatus.PendingApproval] = [PolicySetStatus.Active, PolicySetStatus.Draft],
        [PolicySetStatus.Active] = [PolicySetStatus.Retired],
        [PolicySetStatus.Retired] = [],
    };

    private readonly List<PolicyRule> _rules = [];

    private PolicySet() { } // EF Core

    public PolicySet(Guid foodBankId, int version, string? description, string createdBy, DateTimeOffset createdAt)
    {
        if (foodBankId == Guid.Empty)
        {
            throw new ArgumentException("A policy set must belong to a food bank.", nameof(foodBankId));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        FoodBankId = foodBankId;
        Version = version;
        Description = Normalize(description);
        Status = PolicySetStatus.Draft;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        LastModifiedAt = createdAt;
    }

    public Guid FoodBankId { get; private set; }

    /// <summary>1, 2, 3... per food bank.</summary>
    public int Version { get; private set; }

    public string? Description { get; private set; }

    public PolicySetStatus Status { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Bumped on every change, including rule-only edits, so the parent row (and its rowversion)
    /// always changes and concurrent edits of the rules are detected.
    /// </summary>
    public DateTimeOffset LastModifiedAt { get; private set; }

    public string? SubmittedBy { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public string? ApprovedBy { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    public string? RejectionReason { get; private set; }

    public DateTimeOffset? RetiredAt { get; private set; }

    public Guid? ReplacedByPolicySetId { get; private set; }

    public IReadOnlyCollection<PolicyRule> Rules => _rules.AsReadOnly();

    public static bool CanTransition(PolicySetStatus from, PolicySetStatus to) => AllowedTransitions[from].Contains(to);

    /// <summary>Replaces description and rules. Only drafts can be edited.</summary>
    public void Edit(string? description, IEnumerable<RuleDefinition> rules, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (Status != PolicySetStatus.Draft)
        {
            throw new DomainRuleException("policy.not_draft", $"Only drafts can be edited; version {Version} is {Status}.");
        }

        var definitions = rules.ToList();
        var duplicated = definitions.GroupBy(r => r.Type).FirstOrDefault(g => g.Count() > 1);
        if (duplicated is not null)
        {
            throw new DomainRuleException("policy.duplicate_rule", $"Rule type {duplicated.Key} appears more than once.");
        }

        Description = Normalize(description);
        _rules.Clear(); // EF deletes the orphaned rows (required relationship)
        _rules.AddRange(definitions.Select(d => new PolicyRule(FoodBankId, Id, d.Type, d.ParametersJson)));
        LastModifiedAt = at;
    }

    public void Submit(string submittedBy, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(submittedBy);
        if (_rules.Count == 0)
        {
            throw new DomainRuleException("policy.no_rules", "A policy set needs at least one rule before it can be submitted.");
        }

        TransitionTo(PolicySetStatus.PendingApproval, at);
        SubmittedBy = submittedBy;
        SubmittedAt = at;
        RejectionReason = null;
    }

    /// <summary>Checks the approval rules without changing state (lets the service validate before opening a transaction).</summary>
    public void EnsureCanBeApprovedBy(string approver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approver);
        EnsureCanTransitionTo(PolicySetStatus.Active);

        // Four-eyes principle: whoever submitted a version cannot approve it.
        if (string.Equals(approver, SubmittedBy, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainRuleException("policy.self_approval", "A policy set must be approved by someone other than its submitter.");
        }
    }

    public void Approve(string approver, DateTimeOffset at)
    {
        EnsureCanBeApprovedBy(approver);
        TransitionTo(PolicySetStatus.Active, at);
        ApprovedBy = approver;
        ApprovedAt = at;
    }

    /// <summary>Sends a pending version back to Draft so it can be corrected.</summary>
    public void Reject(string reviewer, string reason, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewer);
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleException("policy.rejection_reason_required", "A rejection needs a reason.");
        }

        TransitionTo(PolicySetStatus.Draft, at);
        RejectionReason = reason.Trim();
        SubmittedBy = null;
        SubmittedAt = null;
    }

    /// <summary>Retires the active version because <paramref name="replacement"/> is being activated.</summary>
    public void RetireInFavorOf(PolicySet replacement, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        if (replacement.FoodBankId != FoodBankId || replacement.Id == Id)
        {
            throw new ArgumentException("The replacement must be another version of the same food bank.", nameof(replacement));
        }

        TransitionTo(PolicySetStatus.Retired, at);
        RetiredAt = at;
        ReplacedByPolicySetId = replacement.Id;
    }

    // TODO(Marcus): add `public void Withdraw(string actor, DateTimeOffset at)`: the person who submitted a
    // version can pull it back from PendingApproval to Draft (e.g. they spotted a mistake before review).
    // Rules: must be PendingApproval (use TransitionTo so the table above is respected); only SubmittedBy
    // may withdraw (case-insensitive), otherwise DomainRuleException "policy.not_submitter"; clear
    // SubmittedBy/SubmittedAt. Fill in the skipped stubs in tests/BodegaAbierta.UnitTests/Policies/PolicySetTests.cs.

    private void EnsureCanTransitionTo(PolicySetStatus next)
    {
        if (!CanTransition(Status, next))
        {
            throw new DomainRuleException(
                "policy.invalid_transition", $"Version {Version} cannot go from {Status} to {next}.");
        }
    }

    private void TransitionTo(PolicySetStatus next, DateTimeOffset at)
    {
        EnsureCanTransitionTo(next);
        Status = next;
        LastModifiedAt = at;
    }

    private static string? Normalize(string? description)
        => string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
