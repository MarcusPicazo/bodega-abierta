using System.Text.Json;
using BodegaAbierta.Application.Common;
using BodegaAbierta.Domain.Policies;

namespace BodegaAbierta.Application.Policies;

public sealed record PolicyRuleRequest(RuleType Type, JsonElement Parameters);

/// <param name="CopyFromPolicySetId">Start the new draft from another version's description and rules.</param>
/// <param name="Rules">Ignored when copying; required (may be empty) otherwise.</param>
public sealed record CreatePolicySetRequest(
    string? Description,
    IReadOnlyList<PolicyRuleRequest>? Rules,
    Guid? CopyFromPolicySetId);

public sealed record UpdatePolicySetRequest(string? Description, IReadOnlyList<PolicyRuleRequest> Rules);

public sealed record RejectPolicySetRequest(string Reason);

public sealed record PolicyRuleResponse(Guid Id, RuleType Type, JsonElement Parameters);

public sealed record PolicySetResponse(
    Guid Id,
    int Version,
    string? Description,
    PolicySetStatus Status,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastModifiedAt,
    string? SubmittedBy,
    DateTimeOffset? SubmittedAt,
    string? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    string? RejectionReason,
    DateTimeOffset? RetiredAt,
    Guid? ReplacedByPolicySetId,
    IReadOnlyList<PolicyRuleResponse> Rules,
    string RowVersion);

internal static class PolicyMappings
{
    public static PolicySetResponse ToResponse(this PolicySet p)
        => new(
            p.Id,
            p.Version,
            p.Description,
            p.Status,
            p.CreatedBy,
            p.CreatedAt,
            p.LastModifiedAt,
            p.SubmittedBy,
            p.SubmittedAt,
            p.ApprovedBy,
            p.ApprovedAt,
            p.RejectionReason,
            p.RetiredAt,
            p.ReplacedByPolicySetId,
            p.Rules.OrderBy(r => r.Type).Select(ToResponse).ToList(),
            VersionToken.Encode(p.RowVersion));

    private static PolicyRuleResponse ToResponse(PolicyRule rule)
    {
        using var document = JsonDocument.Parse(rule.ParametersJson);
        return new PolicyRuleResponse(rule.Id, rule.Type, document.RootElement.Clone());
    }

    public static IEnumerable<RuleDefinition> ToDefinitions(this IEnumerable<PolicyRuleRequest> rules)
        => rules.Select(r => new RuleDefinition(r.Type, r.Parameters.GetRawText()));
}
