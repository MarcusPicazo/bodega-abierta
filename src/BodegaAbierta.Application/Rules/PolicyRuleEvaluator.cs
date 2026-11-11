using System.Text.Json;
using System.Text.Json.Serialization;
using BodegaAbierta.Domain.Policies;

namespace BodegaAbierta.Application.Rules;

/// <summary>
/// Strategy for one <see cref="RuleType"/>. The engine picks the strategy by type and never needs to
/// know what a rule does: adding a rule type means adding one class (open/closed principle).
/// </summary>
public interface IPolicyRuleEvaluator
{
    RuleType Type { get; }

    bool AppliesTo(EvaluationKind kind);

    /// <summary>Errors in the rule's JSON parameters (empty when valid). Used when a policy set is edited.</summary>
    IReadOnlyList<string> ValidateParameters(string parametersJson);

    Task<IReadOnlyList<RuleViolation>> EvaluateAsync(string parametersJson, EvaluationContext context, CancellationToken cancellationToken);
}

public static class RuleParameters
{
    /// <summary>
    /// camelCase, enums as strings ("Monday"), <c>required</c> members enforced, and unknown members
    /// rejected so a typo such as "daysBeforeExpiri" is an error instead of being silently ignored.
    /// </summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static T Parse<T>(string parametersJson)
        where T : class
        => JsonSerializer.Deserialize<T>(parametersJson, Json) ?? throw new JsonException("Parameters cannot be null.");
}

/// <summary>
/// Template for strategies with typed parameters: JSON -&gt; <typeparamref name="TParameters"/> once, here,
/// so each rule only deals with a strongly typed record.
/// </summary>
public abstract class PolicyRuleEvaluator<TParameters> : IPolicyRuleEvaluator
    where TParameters : class
{
    public abstract RuleType Type { get; }

    public abstract bool AppliesTo(EvaluationKind kind);

    public IReadOnlyList<string> ValidateParameters(string parametersJson)
    {
        TParameters parameters;
        try
        {
            parameters = RuleParameters.Parse<TParameters>(parametersJson);
        }
        catch (JsonException ex)
        {
            return [ex.Message];
        }

        return Validate(parameters).ToList();
    }

    public Task<IReadOnlyList<RuleViolation>> EvaluateAsync(
        string parametersJson, EvaluationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return EvaluateAsync(RuleParameters.Parse<TParameters>(parametersJson), context, cancellationToken);
    }

    protected virtual IEnumerable<string> Validate(TParameters parameters) => [];

    protected abstract Task<IReadOnlyList<RuleViolation>> EvaluateAsync(
        TParameters parameters, EvaluationContext context, CancellationToken cancellationToken);

    protected RuleViolation Error(string code, string message, int? lineIndex = null)
        => new(Type, code, ViolationSeverity.Error, message, lineIndex);

    protected RuleViolation Warning(string code, string message, int? lineIndex = null)
        => new(Type, code, ViolationSeverity.Warning, message, lineIndex);

    protected static Task<IReadOnlyList<RuleViolation>> Result(IEnumerable<RuleViolation> violations)
        => Task.FromResult<IReadOnlyList<RuleViolation>>(violations.ToList());
}
