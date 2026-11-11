namespace BodegaAbierta.Domain.Common;

/// <summary>Base type for every entity. Ids are generated in the domain (UUID v7), not by the database.</summary>
public abstract class Entity
{
    public Guid Id { get; protected init; } = Guid.CreateVersion7();
}
