using System.Runtime.InteropServices;

namespace Loxifi.FFmpeg.Native.Types;

[StructLayout(LayoutKind.Sequential)]
public struct AVRational
{
    public int Numerator;
    public int Denominator;

    public AVRational(int num, int den)
    {
        Numerator = num;
        Denominator = den;
    }

    public double ToDouble() => Denominator == 0 ? 0 : (double)Numerator / Denominator;
}
