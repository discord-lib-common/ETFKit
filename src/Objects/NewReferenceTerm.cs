using System.Collections.Generic;

namespace ETFKit.Objects;

/// <summary>
/// Represents a new Erlang reference term.
/// </summary>
public readonly record struct NewReferenceTerm
{
    /// <summary>
    /// The originating node.
    /// </summary>
    public required string Node { get; init; }

    /// <summary>
    /// An identifier indicating the incarnation of a node.
    /// </summary>
    public required byte Creation { get; init; }

    /// <summary>
    /// The relevant ID, to be regarded as uninterpreted data.
    /// </summary>
    public required IReadOnlyList<uint> Id { get; init; }
}
