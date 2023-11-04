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
// this code deals with 256-bit vectorized writing of SmallInteger, Integer, NewFloat and certain BigInteger
// arrays (that is, lists of the specified term type). 
// this code is commented, but it's commented with the assumption of proficiency in vectorization. basics of
// SIMD are not explained, and it is recommended to have the Intel Intrinsics Guide or Felix Cloutier's x86
// instruction reference on hand, at https://felixcloutier.com/x86
partial struct EtfWriter
{
    private readonly unsafe bool WriteByteTermArrayV256
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

            // firstly, we use 0 - 31 instead of some impossibly high sentinel value as optimization for ARM,
            // secondly, we use each value once as optimization for the fallback implementation.
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

            // specifying Avx2 here helps the JIT decide what to do, it might otherwise fall back to 2xSSSE3
            if (Avx2.IsSupported)
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

        return true;
    }
}
