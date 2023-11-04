using System;
using System.Buffers.Binary;
using System.Runtime.Intrinsics;

using CommunityToolkit.HighPerformance;

namespace ETFKit.Serialization;

// contains the public API for fast array writing, if available.
partial struct EtfWriter
{
    public readonly bool TryWriteByteTermArray
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
            return this.WriteByteTermArrayV256(bytes);
        }
        else if (Vector128.IsHardwareAccelerated && bytes.Length >= 16)
        {
            return this.WriteByteTermArrayV128(bytes);
        }
        else
        {
            this.WriteByteTermArrayScalarAndNullTerminate(bytes);
        }

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
}
