using PetGrowthSim.Core.Config;

namespace PetGrowthSim.Core.Sim;

/// <summary>
/// SpeciesConfig(JSON) 기반으로 "개체 초기치(보정치/표기치)"를 생성.
/// - 공격 보정치(AtkAdj)를 먼저 뽑고, Attack Gating(상관관계) 규칙이 있으면
///   방/순 보정치의 상한을 AtkAdj 티어에 따라 제한한다.
/// - 이후 개별 abs cap / 합계(sum) cap을 적용한다(합계는 비율 유지 스케일다운).
/// </summary>
public sealed class InitialStatsGenerator
{
    public PetIndividual CreateIndividual(PetSpeciesConfig cfg, IRng rng)
    {
        var baseStats = cfg.Baseline.Stats;
        var genCfg = cfg.InitialStatGeneration;
        var adjCfg = genCfg.Adjustment;

        double step = adjCfg.Precision.AtkDefSpdStep;
        if (step <= 0) step = 0.1;

        // ===== 1) 보정치(유전자) 생성: Atk -> (Gate) -> Def/Spd =====
        double atkAdj = SampleStepDouble(rng, adjCfg.Range.Atk.Min, adjCfg.Range.Atk.Max, step);

        // Attack Gating 적용: atkAdj 값에 따라 def/spd의 max를 낮춤
        double defMax = adjCfg.Range.Def.Max;
        double spdMax = adjCfg.Range.Spd.Max;

        var tier = FindAttackGateTier(adjCfg, atkAdj);
        if (tier != null)
        {
            // "공이 높을 때만 다른 보정치들도 높게" 만들기 위해 positive max를 낮춤
            defMax = Math.Min(defMax, tier.OtherAdjMax.Def);
            spdMax = Math.Min(spdMax, tier.OtherAdjMax.Spd);
        }

        double defAdj = SampleStepDouble(rng, adjCfg.Range.Def.Min, defMax, step);
        double spdAdj = SampleStepDouble(rng, adjCfg.Range.Spd.Min, spdMax, step);

        int hpAdj = SampleStepInt(rng, adjCfg.Range.Hp.Min, adjCfg.Range.Hp.Max, adjCfg.Precision.HpStep);

        // ===== 2) 개별 abs cap 적용 (기존 globalCap.atk_def_spd_maxAbs) =====
        double absCap = adjCfg.GlobalCap.AtkDefSpdMaxAbs;
        if (absCap > 0)
        {
            atkAdj = Clamp(atkAdj, -absCap, absCap);
            defAdj = Clamp(defAdj, -absCap, absCap);
            spdAdj = Clamp(spdAdj, -absCap, absCap);
        }

        // ===== 3) 합계(sum) cap 적용 (비율 유지) (globalCap.atk_def_spd_sumMaxAbs) =====
        ApplySumCapProportional(
            ref atkAdj, ref defAdj, ref spdAdj,
            step: step,
            sumMaxAbs: adjCfg.GlobalCap.AtkDefSpdSumMaxAbs
        );

        // ===== 4) 내부 raw 스탯 계산 =====
        double atkRaw = baseStats.Atk + atkAdj;
        double defRaw = baseStats.Def + defAdj;
        double spdRaw = baseStats.Spd + spdAdj;
        int hpRaw = baseStats.Hp + hpAdj;

        // ===== 5) 표기 스탯(반올림/클램프) =====
        int atk = RoundHalfUpToInt(atkRaw);
        int def = RoundHalfUpToInt(defRaw);
        int spd = RoundHalfUpToInt(spdRaw);
        int hp = hpRaw; // hp는 정수

        // clampAfterAdjustment
        atk = Clamp(atk, genCfg.ClampAfterAdjustment.Atk.Min, genCfg.ClampAfterAdjustment.Atk.Max);
        def = Clamp(def, genCfg.ClampAfterAdjustment.Def.Min, genCfg.ClampAfterAdjustment.Def.Max);
        spd = Clamp(spd, genCfg.ClampAfterAdjustment.Spd.Min, genCfg.ClampAfterAdjustment.Spd.Max);
        hp  = Clamp(hp,  genCfg.ClampAfterAdjustment.Hp.Min,  genCfg.ClampAfterAdjustment.Hp.Max);

        // NOTE: GrowthRate/Piseong은 GrowthSimulator에서 Derived로 계산 후 WithGrowth로 채움.
        // 여기서는 0으로 두거나 baseline으로 두어도 됨. (일단 baseline으로 채움)
        return new PetIndividual
        {
            PetId = cfg.PetId,
            Name = cfg.Name,

            AtkRaw = atkRaw,
            DefRaw = defRaw,
            SpdRaw = spdRaw,
            HpRaw = hpRaw,

            Atk = atk,
            Def = def,
            Spd = spd,
            Hp = hp,

            AtkAdj = atkAdj,
            DefAdj = defAdj,
            SpdAdj = spdAdj,
            HpAdj = hpAdj,

            GrowthRate = cfg.Baseline.GrowthRate,
            Piseong = cfg.Baseline.Piseong
        };
    }

