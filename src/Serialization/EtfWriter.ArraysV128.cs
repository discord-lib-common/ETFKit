#pragma warning disable CS9081

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

using CommunityToolkit.HighPerformance;

namespace ETFKit.Serialization;

// warning: ancestral horrors ahead. proceed at your own discretion.
//
// this code deals with 128-bit vectorized writing of SmallInteger, Integer, NewFloat and certain BigInteger
// arrays (that is, lists of the specified term type). 
// this code is commented, but it's commented with the assumption of proficiency in vectorization. basics of
// SIMD are not explained, and it is recommended to have the Intel Intrinsics Guide or Felix Cloutier's x86
// instruction reference on hand, at https://felixcloutier.com/x86
partial struct EtfWriter
{
    [SkipLocalsInit]
    private readonly unsafe void WriteByteTermArrayV128
    (
        ReadOnlySpan<byte> bytes
    )
    {
        Span<byte> writtenBytes;
        byte[]? backingWritten = null;

        // try to not be entirely stupid about stackallocs. this is pretty primitive,
        // and it probably wouldn't matter to heapalloc this, but...
        if (bytes.Length < 512)
        {
            writtenBytes = stackalloc byte[bytes.Length * 2];
        }
        else
        {
            backingWritten = ArrayPool<byte>.Shared.Rent(bytes.Length * 2);
            writtenBytes = backingWritten.AsSpan();
        }
        
        uint processed = 0, written = 0;
        ref byte start = ref MemoryMarshal.GetReference(bytes);
        ref byte writtenStart = ref MemoryMarshal.GetReference(writtenBytes);

        // we process 16 at a time, using one read and two writes
        while (processed + 16 <= bytes.Length)
        {
            // we prepare this at half size of the full vector, we'll late overwrite one half of the vector
            // with this
            Vector64<byte> byteTermIndicator = Vector64.CreateScalar((byte)TermType.SmallInteger);

            // we use each value once as optimization for the fallback implementation.
            // these are the indices of the bytes we want to reshuffle, alternating a byte from the term
            // indicator with a byte from the data provided; once for the lower and once for the upper half
            // of the data segment in a pattern of indicator - value - indicator - value...
            Vector128<byte> lowerSegmentMask = Vector128.Create((byte)8, 0, 9, 1, 10, 2, 11, 3, 12, 4, 13, 5, 14, 6, 15, 7);
            Vector128<byte> upperSegmentMask = Vector128.Create((byte)0, 8, 1, 9, 2, 10, 3, 11, 4, 12, 5, 13, 6, 14, 7, 15);

            Vector128<byte> segment = Vector128.LoadUnsafe(ref Unsafe.Add(ref start, processed));

            Vector128<byte> lowerSegment = segment.WithUpper(byteTermIndicator);
            Vector128<byte> upperSegment = segment.WithLower(byteTermIndicator);

            // we special-case this because Vector128.Shuffle may not actually emit vpshufb, which leads to
            // abysmal codegen and, well, it costs us nothing to do this instead.
            if (Ssse3.IsSupported)
            {
                lowerSegment = Ssse3.Shuffle(lowerSegment, lowerSegmentMask);
                upperSegment = Ssse3.Shuffle(upperSegment, upperSegmentMask);
            }
            else
            {
                lowerSegment = Vector128.Shuffle(lowerSegment, lowerSegmentMask);
                upperSegment = Vector128.Shuffle(upperSegment, upperSegmentMask);
            }

            lowerSegment.StoreUnsafe(ref Unsafe.Add(ref writtenStart, written));
            upperSegment.StoreUnsafe(ref Unsafe.Add(ref writtenStart, written + 16));

            written += 32;
            processed += 16;
        }

        this.writer.Write(writtenBytes);

        // scalar fallback for the rest of the data, if we got it all done in SIMD, null-terminate
        if (processed != bytes.Length)
        {
            this.WriteByteTermArrayScalarAndNullTerminate(bytes[(int)processed..]);
        }
        else
        {
            this.writer.Write((byte)TermType.Nil);
        }

        if (backingWritten is not null)
        {
            ArrayPool<byte>.Shared.Return(backingWritten);
        }
    }

