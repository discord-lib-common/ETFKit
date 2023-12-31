using System;
using System.Numerics;

using ETFKit.Serialization;

using Xunit;

namespace ETFKit.Tests.Serialization.EtfReaderTests;

partial class PrimitiveTests
{
    private static readonly byte[] bigIntegerPayload =
    [
        // ETF version header
        0x83,

        // small big integer
        0x6E, 0x02, 0x00, 0x01, 0x01,

        // big big integer
        0x6F, 0x00, 0x00, 0x00, 0x02, 0x01, 0x01, 0x01
    ];

    [Fact]
    public void TestSmallBigInteger()
    {
        ReadOnlySpan<byte> span = [.. bigIntegerPayload];
        EtfReader reader = new
        (
            span,
            stackalloc uint[1],
            stackalloc TermType[1]
        );

        Assert.True(reader.Read());

        Assert.Equal(TermType.SmallBig, reader.TermType);
        Assert.Equal(new BigInteger(257), reader.ReadBigInteger());
        Assert.Equal(257UL, reader.ReadUInt64());
    }

    [Fact]
    public void TestLargeBigInteger()
    {
        ReadOnlySpan<byte> span = [.. bigIntegerPayload];
        EtfReader reader = new
        (
            span,
            stackalloc uint[1],
            stackalloc TermType[1]
        );

        _ = reader.Read();

        Assert.True(reader.Read());

        Assert.Equal(TermType.LargeBig, reader.TermType);
        Assert.Equal(new BigInteger(-257), reader.ReadBigInteger());
        Assert.Equal(-257L, reader.ReadInt64());
    }
}