    // ===================== Attack Gating =====================

    private static PetSpeciesConfig.AttackGateTierConfig? FindAttackGateTier(
        PetSpeciesConfig.AdjustmentConfig adjCfg, double atkAdj)
    {
        var corr = adjCfg.Correlation;
        if (corr == null) return null;

        if (!string.Equals(corr.Mode, "ATTACK_GATING", StringComparison.OrdinalIgnoreCase))
            return null;

        var gate = corr.AttackGate;
        if (gate == null || gate.Tiers.Count == 0) return null;

        foreach (var t in gate.Tiers)
        {
            if (atkAdj >= t.AtkAdjMinInclusive && atkAdj < t.AtkAdjMaxExclusive)
                return t;
        }
        return null;
    }

    // ===================== Sampling Helpers =====================

    private static double SampleStepDouble(IRng rng, double min, double max, double step)
    {
        if (step <= 0) step = 0.1;

        if (max < min)
        {
            // 혹시 게이팅으로 max가 min보다 작아지면 min으로 고정
            max = min;
        }

        int count = (int)Math.Floor((max - min) / step) + 1;
        if (count <= 1) return FixNegZero(Round1(min));

        int idx = (int)Math.Floor(rng.NextDouble() * count);
        if (idx >= count) idx = count - 1;

        double v = min + idx * step;
        return FixNegZero(Round1(v));
    }

    private static int SampleStepInt(IRng rng, int min, int max, int step)
    {
        if (step <= 0) step = 1;
        if (max < min) max = min;

        int count = ((max - min) / step) + 1;
        if (count <= 1) return min;

        int idx = (int)Math.Floor(rng.NextDouble() * count);
        if (idx >= count) idx = count - 1;

        return min + idx * step;
    }

    // ===================== Caps =====================

    private static void ApplySumCapProportional(
        ref double atkAdj, ref double defAdj, ref double spdAdj,
        double step, double sumMaxAbs)
    {
        if (sumMaxAbs <= 0) return;

        double sum = atkAdj + defAdj + spdAdj;
        double absSum = Math.Abs(sum);
        if (absSum <= sumMaxAbs) return;

        double factor = sumMaxAbs / absSum;
        atkAdj *= factor;
        defAdj *= factor;
        spdAdj *= factor;

        atkAdj = Round1(atkAdj);
        defAdj = Round1(defAdj);
        spdAdj = Round1(spdAdj);

        // 반올림 때문에 cap을 살짝 넘는 경우: 가장 큰 항부터 step씩 0쪽으로 줄여 맞춤
        while (Math.Abs(atkAdj + defAdj + spdAdj) > sumMaxAbs + 1e-9)
        {
            if (Math.Abs(atkAdj) >= Math.Abs(defAdj) && Math.Abs(atkAdj) >= Math.Abs(spdAdj))
                atkAdj -= Math.Sign(atkAdj) * step;
            else if (Math.Abs(defAdj) >= Math.Abs(spdAdj))
                defAdj -= Math.Sign(defAdj) * step;
            else
                spdAdj -= Math.Sign(spdAdj) * step;

            atkAdj = FixNegZero(Round1(atkAdj));
            defAdj = FixNegZero(Round1(defAdj));
            spdAdj = FixNegZero(Round1(spdAdj));
        }
    }

    // ===================== Rounding/Clamp =====================

    private static int RoundHalfUpToInt(double x)
        => (int)Math.Round(x, 0, MidpointRounding.AwayFromZero);

    private static double Round1(double x)
        => Math.Round(x, 1, MidpointRounding.AwayFromZero);

    private static double FixNegZero(double x) => Math.Abs(x) < 1e-12 ? 0.0 : x;

    private static int Clamp(int v, int min, int max) => Math.Max(min, Math.Min(max, v));
    private static double Clamp(double v, double min, double max) => Math.Max(min, Math.Min(max, v));
}
