namespace ETFKit;

public enum EtfTokenType : byte
{

    /// <summary>
    /// A map token is started.
    /// </summary>
    StartMap,

    /// <summary>
    /// A list token is started.
    /// </summary>
    StartList,

    /// <summary>
    /// A tuple of any size is started.
    /// </summary>
    StartTuple,

    /// <summary>
    /// Any term containing data.
    /// </summary>
    Term,

    /// <summary>
    /// A map token is ended.
    /// </summary>
    EndMap,

    /// <summary>
    /// A list token is ended.
    /// </summary>
    EndList,

    /// <summary>
    /// A tuple of any size is ended.
    /// </summary>
    EndTuple
}
