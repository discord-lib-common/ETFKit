using System;
using System.Buffers.Binary;

using CommunityToolkit.HighPerformance;

namespace ETFKit.Serialization;

// here we deal with writing the structural types, with a handful of convenience utilities for some
// special cases, like arrays of primitives
partial struct EtfWriter
{
    /// <summary>
    /// Writes the start of a list, and enregisters the list for automatic null-termination.
    /// </summary>
    /// <returns>A value indicating whether this operation succeeded.</returns>
    public bool TryWriteListStart
    (
        uint length
    )
    {
        Span<byte> buffer = stackalloc byte[5];
        buffer[0] = (byte)TermType.List;

        BinaryPrimitives.WriteUInt32BigEndian(buffer[1..], length);

        this.writer.Write<byte>(buffer);

        return this.complexObjects.TryPush(TermType.List) && this.remainingLengths.TryPush(length);
    }

    /// <summary>
    /// Writes the start of a map.
    /// </summary>
    /// <returns>A value indicating whether this operation succeeded.</returns>
    public bool TryWriteMapStart
    (
        uint length
    )
    {
        Span<byte> buffer = stackalloc byte[5];
        buffer[0] = (byte)TermType.Map;

        BinaryPrimitives.WriteUInt32BigEndian(buffer[1..], length);

        this.writer.Write<byte>(buffer);

        return this.complexObjects.TryPush(TermType.Map) && this.remainingLengths.TryPush(length * 2);
    }

    /// <summary>
    /// Writes the start of a small tuple.
    /// </summary>
    /// <returns>A value indicating whether this operation succeeded.</returns>
    public bool TryWriteSmallTupleStart
    (
        byte length
    )
    {
        Span<byte> buffer = [(byte)TermType.SmallTuple, length];
        this.writer.Write<byte>(buffer);

        return this.complexObjects.TryPush(TermType.SmallTuple) && this.remainingLengths.TryPush(length);
    }

    /// <summary>
    /// Writes the start of a large tuple.
    /// </summary>
    /// <returns>A value indicating whether this operation succeeded.</returns>
    public bool TryWriteLargeTupleStart
    (
        uint length
    )
    {
        Span<byte> buffer = stackalloc byte[5];
        buffer[0] = (byte)TermType.LargeTuple;

        BinaryPrimitives.WriteUInt32BigEndian(buffer[1..], length);

        this.writer.Write<byte>(buffer);

        return this.complexObjects.TryPush(TermType.LargeTuple) && this.remainingLengths.TryPush(length);
    }

    /// <summary>
    /// Writes the start of a list and registers it for automatic null-termination.
    /// </summary>
    public void WriteListStart
    (
        uint length
    )
    {
        if (!TryWriteListStart(length))
        {
            ThrowHelper.ThrowInvalidStructureStart(TermType.List);
        }
    }

    /// <summary>
    /// Writes the start of a map.
    /// </summary>
    public void WriteMapStart
    (
        uint length
    )
    {
        if (!TryWriteMapStart(length))
        {
            ThrowHelper.ThrowInvalidStructureStart(TermType.Map);
        }
    }

    /// <summary>
    /// Writes the start of a small tuple.
    /// </summary>
    public void WriteSmallTupleStart
    (
        byte length
    )
    {
        if (!TryWriteSmallTupleStart(length))
        {
            ThrowHelper.ThrowInvalidStructureStart(TermType.SmallTuple);
        }
    }

    /// <summary>
    /// Writes the start of a large tuple.
    /// </summary>
    public void WriteLargeTupleStart
    (
        uint length
    )
    {
        if (!TryWriteLargeTupleStart(length))
        {
            ThrowHelper.ThrowInvalidStructureStart(TermType.LargeTuple);
        }
    }
}
