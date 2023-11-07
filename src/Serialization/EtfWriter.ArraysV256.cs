#pragma warning disable CS9081
#pragma warning disable IDE0045

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
// this code deals with 256-bit vectorized writing of SmallInteger, Integer, NewFloat and certain BigInteger
// arrays (that is, lists of the specified term type). 
// this code is commented, but it's commented with the assumption of proficiency in vectorization. basics of
// SIMD are not explained, and it is recommended to have the Intel Intrinsics Guide or Felix Cloutier's x86
// instruction reference on hand, at https://felixcloutier.com/x86
partial struct EtfWriter
{
    [SkipLocalsInit]
    private readonly unsafe void WriteByteTermArrayV256
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

        // process 32 at a time, following the same scheme as v128 with one read and two writes
        while (processed + 32 <= bytes.Length)
        {
            // we prepare this at half size of the full vector, we'll late overwrite one half of the vector
            // with this
            Vector128<byte> byteTermIndicator = Vector128.CreateScalar((byte)TermType.SmallInteger);

            // we use each value once as optimization for the fallback implementation.
            // these are the indices of the bytes we want to reshuffle, alternating a byte from the term
            // indicator with a byte from the data provided; once for the lower and once for the upper half
            // of the data segment in a pattern of indicator - value - indicator - value...
            Vector256<byte> lowerSegmentMask = Vector256.Create
            (
                (byte)16, 0, 17, 1, 18, 2, 19, 3, 20, 4, 21, 5, 22, 6, 23, 7,
                24, 8, 25, 9, 26, 10, 27, 11, 28, 12, 29, 13, 30, 14, 31, 15
            );

            Vector256<byte> upperSegmentMask = Vector256.Create
            (
                (byte)0, 16, 1, 17, 2, 18, 3, 19, 4, 20, 5, 21, 6, 22, 7, 23,
                8, 24, 9, 25, 10, 26, 11, 27, 12, 28, 13, 29, 14, 30, 15, 31
            );

            Vector256<byte> segment = Vector256.LoadUnsafe(ref Unsafe.Add(ref start, processed));

            Vector256<byte> lowerSegment = segment.WithUpper(byteTermIndicator);
            Vector256<byte> upperSegment = segment.WithLower(byteTermIndicator);

            // if AVX-512-VBMI is available that's just the fastest option
            // specifying AVX2 here helps the JIT decide what to do, it might otherwise fall back to 2xSSSE3
            if (Avx512Vbmi.VL.IsSupported)
            {
                lowerSegment = Avx512Vbmi.VL.PermuteVar32x8(lowerSegment, lowerSegmentMask);
                upperSegment = Avx512Vbmi.VL.PermuteVar32x8(upperSegment, upperSegmentMask);
            }
            else if (Avx2.IsSupported)
            {
                lowerSegment = Avx2.Shuffle(lowerSegment, lowerSegmentMask);
                upperSegment = Avx2.Shuffle(upperSegment, upperSegmentMask);
            }
            else
            {
                lowerSegment = Vector256.Shuffle(lowerSegment, lowerSegmentMask);
                upperSegment = Vector256.Shuffle(upperSegment, upperSegmentMask);
            }

            lowerSegment.StoreUnsafe(ref Unsafe.Add(ref writtenStart, written));
            upperSegment.StoreUnsafe(ref Unsafe.Add(ref writtenStart, written + 32));

            written += 64;
            processed += 32;
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
    private readonly unsafe void WriteInt32TermArrayV256
    (
        ReadOnlySpan<int> integers
    )
    {
        Span<byte> writtenBytes;
        byte[]? backingWritten = null;

        // + 2 here because we write six terms per iteration, or 30 bytes, but the vector length
        // is 32 bytes, so we over-write by two bytes. we later cut those bytes off when writing to
        // the output buffer.
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

        // eight times the integer term indicator, see comments on the mask construction for details
        const int integerTermPrefix = 0x62626262;
        uint processed = 0, written = 0;
        ref int start = ref MemoryMarshal.GetReference(integers);
        ref byte writtenStart = ref MemoryMarshal.GetReference(writtenBytes);

        // + 4 is a bounds check - we only process 3 per iteration
        while (processed + 8 <= integers.Length)
        {
            Vector256<int> source = Vector256.LoadUnsafe(ref Unsafe.Add(ref start, processed));

            // quick intermission to Vector256<long> because that ends up saving one WithElement
            // call (we'd have to call WithElement to indices 6 and 7 if we kept as integers), which in
            // turn improves codegen because the JIT wouldn't actually fuse them very effectively.
            Vector256<long> intermediary = source.AsInt64().WithElement(3, integerTermPrefix);

            // indices 24 through 31 are the term prefix, we just fill the last two bytes with something
            // irrelevant; indices 0 through 23 are the six integers we write, shufflign to big endian.
            // this obviously only works on LE but we're making the assumption of LE quite a bit anyways.
            Vector256<byte> segment = intermediary.AsByte();
            Vector256<byte> mask = Vector256.Create
            (
                (byte)24, 3, 2, 1, 0, 25, 7, 6, 5, 4, 26, 11, 10, 9, 8,
                27, 15, 14, 13, 12, 28, 19, 18, 17, 16, 29, 23, 22, 21, 20, 30, 31
            );

            // same notes to codegen as in byte array
            if (Avx512Vbmi.VL.IsSupported)
            {
                segment = Avx512Vbmi.VL.PermuteVar32x8(segment, mask);
            }
            else if (Avx2.IsSupported)
            {
                segment = Avx2.Shuffle(segment, mask);
            }
            else
            {
                segment = Vector256.Shuffle(segment, mask);
            }

            segment.StoreUnsafe(ref Unsafe.Add(ref writtenStart, written));

            processed += 5;
            written += 30;
        }

        // cut the last byte off, as promised
        this.writer.Write(writtenBytes[..^2]);

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

    [SkipLocalsInit]
    private readonly unsafe void WriteInt16TermArrayV256
    (
        ReadOnlySpan<short> integers
    )
    {
        Span<byte> writtenBytes;
        byte[]? backingWritten = null;

        // try to not be entirely stupid about stackallocs. this is pretty primitive,
        // and it probably wouldn't matter to heapalloc this, but...
        if (integers.Length < 512)
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

        // process 32 at a time, following the same scheme as v128 with one read and two writes
        while (processed + 32 <= integers.Length)
        {
            // we prepare this at half size of the full vector, we'll late overwrite one half of the vector
            // with this
            Vector128<byte> zeros = Vector128.CreateScalar((byte)0);

            //  we use each value once as optimization for the fallback implementation.
            // these are the indices of the bytes we want to reshuffle, expanding the int16s to int32s.
            Vector256<byte> lowerSegmentMask = Vector256.Create
            (
                (byte)0, 1, 16, 17, 2, 3, 18, 19, 4, 5, 20, 21, 6, 7, 22, 23,
                8, 9, 24, 25, 10, 11, 26, 27, 12, 13, 28, 29, 14, 15, 30, 31
            );

            Vector256<byte> upperSegmentMask = Vector256.Create
            (
                (byte)16, 17, 0, 1, 18, 19, 2, 3, 20, 21, 4, 5, 22, 23, 6, 7,
                24, 25, 8, 9, 26, 27, 10, 11, 28, 29, 12, 13, 30, 31, 14, 15
            );

            Vector256<byte> segment = Vector256.LoadUnsafe(ref Unsafe.Add(ref start, processed));

            Vector256<byte> lowerSegment = segment.WithUpper(zeros);
            Vector256<byte> upperSegment = segment.WithLower(zeros);

            // if AVX-512-VBMI is available that's just the fastest option
            // specifying AVX2 here helps the JIT decide what to do, it might otherwise fall back to 2xSSSE3
            if (Avx512Vbmi.VL.IsSupported)
            {
                lowerSegment = Avx512Vbmi.VL.PermuteVar32x8(lowerSegment, lowerSegmentMask);
                upperSegment = Avx512Vbmi.VL.PermuteVar32x8(upperSegment, upperSegmentMask);
            }
            else if (Avx2.IsSupported)
            {
                lowerSegment = Avx2.Shuffle(lowerSegment, lowerSegmentMask);
                upperSegment = Avx2.Shuffle(upperSegment, upperSegmentMask);
            }
            else
            {
                lowerSegment = Vector256.Shuffle(lowerSegment, lowerSegmentMask);
                upperSegment = Vector256.Shuffle(upperSegment, upperSegmentMask);
            }

            lowerSegment.StoreUnsafe(ref Unsafe.Add(ref writtenStart, written));
            upperSegment.StoreUnsafe(ref Unsafe.Add(ref writtenStart, written + 32));

            written += 64;
            processed += 16;
        }

        foreach (short s in integers[(int)processed..])
        {
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref start, written), (int)s);
            written += 4;
        }

        // just call into our int32 implementation
        this.WriteInt32TermArrayV256(MemoryMarshal.Cast<byte, int>(writtenBytes));

        if (backingWritten is not null)
        {
            ArrayPool<byte>.Shared.Return(backingWritten);
        }
    }
}
