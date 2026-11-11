using BodegaAbierta.Application.Abstractions;
using BodegaAbierta.Domain.Distributions;
using BodegaAbierta.Domain.Policies;
using BodegaAbierta.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace BodegaAbierta.Application.Rules;

/// <summary>Context of the strategies: picks the evaluator for each rule of a policy set and runs it.</summary>
public sealed class PolicyEngine
{
    private readonly Dictionary<RuleType, IPolicyRuleEvaluator> _evaluators;

    public PolicyEngine(IEnumerable<IPolicyRuleEvaluator> evaluators)
    {
        // ToDictionary throws on duplicates: two strategies for one rule type is a wiring bug.
        _evaluators = evaluators.ToDictionary(e => e.Type);
    }

    public IReadOnlyCollection<RuleType> SupportedRuleTypes => _evaluators.Keys;

    public IReadOnlyList<string> ValidateParameters(RuleType type, string parametersJson)
        => _evaluators.TryGetValue(type, out var evaluator)
            ? evaluator.ValidateParameters(parametersJson)
            : [$"Rule type {type} is not supported."];

    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(
        PolicySet? policy, EvaluationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (policy is null)
        {
            return [];
        }

        var violations = new List<RuleViolation>();
        foreach (var rule in policy.Rules.OrderBy(r => r.Type))
        {
            if (!_evaluators.TryGetValue(rule.Type, out var evaluator))
            {
                throw new InvalidOperationException($"No evaluator registered for rule type {rule.Type}.");
            }

            if (evaluator.AppliesTo(context.Kind))
            {
                violations.AddRange(await evaluator.EvaluateAsync(rule.ParametersJson, context, cancellationToken));
            }
        }

        return violations;
    }
}

/// <summary>Outcome of checking an operation: built-in feasibility checks plus the active policy's rules.</summary>
public sealed record PolicyEvaluation(PolicySet? Policy, IReadOnlyList<RuleViolation> Violations)
{
    public bool Allowed => Violations.All(v => v.Severity != ViolationSeverity.Error);

    public IReadOnlyList<RuleViolation> Warnings => Violations.Where(v => v.Severity == ViolationSeverity.Warning).ToList();

    public void EnsureAllowed()
    {
        if (!Allowed)
        {
            throw new PolicyViolationException(Violations);
        }
    }
}

/// <summary>The operation breaks the active policy or is not feasible (HTTP 422 with the violations).</summary>
public sealed class PolicyViolationException : Exception
{
    public PolicyViolationException(IReadOnlyList<RuleViolation> violations)
        : base("The operation is not allowed: " + string.Join(" ", violations.Where(v => v.Severity == ViolationSeverity.Error).Select(v => v.Message)))
    {
        Violations = violations;
    }

    public PolicyViolationException() : this([]) { }

    public PolicyViolationException(string message) : base(message) => Violations = [];

    public PolicyViolationException(string message, Exception innerException) : base(message, innerException) => Violations = [];

    public IReadOnlyList<RuleViolation> Violations { get; }
}

/// <summary>Loads the current food bank's Active policy set and evaluates against it.</summary>
public sealed class PolicyGate(IBodegaDbContext db, PolicyEngine engine)
{
    public Task<PolicySet?> GetActivePolicyAsync(CancellationToken cancellationToken)
        => db.PolicySets.AsNoTracking()
            .Include(p => p.Rules)
            .FirstOrDefaultAsync(p => p.Status == PolicySetStatus.Active, cancellationToken);

    public async Task<PolicyEvaluation> EvaluateAsync(
        EvaluationContext context, IEnumerable<RuleViolation> feasibility, CancellationToken cancellationToken)
    {
        var policy = await GetActivePolicyAsync(cancellationToken);
        var violations = feasibility.ToList();
        violations.AddRange(await engine.EvaluateAsync(policy, context, cancellationToken));
        return new PolicyEvaluation(policy, violations);
    }
}

/// <summary>Port used by BeneficiaryLimit so the rule can be unit-tested without a database.</summary>
public interface IDistributionHistory
{
    /// <summary>Kilograms in non-cancelled distributions scheduled in (fromExclusive, toInclusive].</summary>
    Task<decimal> GetKilogramsScheduledAsync(Guid beneficiaryId, DateTimeOffset fromExclusive, DateTimeOffset toInclusive, CancellationToken cancellationToken);
}

internal sealed class DistributionHistory(IBodegaDbContext db) : IDistributionHistory
{
    public async Task<decimal> GetKilogramsScheduledAsync(
        Guid beneficiaryId, DateTimeOffset fromExclusive, DateTimeOffset toInclusive, CancellationToken cancellationToken)
        => await db.Distributions
            .Where(d => d.BeneficiaryId == beneficiaryId
                && d.Status != DistributionStatus.Cancelled
                && d.ScheduledFor > fromExclusive
                && d.ScheduledFor <= toInclusive)
            .SelectMany(d => d.Allocations)
            .Where(a => a.Unit == UnitOfMeasure.Kilogram)
            .SumAsync(a => (decimal?)a.Quantity, cancellationToken) ?? 0m;
}
