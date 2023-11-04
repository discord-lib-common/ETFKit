using System;

using ETFKit.Serialization;

using Xunit;

namespace ETFKit.Tests.Serialization.EtfReaderTests;

partial class PrimitiveTests
{
    private static readonly byte[] stringPayload =
    [
        // ETF version header
        0x83,

        // small utf8 atom
        0x77, 0x0C, 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x57, 0x6F, 0x72, 0x6C, 0x64, 0x21,

        // large utf8 atom
        0x76, 0x00, 0x0C, 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x57, 0x6F, 0x72, 0x6C, 0x64, 0x21,

        // string
        0x6B, 0x00, 0x0C, 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x57, 0x6F, 0x72, 0x6C, 0x64, 0x21
    ];

    /// <summary>
    /// Tests deserialization of a small UTF8 atom.
    /// </summary>
    [Fact]
    public void TestSmallUtf8Atom()
    {
        ReadOnlySpan<byte> span = [.. stringPayload];
        EtfReader reader = new
        (
            span,
            stackalloc uint[1],
            stackalloc TermType[1]
        );

        Assert.True(reader.Read());

        Assert.Equal("Hello World!", reader.ReadString());

        Span<byte> buffer = stackalloc byte[12];
        reader.ReadUtf8String(buffer);

        Assert.True("Hello World!"u8.SequenceEqual(buffer));
    }

    /// <summary>
    /// Tests deserialization of a large UTF8 atom.
    /// </summary>
    [Fact]
    public void TestLargeUtf8Atom()
    {
        ReadOnlySpan<byte> span = [.. stringPayload];
        EtfReader reader = new
        (
            span,
            stackalloc uint[1],
            stackalloc TermType[1]
        );

        _ = reader.Read();

        Assert.True(reader.Read());

        Assert.Equal("Hello World!", reader.ReadString());

        Span<byte> buffer = stackalloc byte[12];
        reader.ReadUtf8String(buffer);

        Assert.True("Hello World!"u8.SequenceEqual(buffer));
    }

    /// <summary>
    /// Tests deserialization of a string.
    /// </summary>
    [Fact]
    public void TestString()
    {
        ReadOnlySpan<byte> span = [.. stringPayload];
        EtfReader reader = new
        (
            span,
            stackalloc uint[1],
            stackalloc TermType[1]
        );

        _ = reader.Read();
        _ = reader.Read();

        Assert.True(reader.Read());

        Assert.Equal(TermType.String, reader.TermType);
        Assert.Equal("Hello World!", reader.ReadString());
    }
}