    [SkipLocalsInit]
    private readonly unsafe void WriteInt32TermArrayV128
    (
        ReadOnlySpan<int> integers
    )
    {
        Span<byte> writtenBytes;
        byte[]? backingWritten = null;

        // + 1 here because we write three terms per iteration, or 15 bytes, but the vector length
        // is 16 bytes, so we over-write by one byte. we later cut that byte off when writing to the
        // output buffer.
        // 200 is chosen as threshold to keep the maximum stackalloc at around 1KB, but there's no
        // precise significance to the number
        if (integers.Length < 200)
        {
            writtenBytes = stackalloc byte[(integers.Length * 5) + 1];
        }
        else
        {
            backingWritten = ArrayPool<byte>.Shared.Rent((integers.Length * 5) + 1);
            writtenBytes = backingWritten.AsSpan();
        }

        // four times the integer term indicator, see comments on the mask construction for details
        const int integerTermPrefix = 0x62626262;
        uint processed = 0, written = 0;
        ref int start = ref MemoryMarshal.GetReference(integers);
        ref byte writtenStart = ref MemoryMarshal.GetReference(writtenBytes);

        // + 4 is a bounds check - we only process 3 per iteration
        while (processed + 4 <= integers.Length)
        {
            Vector128<int> source = Vector128.LoadUnsafe(ref Unsafe.Add(ref start, processed));
            source = source.WithElement(3, integerTermPrefix);

            // indices 12 through 15 are the term prefix, we just fill the last byte with something irrelevant;
            // indices 0 through 3, 4 through 7 and 8 through 11 are the three integers we write, appearing in
            // reverse order to write as big endian. convenient.
            // this obviously only works on LE but we're making the assumption of LE quite a bit anyways.
            Vector128<byte> segment = source.AsByte();
            Vector128<byte> mask = Vector128.Create((byte)12, 3, 2, 1, 0, 13, 7, 6, 5, 4, 14, 11, 10, 9, 8, 15);

            segment = Ssse3.IsSupported 
                ? Ssse3.Shuffle(segment, mask) 
                : Vector128.Shuffle(segment, mask);

            segment.StoreUnsafe(ref Unsafe.Add(ref writtenStart, written));

            processed += 3;
            written += 15;
        }

        // cut the last byte off, as promised
        this.writer.Write(writtenBytes[..^1]);

        // scalar fallback for the rest of the data, if we got it all done in SIMD, null-terminate
        if (processed != integers.Length)
        {
            this.WriteInt32TermArrayScalarAndNullTerminate(integers[(int)processed..]);
        }
        else
        {
            this.writer.Write((byte)TermType.Nil);
        }

        if (backingWritten is not null)
        {
            ArrayPool<byte>.Shared.Return(backingWritten);
        }
    }

    private readonly unsafe void WriteInt16TermArrayV128
    (
        ReadOnlySpan<short> integers
    )
    {
        Span<byte> writtenBytes;
        byte[]? backingWritten = null;

        if (integers.Length < 256)
        {
            writtenBytes = stackalloc byte[integers.Length * 4];
        }
        else
        {
            backingWritten = ArrayPool<byte>.Shared.Rent(integers.Length * 4);
            writtenBytes = backingWritten.AsSpan();
        }

        uint processed = 0, written = 0;
        ref byte start = ref MemoryMarshal.GetReference(MemoryMarshal.AsBytes(integers));
        ref byte writtenStart = ref MemoryMarshal.GetReference(writtenBytes);

        while (processed + 8 <= integers.Length)
        {
            // we prepare this at half size of the full vector, we'll late overwrite one half of the vector
            // with this
            Vector64<byte> zeros = Vector64.CreateScalar((byte)0);

            // we use each value once as optimization for the fallback implementation.
            // these are the indices of the bytes we want to reshuffle, zero-extending every short to an int
            Vector128<byte> lowerSegmentMask = Vector128.Create((byte)0, 1, 8, 9, 2, 3, 10, 11, 4, 5, 12, 13, 6, 7, 14, 15);
            Vector128<byte> upperSegmentMask = Vector128.Create((byte)8, 9, 0, 1, 10, 11, 2, 3, 12, 13, 4, 5, 14, 15, 6, 7);

            Vector128<byte> segment = Vector128.LoadUnsafe(ref Unsafe.Add(ref start, processed));

            Vector128<byte> lowerSegment = segment.WithUpper(zeros);
            Vector128<byte> upperSegment = segment.WithLower(zeros);

            // we special-case this because Vector128.Shuffle may not actually emit vpshufb, which leads to
            // abysmal codegen and, well, it costs us nothing to do this instead.
            if (Ssse3.IsSupported)
            {
                lowerSegment = Ssse3.Shuffle(lowerSegment, lowerSegmentMask);
                upperSegment = Ssse3.Shuffle(upperSegment, upperSegmentMask);
            }
            else
            {
                lowerSegment = Vector128.Shuffle(lowerSegment, lowerSegmentMask);
                upperSegment = Vector128.Shuffle(upperSegment, upperSegmentMask);
            }

            lowerSegment.StoreUnsafe(ref Unsafe.Add(ref writtenStart, written));
            upperSegment.StoreUnsafe(ref Unsafe.Add(ref writtenStart, written + 16));

            processed += 8;
            written += 32;
        }

        foreach (short s in integers[(int)processed..])
        {
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref start, written), (int)s);
            written += 4;
        }

        // now that we have integers, just defer to the existing implementation
        this.WriteInt32TermArrayV128(MemoryMarshal.Cast<byte, int>(writtenBytes));

        if (backingWritten is not null)
        {
            ArrayPool<byte>.Shared.Return(backingWritten);
        }
    }
}
