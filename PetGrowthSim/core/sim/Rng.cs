namespace PetGrowthSim.Core.Sim;

public interface IRng
{
    double NextDouble();                 // [0,1)
    int NextInt(int minInclusive, int maxInclusive);
}

public sealed class XorShift32Rng : IRng
{
    private uint _x;
    public XorShift32Rng(uint seed) => _x = seed == 0 ? 2463534242u : seed;

    public double NextDouble()
    {
        uint t = _x;
        t ^= t << 13;
        t ^= t >> 17;
        t ^= t << 5;
        _x = t;
        return (_x / (double)uint.MaxValue); // ~[0,1]
    }

    public int NextInt(int minInclusive, int maxInclusive)
    {
        if (minInclusive > maxInclusive)
            throw new ArgumentException("minInclusive > maxInclusive");
        var r = NextDouble();
        var span = (long)maxInclusive - minInclusive + 1;
        var v = (long)(r * span) + minInclusive;
        if (v > maxInclusive) v = maxInclusive;
        return (int)v;
    }
}

public static class MathUtil
{
    // 네가 말한 방식: 12.7->13, 7.3->7, 12.5->13
    // (음수까지 정의하면 -1.5 -> -1 같은 half-up)
    public static int RoundHalfUp(double x)
        => x >= 0 ? (int)Math.Floor(x + 0.5) : -(int)Math.Floor(Math.Abs(x) + 0.5);

    public static int Clamp(int x, int min, int max) => Math.Max(min, Math.Min(max, x));
    public static double Clamp(double x, double min, double max) => Math.Max(min, Math.Min(max, x));
}
