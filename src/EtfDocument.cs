#pragma warning disable CA1063

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using ETFKit.Serialization;

namespace ETFKit;

/// <summary>
/// Provides a mechanism for examining the structure and layout of ETF-encoded data.
/// </summary>
public abstract class EtfDocument : IDisposable
{
    /// <summary>
    /// Parses a document model from the provided EtfReader.
    /// </summary>
    /// <remarks>
    /// The reader will be advanced to the end of the current scope, and cannot be rewound.
    /// </remarks>
    /// <param name="reader">The source containing the ETF data in question</param>
    /// <param name="options">Additional options for document model creation.</param>
    /// <returns>The created document model.</returns>
    public static EtfDocument Parse(EtfReader reader, EtfDocumentOptions options)
        => new InlineEtfDocument();

    /// <summary>
    /// Parses a document model from the provided binary data.
    /// </summary>
    /// <remarks>
    /// This method is unaware of preceding structure and must not be called on data without length
    /// and structure information.
    /// </remarks>
    /// <param name="data">The source containing the ETF data in question</param>
    /// <param name="options">Additional options for document model creation.</param>
    /// <returns>The created document model.</returns>
    public static EtfDocument Parse(ReadOnlySpan<byte> data, EtfDocumentOptions options)
        => new InlineEtfDocument();

    /// <summary>
    /// The root element of this document model.
    /// </summary>
    public abstract EtfElement RootElement { get; }

    /// <summary>
    /// Indicates whether the root element is synthesized because there were multiple provided terms
    /// without any provided direct ancestor.
    /// </summary>
    public abstract bool HasSyntheticRootElement { get; }

    /// <inheritdoc/>
    public abstract void Dispose();

    /// <summary>
    /// Writes the contents of this document model to the specified writer.
    /// </summary>
    public abstract void WriteTo(EtfWriter writer);

    // --- internal implementation methods for EtfElement --- //

    /// <summary>
    /// Retrieves the term type for the token at the given index.
    /// </summary>
    protected internal abstract TermType GetTermType(long index);

    /// <summary>
    /// Retrieves the synthetic token type for the token at the given index.
    /// </summary>
    protected internal abstract EtfTokenType GetTokenType(long index);

    /// <summary>
    /// Retrieves an array (list/tuple) element at the given index.
    /// </summary>
    /// <param name="currentIndex">The index of the list or tuple token.</param>
    /// <param name="arrayIndex">The index of the array element to retrieve.</param>
    /// <param name="element">The ETF element at the given index.</param>
    /// <returns>A value indicating whether this operation was successful.</returns>
    protected internal abstract bool TryGetArrayElementAtIndex
    (
        long currentIndex,
        int arrayIndex,
        out EtfElement element
    );

    /// <summary>
    /// Retrieves a child property of a map at the given index.
    /// </summary>
    /// <param name="currentIndex">The index of the map token.</param>
    /// <param name="propertyIndex">The index of the value to retrieve.</param>
    /// <param name="element">The ETF element at the given index.</param>
    /// <returns>A value indicating whether this operation was successful.</returns>
    protected internal abstract bool TryGetChildProperty
    (
        long currentIndex,
        int propertyIndex,
        out EtfElement element
    );

    /// <summary>
    /// Retrieves a child property of a map with the given name.
    /// </summary>
    /// <param name="currentIndex">The index of the map token.</param>
    /// <param name="propertyName">The name of the value to retrieve.</param>
    /// <param name="element">The ETF element at the given index.</param>
    /// <returns>A value indicating whether this operation was successful.</returns>
    protected internal abstract bool TryGetChildProperty
    (
        long currentIndex,
        string propertyName,
        out EtfElement element
    );

    /// <summary>
    /// Retrieves the value to the given key from a map.
    /// </summary>
    /// <param name="currentIndex">The index of the map token.</param>
    /// <param name="propertyKey">The key token.</param>
    /// <param name="element">The ETF element corresponding to the requested value.</param>
    /// <param name="matchData">Indicates whether to match by data, not by token identity.</param>
    /// <returns>A value indicating whether this operation was successful.</returns>
    protected internal abstract bool TryGetChildProperty
    (
        long currentIndex,
        EtfElement propertyKey,
        out EtfElement element,
        bool matchData = false
    );

    /// <summary>
    /// Retrieves a child property of a map by value data.
    /// </summary>
    /// <remarks>
    /// It is recommended that implementers optimize for string keys.
    /// </remarks>
    /// <param name="currentIndex">The index of the map token.</param>
    /// <param name="data">The data of the key.</param>
    /// <param name="element">The ETF element corresponding to the requested value.</param>
    /// <returns>A value indicating wehther this operation was successful.</returns>
    protected internal abstract bool TryGetChildProperty
    (
        long currentIndex,
        ReadOnlySpan<byte> data,
        out EtfElement element
    );

