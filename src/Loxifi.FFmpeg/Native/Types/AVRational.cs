// AVRational.cs — FFmpeg's rational number type used for timebases and frame rates.
// FFmpeg uses rational numbers (numerator/denominator pairs) extensively to represent
// time bases (e.g., 1/90000 for MPEG-TS) and frame rates (e.g., 30000/1001 for 29.97fps).

using System.Runtime.InteropServices;

namespace Loxifi.FFmpeg.Native.Types;

/// <summary>
/// Represents an FFmpeg rational number (numerator/denominator pair).
/// Used throughout FFmpeg for timebases, frame rates, and aspect ratios.
/// The field order must match FFmpeg's C struct exactly for correct P/Invoke marshalling.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct AVRational
{
    /// <summary>The numerator of the rational number.</summary>
    public int Numerator;

    /// <summary>The denominator of the rational number.</summary>
    public int Denominator;

    /// <summary>
    /// Creates a new rational number with the given numerator and denominator.
    /// </summary>
    /// <param name="num">The numerator.</param>
    /// <param name="den">The denominator.</param>
    public AVRational(int num, int den)
    {
        Numerator = num;
        Denominator = den;
    }

    /// <summary>
    /// Converts the rational number to a double-precision floating point value.
    /// Returns 0 if the denominator is 0 to avoid division by zero.
    /// </summary>
    public double ToDouble() => Denominator == 0 ? 0 : (double)Numerator / Denominator;
}
