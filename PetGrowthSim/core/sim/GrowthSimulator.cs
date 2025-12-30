using PetGrowthSim.Core.Config;

namespace PetGrowthSim.Core.Sim;

public sealed class GrowthSimulator
{
    public sealed record LevelUpDelta(int Atk, int Def, int Spd, int Hp);

    public sealed record SimResult(
        PetIndividual Individual,
        GrowthProfileEngine.DerivedGrowth Derived,
        IReadOnlyList<LevelUpDelta> Deltas,
        int EndAtk,
        int EndDef,
        int EndSpd,
        int EndHp
    );

    private readonly GrowthProfileEngine _engine = new();

    public SimResult SimulateLevels(PetSpeciesConfig cfg, PetIndividual indiv, IRng rng, int levelUps)
    {
        if (levelUps < 0) throw new ArgumentOutOfRangeException(nameof(levelUps));

        // 1) 개체 파생 성장(성장률/피성/평균 증가치) 계산
        var derived = _engine.Build(cfg, indiv);

        // 2) Individual에 성장률/피성 기록(출력 편의)
        var indivWithGrowth = indiv.WithGrowth(derived.GrowthRate, derived.Piseong);

        // 3) 종 제약 조건 준비
        var rule = cfg.GrowthRules.LevelUpIncrements;

        // 공/방/순: 기본 0~3, disallow 반영
        // (JSON에 min/max를 넣었다면 rule.Atk.Min/Max를 쓰고 싶겠지만,
        //  일단 mogaros 기준 0~3 고정으로 사용)
        int atkMin = rule.Atk.Min;
        int atkMax = rule.Atk.Max;
        int defMin = rule.Def.Min;
        int defMax = rule.Def.Max;
        int spdMin = rule.Spd.Min;
        int spdMax = rule.Spd.Max;

        var atkAllowed = DiscreteMeanSampler.BuildAllowedIntRange(atkMin, atkMax, rule.Atk.Disallow);
        var defAllowed = DiscreteMeanSampler.BuildAllowedIntRange(defMin, defMax, rule.Def.Disallow);
        var spdAllowed = DiscreteMeanSampler.BuildAllowedIntRange(spdMin, spdMax, rule.Spd.Disallow);

        // hp: min..max (모가로스: 8..11)
        var hpAllowed = DiscreteMeanSampler.BuildAllowedIntRange(rule.Hp.Min, rule.Hp.Max, null);

        if (atkAllowed.Count == 0) throw new InvalidOperationException("No allowed atk increments (check disallow/min/max).");
        if (defAllowed.Count == 0) throw new InvalidOperationException("No allowed def increments (check disallow/min/max).");
        if (spdAllowed.Count == 0) throw new InvalidOperationException("No allowed spd increments (check disallow/min/max).");
        if (hpAllowed.Count == 0)  throw new InvalidOperationException("No allowed hp increments (check min/max).");

        // 4) 시뮬레이션
        int curAtk = indivWithGrowth.Atk;
        int curDef = indivWithGrowth.Def;
        int curSpd = indivWithGrowth.Spd;
        int curHp  = indivWithGrowth.Hp;

        var deltas = new List<LevelUpDelta>(levelUps);

        for (int i = 0; i < levelUps; i++)
        {
            // "목표 평균" 기반 샘플링
            int dAtk = DiscreteMeanSampler.SampleFromMean(rng, atkAllowed, derived.AtkMean);
            int dDef = DiscreteMeanSampler.SampleFromMean(rng, defAllowed, derived.DefMean);
            int dSpd = DiscreteMeanSampler.SampleFromMean(rng, spdAllowed, derived.SpdMean);
            int dHp  = DiscreteMeanSampler.SampleFromMean(rng, hpAllowed,  derived.HpMean);

            curAtk += dAtk;
            curDef += dDef;
            curSpd += dSpd;
            curHp  += dHp;

            deltas.Add(new LevelUpDelta(dAtk, dDef, dSpd, dHp));
        }

        return new SimResult(
            indivWithGrowth,
            derived,
            deltas,
            curAtk,
            curDef,
            curSpd,
            curHp
        );
    }
}

/// <summary>
/// PetIndividual이 class인 상태에서 "with" 느낌으로 값 일부만 바꾼 새 객체를 만드는 확장.
/// </summary>
public static class PetIndividualExtensions
{
    public static PetIndividual WithGrowth(this PetIndividual p, double growthRate, double piseong)
    {
        return new PetIndividual
        {
            PetId = p.PetId,
            Name = p.Name,

            AtkRaw = p.AtkRaw,
            DefRaw = p.DefRaw,
            SpdRaw = p.SpdRaw,
            HpRaw = p.HpRaw,

            Atk = p.Atk,
            Def = p.Def,
            Spd = p.Spd,
            Hp = p.Hp,

            AtkAdj = p.AtkAdj,
            DefAdj = p.DefAdj,
            SpdAdj = p.SpdAdj,
            HpAdj = p.HpAdj,

            GrowthRate = growthRate,
            Piseong = piseong
        };
    }
}
