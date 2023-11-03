// This Source Code form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

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
    private readonly unsafe bool WriteByteTermArrayV128
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
        ref byte writtenStart = ref MemoryMarshal.GetReference(bytes);

        // we process 16 at a time, using one read and two writes
        while (processed + 16 <= bytes.Length)
        {
            // we prepare this at half size of the full vector, we'll late overwrite one half of the vector
            // with this
            Vector64<byte> byteTermIndicator = Vector64.CreateScalar((byte)TermType.SmallInteger);

            // firstly, we use 0 - 15 instead of some impossibly high sentinel value as optimization for ARM,
            // secondly, we use each value once as optimization for the fallback implementation.
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

        return true;
    }
}
