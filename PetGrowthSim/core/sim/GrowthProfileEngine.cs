using PetGrowthSim.Core.Config;

namespace PetGrowthSim.Core.Sim;

/// <summary>
/// growthProfile(정상/맥스 + 매핑 규칙)을 이용해
/// 개체의 성장률/피성/평균 증가치(공방순/체)를 "파생"해주는 엔진.
/// </summary>
public sealed class GrowthProfileEngine
{
    public sealed record DerivedGrowth(
        double Q,
        double GrowthRate,
        double Piseong,
        double AtkMean,
        double DefMean,
        double SpdMean,
        double HpMean
    );

    public DerivedGrowth Build(PetSpeciesConfig cfg, PetIndividual indiv)
    {
        var gp = cfg.GrowthProfile;
        var map = gp.Mapping;

        // 1) q 계산: sumAdj가 SumAdjForMax에 도달하면 q=1
        double sumAdj = indiv.AtkAdj + indiv.DefAdj + indiv.SpdAdj;
        double denom = map.SumAdjForMax <= 0 ? 1.0 : map.SumAdjForMax;

        double q = sumAdj / denom;
        q = Clamp(q, map.QClamp.Min, map.QClamp.Max);

        // 2) normal ↔ max 보간 (lerp)
        double growthRate = Lerp(gp.Normal.GrowthRate, gp.Max.GrowthRate, q);
        double piseong    = Lerp(gp.Normal.Piseong,    gp.Max.Piseong,    q);

        double atkMean = Lerp(gp.Normal.MeanIncrements.Atk, gp.Max.MeanIncrements.Atk, q);
        double defMean = Lerp(gp.Normal.MeanIncrements.Def, gp.Max.MeanIncrements.Def, q);
        double spdMean = Lerp(gp.Normal.MeanIncrements.Spd, gp.Max.MeanIncrements.Spd, q);

        // 3) perStatRedistribution: dev = statAdj - avgAdj, mean에 미세 반영
        if (map.PerStatRedistribution.Enabled)
        {
            double avgAdj = sumAdj / 3.0;
            double devAtk = indiv.AtkAdj - avgAdj;
            double devDef = indiv.DefAdj - avgAdj;
            double devSpd = indiv.SpdAdj - avgAdj;

            double k = map.PerStatRedistribution.K;
            atkMean += k * devAtk;
            defMean += k * devDef;
            spdMean += k * devSpd;
        }

        // 4) hpMean: base=piseong(보간된 값) + hpAdj * hpAdjK
        double hpMean = piseong;
        if (map.HpMean.Enabled)
        {
            hpMean = piseong + (indiv.HpAdj * map.HpMean.HpAdjK);
        }

        // 선택: 말도 안 되는 값 방지용 클램프(원하면 제거 가능)
        // 공/방/순은 0~3 사이 평균이 자연스럽지만, 종마다 다를 수 있어 여긴 최소 보호만 둠.
        atkMean = Clamp(atkMean, -10, 10);
        defMean = Clamp(defMean, -10, 10);
        spdMean = Clamp(spdMean, -10, 10);
        
        if (map.HpMean.Enabled)
        {
            hpMean = piseong + (indiv.HpAdj * map.HpMean.HpAdjK);

            // NEW: hpMean clamp (optional)
            if (map.HpMean.Clamp != null)
                hpMean = Clamp(hpMean, map.HpMean.Clamp.Min, map.HpMean.Clamp.Max);
        }

        return new DerivedGrowth(q, growthRate, piseong, atkMean, defMean, spdMean, hpMean);
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    private static double Clamp(double x, double min, double max) => Math.Max(min, Math.Min(max, x));
}
