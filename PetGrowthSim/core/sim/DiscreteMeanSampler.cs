namespace PetGrowthSim.Core.Sim;

public static class DiscreteMeanSampler
{
    /// <summary>
    /// allowedValues(정렬된 정수 목록)에서 목표 평균 targetMean에 맞게
    /// 인접한 두 값으로 확률을 구성해 샘플링한다.
    ///
    /// 예) allowed=[1,2,3], targetMean=2.2
    ///     -> lower=2, upper=3, P(3)=0.2, P(2)=0.8
    ///
    /// - targetMean은 allowedValues의 min/max로 클램프된다.
    /// - disallow는 allowedValues 생성 단계에서 제외되어 들어오는 것을 전제로 한다.
    /// </summary>
    public static int SampleFromMean(IRng rng, IReadOnlyList<int> allowedValues, double targetMean)
    {
        if (allowedValues.Count == 0)
            throw new ArgumentException("allowedValues is empty", nameof(allowedValues));

        if (allowedValues.Count == 1)
            return allowedValues[0];

        double min = allowedValues[0];
        double max = allowedValues[^1];
        double m = Clamp(targetMean, min, max);

        // lower: greatest <= m
        // upper: smallest >= m
        int lower = allowedValues[0];
        int upper = allowedValues[^1];

        for (int i = 0; i < allowedValues.Count; i++)
        {
            int v = allowedValues[i];
            if (v <= m) lower = v;
            if (v >= m) { upper = v; break; }
        }

        if (lower == upper)
            return lower;

        // upper 확률
        double t = (m - lower) / (upper - lower); // 0..1
        return rng.NextDouble() < t ? upper : lower;
    }

    /// <summary>
    /// min..max 범위에서 disallow를 제외한 allowed 리스트를 만든다.
    /// </summary>
    public static List<int> BuildAllowedIntRange(int minInclusive, int maxInclusive, int[]? disallow)
    {
        if (minInclusive > maxInclusive)
            throw new ArgumentException("minInclusive > maxInclusive");

        var blocked = new HashSet<int>(disallow ?? Array.Empty<int>());
        var list = new List<int>();

        for (int v = minInclusive; v <= maxInclusive; v++)
        {
            if (!blocked.Contains(v))
                list.Add(v);
        }

        return list;
    }

    private static double Clamp(double x, double min, double max)
        => Math.Max(min, Math.Min(max, x));
}
