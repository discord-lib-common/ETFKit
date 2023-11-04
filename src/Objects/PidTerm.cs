namespace ETFKit.Objects;

/// <summary>
/// Represents an Erlang process identifier object.
/// </summary>
public readonly record struct PidTerm
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
    public required byte Creation { get; init; }
}
