// This Source Code form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Text;

using CommunityToolkit.HighPerformance;

namespace ETFKit.Serialization;

partial struct EtfWriter
{
    /// <summary>
    /// Writes a small-integer term.
    /// </summary>
    /// <returns>A value indicating whether this operation succeeded.</returns>
    public bool TryWriteSmallIntegerTerm
    (
        byte value
    )
    {
        this.writer.Write([(byte)TermType.SmallInteger, value]);

        this.DecrementAndWriteNullTerminator();

        return true;
    }

    /// <summary>
    /// Writes an integer term.
    /// </summary>
    /// <returns>A value indicating whether this operation succeeded.</returns>
    public bool TryWriteIntegerTerm
    (
        int value
    )
    {
        Span<byte> buffer = stackalloc byte[5];
        buffer[0] = (byte)TermType.Integer;

        BinaryPrimitives.WriteInt32BigEndian(buffer[1..], value);

        this.writer.Write<byte>(buffer);

        this.DecrementAndWriteNullTerminator();

        return true;
    }

    /// <summary>
    /// Writes a float term. This method should never be used, use <seealso cref="TryWriteNewFloatTerm(double)"/> instead.
    /// </summary>
    /// <returns>A value indicating whether the write operation succeeded.</returns>
    [Obsolete("This term should not be used. Use WriteNewFloatTerm instead.", DiagnosticId = "ETF4000")]
    public bool TryWriteFloatTerm
    (
        double value
    )
    {
        Span<byte> buffer = stackalloc byte[32];
        buffer[0] = (byte)TermType.Float;

        bool success = value.TryFormat
        (
            utf8Destination: buffer[1..],
            bytesWritten: out int _,
            format: ".20e".AsSpan(),
            provider: CultureInfo.InvariantCulture.NumberFormat
        );

        if (!success)
        {
            return false;
        }

        this.writer.Write<byte>(buffer);

        this.DecrementAndWriteNullTerminator();

        return true;
    }

    /// <summary>
    /// Writes a new float term.
    /// </summary>
    /// <returns>A value indicating whether this operation succeeded.</returns>
    public bool TryWriteNewFloatTerm
    (
        double value
    )
    {
        Span<byte> buffer = stackalloc byte[9];
        buffer[0] = (byte)TermType.Integer;

        BinaryPrimitives.WriteDoubleBigEndian(buffer[1..], value);

        this.writer.Write<byte>(buffer);

        this.DecrementAndWriteNullTerminator();

        return true;
    }

    /// <summary>
    /// Writes a small atom term.
    /// </summary>
    /// <returns>A value indicating whether this operation succeeded.</returns>
    public bool TryWriteSmallAtomTerm
    (
        string value
    )
    {
        if (Ascii.IsValid(value))
        {
            if (value.Length >= 255)
            {
                return false;
            }

            Span<byte> buffer = stackalloc byte[value.Length];

            OperationStatus result = Ascii.FromUtf16
            (
                value,
                buffer,
                out int _
            );

            return result == OperationStatus.Done && this.TryWriteSmallAtomTerm(buffer);
        }

        return Encoding.UTF8.GetByteCount(value) <= 255 && TryWriteSmallAtomTerm(Encoding.UTF8.GetBytes(value));
    }

    /// <summary>
    /// Writes a small atom term.
    /// </summary>
    /// <returns>A value indicating whether this operation succeeded.</returns>
    public bool TryWriteSmallAtomTerm
    (
        scoped ReadOnlySpan<byte> value
    )
    {
        if (value.Length >= 255)
        {
            return false;
        }

        this.writer.Write([(byte)TermType.SmallAtomUtf8, (byte)value.Length]);
        this.writer.Write(value);

        this.DecrementAndWriteNullTerminator();

        return true;
    }

    /// <summary>
    /// Writes an atom term.
    /// </summary>
    /// <returns>A value indicating whether this operation succeeded.</returns>
    public bool TryWriteAtomTerm
    (
        string value
    )
    {
        if (Ascii.IsValid(value))
        {
            if (value.Length >= ushort.MaxValue)
            {
                return false;
            }

            Span<byte> buffer = stackalloc byte[value.Length];

            OperationStatus result = Ascii.FromUtf16
            (
                value,
                buffer,
                out int _
            );

            return result == OperationStatus.Done && this.TryWriteAtomTerm(buffer);
        }

        return Encoding.UTF8.GetByteCount(value) <= ushort.MaxValue && TryWriteAtomTerm(Encoding.UTF8.GetBytes(value));
    }

