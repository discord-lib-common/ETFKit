using System;

using Bundles.ValueCollections;

using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;

namespace ETFKit.Serialization;

/// <summary>
/// Provides a high-performance, low-allocation API for writing Erlang External Term Format, version 131,
/// encoded data. It writes binary data sequentially with no caching, and without support for non-standard
/// data or older format versions according to the specification at
/// <seealso href="https://www.erlang.org/doc/apps/erts/erl_ext_dist"/>.
/// </summary>
/// <remarks>
/// This type writes uncompressed data, and any compression needs to be implemented on top of it.
/// </remarks>
public ref partial struct EtfWriter
{
    private readonly ArrayPoolBufferWriter<byte> writer;

    private ValueStack<uint> remainingLengths;
    private ValueStack<TermType> complexObjects;

    /// <summary>
    /// Retrieves the data written to the underlying buffer as a <seealso cref="ReadOnlySpan{T}"/>.
    /// </summary>
    public readonly ReadOnlySpan<byte> WrittenSpan => this.writer.WrittenSpan;

    /// <summary>
    /// Retrieves the data written to the underlying buffer as a <seealso cref="ReadOnlyMemory{T}"/>.
    /// </summary>
    public readonly ReadOnlyMemory<byte> WrittenMemory => this.writer.WrittenMemory;

    /// <summary>
    /// Gets the amount of data written so far, in bytes.
    /// </summary>
    public readonly int WrittenCount => this.writer.WrittenCount;

    /// <summary>
    /// Initializes a new EtfWriter with default settings.
    /// </summary>
    public EtfWriter()
        : this
        (
            // 4096 is the most common page size on systems we run on
            4096,

            // 256 as default max depth matches EtfReader
            256
        )
    {

    }

    /// <summary>
    /// Initializes a new EtfWriter with the provided initial capacity and default maximum depth.
    /// </summary>
    public EtfWriter
    (
        int initialCapacity
    )
        : this
        (
            initialCapacity,
            256
        )
    {

    }

    /// <summary>
    /// Initializes a new EtfWriter with the provided initial capacity and maximum depth.
    /// </summary>
    public EtfWriter
    (
        int initialCapacity,
        int maxDepth
    )
        : this
        (
            new ArrayPoolBufferWriter<byte>(initialCapacity),
            maxDepth
        )
    { 
    
    }

    /// <summary>
    /// Initializes a new EtfWriter using the provided <seealso cref="ArrayPoolBufferWriter{T}"/> and maximum depth.
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="maxDepth"></param>
    public EtfWriter
    (
        ArrayPoolBufferWriter<byte> writer,
        int maxDepth
    )
        : this
        (
            writer, 
            new(new uint[maxDepth]), 
            new(new TermType[maxDepth])
        )
    { 
    
    }

    /// <summary>
    /// Initializes a new EtfWriter using the provided <seealso cref="ArrayPoolBufferWriter{T}"/> and backing state
    /// collections. These must match in length, and their length constitutes the maximum writing depth.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if the provided stacks do not match in length.</exception>
    public EtfWriter
    (
        ArrayPoolBufferWriter<byte> writer,
        ValueStack<uint> lengths,
        ValueStack<TermType> objects
    )
    {
        if (lengths.Capacity != objects.Capacity)
        {
            throw new ArgumentException("Inconsistent maximum depth between object lengths and object types.");
        }

        this.writer = writer;
        this.remainingLengths = lengths;
        this.complexObjects = objects;

        writer.Write<byte>(0x83);
    }

    private void DecrementAndWriteNullTerminator()
    {
        scoped ref uint length = ref this.remainingLengths.PeekRef();
        length--;

        if (length > 0)
        {
            return;
        }

        // length == 0
        this.remainingLengths.Pop();
        TermType presentTerm = this.complexObjects.Pop();

        // "null"-terminate a list
        if (presentTerm == TermType.List)
        {
            this.writer.Write((byte)TermType.Nil);
        }

        // this might've terminated something else, sooooo
        this.DecrementAndWriteNullTerminator();
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public readonly void Dispose()
        => this.writer.Dispose();
}
