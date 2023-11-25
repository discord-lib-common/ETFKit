using System;
using System.Buffers;
using System.Linq;

using CommunityToolkit.HighPerformance.Buffers;

namespace ETFKit.Stringification;

/// <summary>
/// Handles writing indented text, for pretty-printing the contents of an ETF document.
/// </summary>
internal sealed class IndentedUtf8TextWriter
(
    int baseCapacity = 128,
    int indentWidth = 4
)
    : IDisposable
{
    private static class Newlines
    {
        // characters from S.P.CoreLib/src/System/String.Manipulation.cs
        internal static SearchValues<byte> Values { get; } = SearchValues.Create("\n\r\f\u0085\u2028\u2029"u8);
    }

    private readonly byte[] indent = Enumerable.Repeat<byte>(0x20, indentWidth).ToArray();

    private readonly ArrayPoolBufferWriter<byte> writer = new(baseCapacity);
    private uint currentIndentation;

    public ReadOnlySpan<byte> Written => this.writer.WrittenSpan;

    public void IndentLevel() => this.currentIndentation++;
    public void RemoveIndentationLevel() => this.currentIndentation--;

    /// <summary>
    /// If the text contains newlines, they will be indented.
    /// </summary>
    public void Write(ReadOnlySpan<byte> text)
    {
        int index;

        if ((index = text.IndexOfAny(Newlines.Values)) != 0)
        {
            ReadOnlySpan<byte> line, remaining = text;

            while (index != -1)
            {
                if ((uint)index < (uint)remaining.Length)
                {
                    int stride = 1;

                    if (remaining[index] == '\r' && (uint)(index + 1) < (uint)remaining.Length && remaining[index + 1] == '\n')
                    {
                        stride = 2;
                    }

                    line = remaining[..index];
                    remaining = remaining[(index + stride)..];
                }
                else
                {
                    line = remaining;
                    remaining = default;
                }

                for (int i = 0; i < this.currentIndentation; i++)
                {
                    this.writer.Write(this.indent);
                }

                this.writer.Write(line);
                this.writer.Write("\n"u8);

                index = text.IndexOfAny(Newlines.Values);
            }

            return;
        }

        for (int i = 0; i < this.currentIndentation; i++)
        {
            this.writer.Write(this.indent);
        }

        this.writer.Write(text);
        this.writer.Write("\n"u8);
    }

    public void Dispose() => this.writer.Dispose();
}