    /// <summary>
    /// Writes an atom term.
    /// </summary>
    /// <returns>A value indicating whether this operation succeeded.</returns>
    public bool TryWriteAtomTerm
    (
        scoped ReadOnlySpan<byte> value
    )
    {
        if (value.Length >= ushort.MaxValue)
        {
            return false;
        }

        this.writer.Write((byte)TermType.AtomUtf8);

        Span<byte> length = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)value.Length);

        this.writer.Write(length);

        this.writer.Write(value);

        this.DecrementAndWriteNullTerminator();

        return true;
    }

    /// <summary>
    /// Writes a string term.
    /// </summary>
    /// <returns>A value indicating whether this operation succeeded.</returns>
    public bool TryWriteStringTerm
    (
        string value
    )
    {
        if (Ascii.IsValid(value))
        {
            if (value.Length >= ushort.MaxValue)
            {
                return false;
            }

            Span<byte> buffer = stackalloc byte[value.Length];

            OperationStatus result = Ascii.FromUtf16
            (
                value,
                buffer,
                out int _
            );

            return result == OperationStatus.Done && this.TryWriteStringTerm(buffer);
        }

        return Encoding.UTF8.GetByteCount(value) <= ushort.MaxValue && TryWriteStringTerm(Encoding.UTF8.GetBytes(value));
    }

    /// <summary>
    /// Writes a string term.
    /// </summary>
    /// <returns>A value indicating whether this operation succeeded.</returns>
    public bool TryWriteStringTerm
    (
        scoped ReadOnlySpan<byte> value
    )
    {
        if (value.Length >= ushort.MaxValue)
        {
            return false;
        }

        this.writer.Write((byte)TermType.String);

        Span<byte> length = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)value.Length);

        this.writer.Write(length);

        this.writer.Write(value);

        this.DecrementAndWriteNullTerminator();

        return true;
    }

    /// <summary>
    /// Writes a small-integer term.
    /// </summary>
    public void WriteSmallIntegerTerm(byte value)
        => this.TryWriteSmallIntegerTerm(value);

    /// <summary>
    /// Writes an integer term.
    /// </summary>
    public void WriteIntegerTerm(int value)
        => this.TryWriteIntegerTerm(value);

    /// <summary>
    /// Writes a float term. This method should never be used, use <seealso cref="WriteNewFloatTerm(double)"/> instead.
    /// </summary>
    [Obsolete("This term should not be used. Use WriteNewFloatTerm instead.", DiagnosticId = "ETF4000")]
    public void WriteFloatTerm
    (
        double value
    )
    {
        if (!this.TryWriteFloatTerm(value))
        {
            ThrowHelper.ThrowInvalidFloatEncode(value);
        }
    }

    /// <summary>
    /// Writes a new float term.
    /// </summary>
    public void WriteNewFloatTerm(double value)
        => this.TryWriteNewFloatTerm(value);

    /// <summary>
    /// Writes a small atom term.
    /// </summary>
    public void WriteSmallAtomTerm
    (
        string value
    )
    {
        if (!this.TryWriteSmallAtomTerm(value))
        {
            ThrowHelper.ThrowInvalidStringEncode(TermType.SmallAtomUtf8);
        }
    }

    /// <summary>
    /// Writes a small atom term.
    /// </summary>
    public void WriteSmallAtomTerm
    (
        scoped ReadOnlySpan<byte> value
    )
    {
        if (!this.TryWriteAtomTerm(value))
        {
            ThrowHelper.ThrowInvalidStringEncode(TermType.SmallAtomUtf8);
        }
    }

    /// <summary>
    /// Writes an atom term.
    /// </summary>
    public void WriteAtomTerm
    (
        string value
    )
    {
        if (!this.TryWriteAtomTerm(value))
        {
            ThrowHelper.ThrowInvalidStringEncode(TermType.AtomUtf8);
        }
    }

    /// <summary>
    /// Writes an atom term.
    /// </summary>
    public void WriteAtomTerm
    (
        scoped ReadOnlySpan<byte> value
    )
    {
        if (!this.TryWriteAtomTerm(value))
        {
            ThrowHelper.ThrowInvalidStringEncode(TermType.AtomUtf8);
        }
    }

    /// <summary>
    /// Writes a string term.
    /// </summary>
    public void WriteStringTerm
    (
        string value
    )
    {
        if (!this.TryWriteStringTerm(value))
        {
            ThrowHelper.ThrowInvalidStringEncode(TermType.String);
        }
    }

    /// <summary>
    /// Writes a string term.
    /// </summary>
    public void WriteStringTerm
    (
        scoped ReadOnlySpan<byte> value
    )
    {
        if (!this.TryWriteStringTerm(value))
        {
            ThrowHelper.ThrowInvalidStringEncode(TermType.String);
        }
    }
}
