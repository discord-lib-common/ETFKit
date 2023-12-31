using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ETFKit.Serialization;

/// <summary>
/// Provides methods to relegate <see langword="throw"/>s to, to work around inlining limitations in the JIT.
/// </summary>
internal static class ThrowHelper
{
    [DoesNotReturn]
    [DebuggerHidden]
    [StackTraceHidden]
    public static void ThrowInvalidDecode(Type target)
        => throw new InvalidOperationException($"Failed to decode the current term into an object of type {target}.");

    [DoesNotReturn]
    [DebuggerHidden]
    [StackTraceHidden]
    public static void ThrowInvalidFloatEncode(double value)
        => throw new InvalidOperationException($"Failed to encode {value} as old float term. Consider using a new float term instead.");

    [DoesNotReturn]
    [DebuggerHidden]
    [StackTraceHidden]
    public static void ThrowInvalidStringEncode(TermType term)
        => throw new InvalidOperationException($"Failed to encode the given string as {term}.");

    [DoesNotReturn]
    [DebuggerHidden]
    [StackTraceHidden]
    public static void ThrowInvalidStructureStart(TermType term)
        => throw new InvalidOperationException($"Failed to start a structure of type {term}.");
}
