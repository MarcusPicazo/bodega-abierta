namespace BodegaAbierta.Domain.Common;

/// <summary>
/// An entity protected by optimistic concurrency. The persistence layer maps <see cref="RowVersion"/>
/// to a SQL Server <c>rowversion</c> column that the database bumps on every update.
/// </summary>
public interface IVersioned
{
#pragma warning disable CA1819 // rowversion is naturally a byte[]; it is never mutated in place.
    byte[] RowVersion { get; }
#pragma warning restore CA1819
}
