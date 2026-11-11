using BodegaAbierta.Domain.Common;

namespace BodegaAbierta.Domain.Policies;

/// <summary>One rule inside a <see cref="PolicySet"/>. Created only through <see cref="PolicySet.Edit"/>.</summary>
public sealed class PolicyRule : Entity, ITenantOwned
{
    private PolicyRule() { } // EF Core

    internal PolicyRule(Guid foodBankId, Guid policySetId, RuleType type, string parametersJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parametersJson);
        FoodBankId = foodBankId;
        PolicySetId = policySetId;
        Type = type;
        ParametersJson = parametersJson;
    }

    public Guid FoodBankId { get; private set; }

    public Guid PolicySetId { get; private set; }

    public RuleType Type { get; private set; }

    /// <summary>Rule parameters as a JSON object, e.g. <c>{"daysBeforeExpiry":3}</c> for ExpiryAlert.</summary>
    public string ParametersJson { get; private set; } = "{}";
}
