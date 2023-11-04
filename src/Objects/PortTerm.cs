namespace ETFKit.Objects;

/// <summary>
/// Represents a port in Erlang parlance.
/// </summary>
public readonly record struct PortTerm
{
    /// <summary>
    /// The originating node.
    /// </summary>
    public required string Node { get; init; }

    /// <summary>
    /// The relevant ID.
    /// </summary>
    public required uint Id { get; init; }

    /// <summary>
    /// An identifier indicating the incarnation of a node.
    /// </summary>
    public required byte Creation { get; init; }
}
