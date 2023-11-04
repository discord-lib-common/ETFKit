namespace ETFKit.Objects;

/// <summary>
/// Represents a new Erlang process identifier object.
/// </summary>
public readonly record struct NewPidTerm
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
    /// The relevant serial number.
    /// </summary>
    public required uint Serial { get; init; }

    /// <summary>
    /// An identifier indicating the incarnation of a node.
    /// </summary>
    public required uint Creation { get; init; }
}
