using System;

using ETFKit.Serialization;

using Xunit;

namespace ETFKit.Tests.Serialization.EtfReaderTests;

partial class StructureTests
{
    private static readonly byte[] tuplePayload =
    [
        // ETF version header
        0x83,

        // tuple
        0x68, 0x02,

            // first child term
            0x62, 0x00, 0x00, 0x00, 0x07,

            // second child term
            0x61, 0x7F
    ];

    [Fact]
    public void TestReadingTuple()
    {
        ReadOnlySpan<byte> span = [.. tuplePayload];
        EtfReader reader = new
        (
            span,
            stackalloc uint[1],
            stackalloc TermType[1]
        );

        Assert.True(reader.Read());
        Assert.Equal(TermType.SmallTuple, reader.TermType);
        Assert.Equal(EtfTokenType.StartTuple, reader.TokenType);

        Assert.True(reader.Read());
        Assert.Equal((uint)1, reader.GetCurrentRemainingLength());

        _ = reader.Read();

        // are we correctly synthesizing the last end token, even if there is no actual data left?
        Assert.True(reader.Read());
        Assert.Equal(EtfTokenType.EndTuple, reader.TokenType);
    }
}