    /// <summary>
    /// Writes the token and all child tokens to the provided writer.
    /// </summary>
    /// <param name="index">The index of the first token to write.</param>
    /// <param name="writer">The writer instance to write to.</param>
    protected internal abstract void WriteTo(long index, EtfWriter writer);

    /// <summary>
    /// Retrieves the value of the specified token.
    /// </summary>
    /// <param name="index">The index of the specified token.</param>
    /// <param name="value">The retrieved value.</param>
    /// <returns>A value indicating whether this operation succeeded.</returns>
    protected internal abstract bool TryGetValue<T>
    (
        long index,

        [MaybeNullWhen(false)]
        out T value
    );

    /// <summary>
    /// If this token is a map value, this retrieves its key.
    /// </summary>
    /// <param name="index">The index of the value.</param>
    /// <param name="name">A buffer to write the key to.</param>
    /// <returns>A value indicating whether this operation was successful.</returns>
    protected internal abstract bool GetNameOfPropertyValue(long index, ReadOnlySpan<byte> name);

    /// <summary>
    /// If this token is an array value, this retrieves its index.
    /// </summary>
    /// <param name="index">The document index of the value.</param>
    /// <param name="value">The index within this array, if found.</param>
    /// <returns>A value indicating whether this operation was successful.</returns>
    protected internal abstract bool GetIndexOfArrayValue(long index, out int value);

    /// <summary>
    /// Gets a string representation for a specified token.
    /// </summary>
    /// <param name="index">The index of the token to stringify.</param>
    /// <param name="provider">A format provider for potential contained values.</param>
    /// <param name="format">
    /// A format string to stringify the token by. Refer to the documentation to see legal format instructions.
    /// </param>
    /// <returns>The created string.</returns>
    protected internal abstract string GetStringRepresentation(long index, IFormatProvider provider, string format);

    /// <summary>
    /// Checks whether two tokens are equal in value.
    /// </summary>
    /// <param name="index">The index of the first token.</param>
    /// <param name="comparisonIndex">The index of the second token.</param>
    protected internal abstract bool ValueEquals(long index, long comparisonIndex);

    /// <summary>
    /// Gets the projected child types of a map. It is possible that actually encountered types deviate,
    /// and especially the <paramref name="valueType"/> should be taken with a grain of salt.
    /// </summary>
    /// <param name="index">The index of the map token.</param>
    /// <param name="keyType">The projected type of map keys.</param>
    /// <param name="valueType">The projected type of map values.</param>
    /// <returns>A value indicating whether this operation was successful.</returns>
    protected internal abstract bool TryGetMapChildTypes(long index, out TermType keyType, out TermType valueType);

    /// <summary>
    /// Gets the projected child type of a list. It is possible that actually encountered types deviate,
    /// and the result should be taken with a grain of salt.
    /// </summary>
    /// <param name="index">The index of the list token.</param>
    /// <param name="type">The projected type of list values.</param>
    /// <returns>A value indicating whether this operation was successful.</returns>
    protected internal abstract bool TryGetListValueType(long index, out TermType type);

    /// <summary>
    /// Verifies whether the keys of this map are homogenous in type.
    /// </summary>
    /// <param name="index">The index of the map token.</param>
    protected internal abstract bool IsMapHomogenous(long index);

    /// <summary>
    /// Verifies whether the values of this map are homogenous in type.
    /// </summary>
    /// <param name="index">The index of the map token.</param>
    protected internal abstract bool IsMapValueHomogenous(long index);

    /// <summary>
    /// Verifies whether the values of this list are homogenous in type.
    /// </summary>
    /// <param name="index">The index of the list token.</param>
    protected internal abstract bool IsListHomogenous(long index);

    /// <summary>
    /// Gets the length of the specified complex structure.
    /// </summary>
    /// <param name="index">The index of the complex structure.</param>
    /// <returns>A value indicating whether this operation was successful.</returns>
    protected internal abstract bool TryGetStructureLength(long index);

    /// <summary>
    /// Enumerates the child tokens of the specified list/tuple.
    /// </summary>
    /// <param name="index">The index of the complex list/tuple.</param>
    protected internal abstract IEnumerable<EtfElement> EnumerateChildTokens(long index);

    /// <summary>
    /// Enumerates the key-value pairs of the specified map.
    /// </summary>
    /// <param name="index">The index of the map token.</param>
    protected internal abstract IEnumerable<KeyValuePair<EtfElement, EtfElement>> EnumerateProperties(long index);
}
