namespace ETFKit.Objects;

/// <summary>
/// Represents a new port in Erlang parlance.
/// </summary>
public readonly record struct V4PortTerm
{
    /// <summary>
    /// The originating node.
    /// </summary>
    public required string Node { get; init; }

    /// <summary>
    /// The relevant ID.
    /// </summary>
    public required ulong Id { get; init; }

    /// <summary>
    /// An identifier indicating the incarnation of a node.
    /// </summary>
    public required uint Creation { get; init; }
}
