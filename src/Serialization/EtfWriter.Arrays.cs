using System;
using System.Buffers.Binary;
using System.Runtime.Intrinsics;

using CommunityToolkit.HighPerformance;

namespace ETFKit.Serialization;

// contains the public API for fast array writing, if available.
partial struct EtfWriter
{
    public bool TryWriteByteArray
    (
        ReadOnlySpan<byte> bytes
    )
    {
        Span<byte> buffer = stackalloc byte[5];

        buffer[0] = (byte)TermType.List;
        BinaryPrimitives.WriteUInt32BigEndian(buffer[1..], (uint)bytes.Length);

        this.writer.Write<byte>(buffer);

        if (Vector256.IsHardwareAccelerated && bytes.Length >= 32)
        {
            this.WriteByteTermArrayV256(bytes);
        }
        else if (Vector128.IsHardwareAccelerated && bytes.Length >= 16)
        {
            this.WriteByteTermArrayV128(bytes);
        }
        else
        {
            this.WriteByteTermArrayScalarAndNullTerminate(bytes);
        }

        this.DecrementAndWriteNullTerminator();

        return true;
    }

    public bool TryWriteInt32Array
    (
        ReadOnlySpan<int> integers
    )
    {
        Span<byte> buffer = stackalloc byte[5];

        buffer[0] = (byte)TermType.List;
        BinaryPrimitives.WriteUInt32BigEndian(buffer[1..], (uint)integers.Length);

        this.writer.Write<byte>(buffer);

        if (Vector256.IsHardwareAccelerated && integers.Length >= 16)
        {
            this.WriteInt32TermArrayV256(integers);
        }
        else if (Vector128.IsHardwareAccelerated && integers.Length >= 4)
        {
            this.WriteInt32TermArrayV128(integers);
        }
        else
        {
            this.WriteInt32TermArrayScalarAndNullTerminate(integers);
        }

        this.DecrementAndWriteNullTerminator();

        return true;
    }

    public bool TryWriteInt16Array
    (
        ReadOnlySpan<short> integers
    )
    {
        Span<byte> buffer = stackalloc byte[5];

        buffer[0] = (byte)TermType.List;
        BinaryPrimitives.WriteUInt32BigEndian(buffer[1..], (uint)integers.Length);

        this.writer.Write<byte>(buffer);

        if (Vector256.IsHardwareAccelerated && integers.Length >= 32)
        {
            this.WriteInt16TermArrayV256(integers);
        }
        else if (Vector128.IsHardwareAccelerated && integers.Length >= 8)
        {
            this.WriteInt16TermArrayV128(integers);
        }
        else
        {
            this.WriteInt16TermArrayScalarAndNullTerminate(integers);
        }

        this.DecrementAndWriteNullTerminator();

        return true;
    }

    private readonly void WriteByteTermArrayScalarAndNullTerminate
    (
        ReadOnlySpan<byte> bytes
    )
    {
        Span<byte> buffer = stackalloc byte[2];
        buffer[0] = (byte)TermType.SmallInteger;

        foreach (byte b in bytes)
        {
            buffer[1] = b;

            this.writer.Write<byte>(buffer);
        }

        this.writer.Write((byte)TermType.Nil);
    }

    private readonly void WriteInt32TermArrayScalarAndNullTerminate
    (
        ReadOnlySpan<int> integers
    )
    {
        Span<byte> buffer = stackalloc byte[5];
        buffer[0] = (byte)TermType.SmallInteger;

        foreach(int i in integers)
        {
            BinaryPrimitives.WriteInt32BigEndian(buffer[1..], i);

            this.writer.Write<byte>(buffer);
        }

        this.writer.Write((byte)TermType.Nil);
    }

    private readonly void WriteInt16TermArrayScalarAndNullTerminate
    (
        ReadOnlySpan<short> integers
    )
    {
        Span<byte> buffer = stackalloc byte[5];
        buffer[0] = (byte)TermType.SmallInteger;

        foreach (int i in integers)
        {
            BinaryPrimitives.WriteInt32BigEndian(buffer[1..], i);

            this.writer.Write<byte>(buffer);
        }

        this.writer.Write((byte)TermType.Nil);
    }
}
