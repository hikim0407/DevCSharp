namespace PetGrowthSim.Core.Sim;

/// <summary>
/// 한 “개체”를 표현.
/// - Raw: 보정치가 더해진 내부값(공/방/순은 소수 1자리 가능)
/// - Display: 반올림/클램프된 표기값
/// - Adj: 보정치(유전자)
/// - GrowthRate/Piseong: growthProfile에 의해 파생된 개체 성장 파라미터
/// </summary>
public sealed class PetIndividual
{
    public required string PetId { get; init; }
    public required string Name { get; init; }

    // ===== Internal raw stats (with decimal adjustments) =====
    public required double AtkRaw { get; init; }
    public required double DefRaw { get; init; }
    public required double SpdRaw { get; init; }
    public required int HpRaw { get; init; }

    // ===== Display stats (rounded/clamped) =====
    public required int Atk { get; init; }
    public required int Def { get; init; }
    public required int Spd { get; init; }
    public required int Hp { get; init; }

    // ===== Individual adjustments (genes) =====
    public required double AtkAdj { get; init; }
    public required double DefAdj { get; init; }
    public required double SpdAdj { get; init; }
    public required int HpAdj { get; init; }

    // ===== Derived growth params (from growthProfile) =====
    public double GrowthRate { get; init; }  // e.g., 5.00 ~ 5.30
    public double Piseong { get; init; }     // e.g., 9.6 ~ 9.9

    public override string ToString()
        => $"{Name}({PetId})  " +
           $"[표기] 공:{Atk} 방:{Def} 순:{Spd} 체:{Hp}  " +
           $"[성장] 성장률:{GrowthRate:0.00} 피성:{Piseong:0.0}  " +
           $"[보정] 공:{Fmt1(AtkAdj)} 방:{Fmt1(DefAdj)} 순:{Fmt1(SpdAdj)} 체:{FmtI(HpAdj)}";

    private static string Fmt1(double v) => v.ToString("+0.0;-0.0;0.0");
    private static string FmtI(int v) => v.ToString("+0;-0;0");
}
