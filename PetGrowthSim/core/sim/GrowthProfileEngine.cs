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

        // 1) q 계산: sumAdj가 SumAdjForMax에 도달하면 q≈1, 음수도 허용(문턱 하향용)
        double sumAdj = indiv.AtkAdj + indiv.DefAdj + indiv.SpdAdj;
        double denom = map.SumAdjForMax <= 0 ? 1.0 : map.SumAdjForMax;

        double rawQ = sumAdj / denom;
        double q = Clamp(rawQ, map.QClamp.Min, map.QClamp.Max);

        // q를 양/음으로 분리
        double qPos = q > 0 ? q : 0; // 0..1
        double qNeg = q < 0 ? q : 0; // -1..0

        // 2) normal ↔ max 보간 (양수 구간만)
        double growthRate = Lerp(gp.Normal.GrowthRate, gp.Max.GrowthRate, qPos);
        double piseong    = Lerp(gp.Normal.Piseong,    gp.Max.Piseong,    qPos);

        double atkMean = Lerp(gp.Normal.MeanIncrements.Atk, gp.Max.MeanIncrements.Atk, qPos);
        double defMean = Lerp(gp.Normal.MeanIncrements.Def, gp.Max.MeanIncrements.Def, qPos);
        double spdMean = Lerp(gp.Normal.MeanIncrements.Spd, gp.Max.MeanIncrements.Spd, qPos);

        // 2.5) 음수 패널티(중요): bad 프로필 없이 normal 기반으로 깎기
        // qNeg=-1 일 때 (1 - K)배. qNeg=0이면 영향 없음.
        var pen = map.NegativePenalty;
        if (pen != null && pen.Enabled && qNeg < 0)
        {
            // 공/방/순 평균 감소
            double mulADS = 1.0 + (pen.AtkDefSpdPenaltyK * qNeg); // qNeg는 음수
            // 너무 과도한 하락 방지용 바닥(원하면 조절/제거)
            if (mulADS < 0.50) mulADS = 0.50;

            atkMean *= mulADS;
            defMean *= mulADS;
            spdMean *= mulADS;

            // 성장률/피성도 같은 배율로 깎으면 "태생이 안 좋으면 전반적으로 덜 큰다" 느낌이 맞음
            // 원치 않으면 아래 2줄 주석 처리 가능
            growthRate *= mulADS;
            piseong    *= mulADS;
        }

        // 3) perStatRedistribution: dev = statAdj - avgAdj, mean에 미세 반영
        // (패널티/보간 이후에 적용해야 dev 반영이 최종 mean에 살아남음)
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

        // 4) hpMean: base=piseong(보간/패널티 반영된 값) + hpAdj * hpAdjK
        double hpMean = piseong;
        if (map.HpMean.Enabled)
        {
            hpMean = piseong + (indiv.HpAdj * map.HpMean.HpAdjK);

            // hp도 음수 패널티 별도 적용 (공/방/순과 다른 K)
            if (pen != null && pen.Enabled && qNeg < 0)
            {
                double mulHp = 1.0 + (pen.HpPenaltyK * qNeg); // qNeg 음수
                if (mulHp < 0.70) mulHp = 0.70;
                hpMean *= mulHp;
            }

            // hpMean clamp (optional)
            if (map.HpMean.Clamp != null)
                hpMean = Clamp(hpMean, map.HpMean.Clamp.Min, map.HpMean.Clamp.Max);
        }

        // 선택: 말도 안 되는 값 방지용 클램프(원하면 제거 가능)
        // 공/방/순은 보통 0~3 사이 평균이 자연스럽지만, 종마다 다를 수 있어 여기선 최소 보호만 둠.
        atkMean = Clamp(atkMean, -10, 10);
        defMean = Clamp(defMean, -10, 10);
        spdMean = Clamp(spdMean, -10, 10);

        // piseong/growthRate는 음수로 떨어지면 말이 안되니 최소 0 보호
        piseong = Math.Max(0, piseong);
        growthRate = Math.Max(0, growthRate);

        return new DerivedGrowth(q, growthRate, piseong, atkMean, defMean, spdMean, hpMean);
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    private static double Clamp(double x, double min, double max) => Math.Max(min, Math.Min(max, x));
}
