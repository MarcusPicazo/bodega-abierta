namespace BodegaAbierta.Domain.Common;

/// <summary>
/// A business rule was violated (the request was well formed but not allowed in the current state).
/// <see cref="Code"/> is a stable, machine-readable identifier such as <c>lot.expiry_required</c>.
/// </summary>
public sealed class DomainRuleException : Exception
{
    public DomainRuleException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public DomainRuleException() : this("domain.rule", "A business rule was violated.") { }

    public DomainRuleException(string message) : this("domain.rule", message) { }

    public DomainRuleException(string message, Exception innerException)
        : base(message, innerException)
    {
        Code = "domain.rule";
    }

    public string Code { get; } = "domain.rule";
}
