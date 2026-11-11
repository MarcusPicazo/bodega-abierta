namespace BodegaAbierta.Domain.Common;

/// <summary>Base type for entities that are edited concurrently and need optimistic concurrency.</summary>
public abstract class VersionedEntity : Entity, IVersioned
{
#pragma warning disable CA1819 // see IVersioned
    public byte[] RowVersion { get; private set; } = [];
#pragma warning restore CA1819
}
