using PetGrowthSim.Core.Config;
using PetGrowthSim.Core.Sim;

internal class Program
{
    private static void Main(string[] args)
    {
        var baseDir = AppContext.BaseDirectory;
        var petsDir = Path.Combine(baseDir, "Data", "pets");

        // 매 실행마다 랜덤 시드
        IRng rng = new XorShift32Rng((uint)Random.Shared.Next());

        // 1) petId 선택 (args[0] 우선)
        string petId = args.Length >= 1 ? args[0].Trim().ToLowerInvariant() : "";

        if (string.IsNullOrEmpty(petId))
        {
            Console.WriteLine("펫 선택:");
            Console.WriteLine("  1) mogaros");
            Console.WriteLine("  2) orgon");
            Console.Write("입력 (1/2 또는 petId 직접입력): ");
            var sel = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();

            petId = sel switch
            {
                "1" => "mogaros",
                "2" => "orgon",
                _ => sel
            };

            if (string.IsNullOrEmpty(petId))
                petId = "mogaros";
        }

        var jsonPath = Path.Combine(petsDir, $"{petId}.json");
        if (!File.Exists(jsonPath))
        {
            Console.WriteLine($"[ERROR] 펫 파일이 없습니다: {jsonPath}");
            Console.WriteLine("Data/pets 폴더에 mogaros.json / orgon.json 이 있는지 확인하세요.");
            return;
        }

        var repo = new SpeciesRepository();
        var cfg = repo.LoadFromFile(jsonPath);

        // 2) mode 선택 (args[1] 우선)
        string mode = args.Length >= 2 ? args[1].Trim().ToLowerInvariant() : "";

        if (string.IsNullOrEmpty(mode))
        {
            Console.WriteLine();
            Console.WriteLine($"선택된 펫: {cfg.Name} ({cfg.PetId})");
            Console.WriteLine("모드 선택:");
            Console.WriteLine("  1) 120까지 한 번에 (10단위 스냅샷)");
            Console.WriteLine("  2) 1업씩 진행");
            Console.WriteLine("  3) 1000마리 시뮬 (120까지 키운 통계)");
            Console.Write("입력 (1/2/3): ");
            var sel = (Console.ReadLine() ?? "").Trim();

            mode = sel switch
            {
                "1" => "bulk120",
                "2" => "step",
                "3" => "batch1000",
                _ => "bulk120"
            };
        }

        Console.WriteLine();
        Console.WriteLine($"[RUN] petId={cfg.PetId}, mode={mode}");
        Console.WriteLine();

        switch (mode)
        {
            case "bulk120":
            case "120":
                RunBulk120(cfg, rng);
                break;

            case "step":
            case "one":
                RunStepByStep(cfg, rng);
                break;

            case "batch1000":
            case "1000":
            case "batch":
                RunBatch(cfg, rng, count: 1000, levelUps: 120);
                break;

            default:
                Console.WriteLine($"알 수 없는 모드: {mode}");
                Console.WriteLine("사용 가능: bulk120 | step | batch1000");
                break;
        }
    }

    // =========================
    // 1) 120까지 한 번에 (10단위 스냅샷)
    // =========================
    private static void RunBulk120(PetSpeciesConfig cfg, IRng rng)
    {
        var gen = new InitialStatsGenerator();
        var sim = new GrowthSimulator();

        var pet = gen.CreateIndividual(cfg, rng);
        Console.WriteLine(pet);
        Console.WriteLine();

        var result = sim.SimulateLevels(cfg, pet, rng, levelUps: 120);

        // 실시간 성장치(공성/방성/순성/총성/체성) 계산해서 10단위 출력
        PrintSnapshotsEvery10(result.Individual, result.Deltas, maxLevelUps: 120);
    }

    // =========================
    // 2) 1업씩 진행
    // =========================
    private static void RunStepByStep(PetSpeciesConfig cfg, IRng rng)
    {
        var gen = new InitialStatsGenerator();
        var engine = new GrowthProfileEngine();

        // 규칙 준비(한 번만)
        var rule = cfg.GrowthRules.LevelUpIncrements;
        var atkAllowed = DiscreteMeanSampler.BuildAllowedIntRange(rule.Atk.Min, rule.Atk.Max, rule.Atk.Disallow);
        var defAllowed = DiscreteMeanSampler.BuildAllowedIntRange(rule.Def.Min, rule.Def.Max, rule.Def.Disallow);
        var spdAllowed = DiscreteMeanSampler.BuildAllowedIntRange(rule.Spd.Min, rule.Spd.Max, rule.Spd.Disallow);
        var hpAllowed  = DiscreteMeanSampler.BuildAllowedIntRange(rule.Hp.Min,  rule.Hp.Max,  null);

        // 현재 개체/상태 (리롤로 갱신)
        PetIndividual pet = null!;
        GrowthProfileEngine.DerivedGrowth derived = null!;

        int baseAtk = 0, baseDef = 0, baseSpd = 0, baseHp = 0;
        int curAtk = 0, curDef = 0, curSpd = 0, curHp = 0;
        int levelUpsDone = 0;

        void Reroll()
        {
            pet = gen.CreateIndividual(cfg, rng);
            derived = engine.Build(cfg, pet);

            baseAtk = pet.Atk;
            baseDef = pet.Def;
            baseSpd = pet.Spd;
            baseHp  = pet.Hp;

            curAtk = baseAtk;
            curDef = baseDef;
            curSpd = baseSpd;
            curHp  = baseHp;

            levelUpsDone = 0;

            Console.WriteLine();
            Console.WriteLine("=== 초기치 리롤 완료 ===");
            Console.WriteLine(pet);
            Console.WriteLine();
            Console.WriteLine("1업씩 진행: 엔터=1업 / 숫자=폭업 / r=초기치 리롤 / q=종료");
            Console.WriteLine("현재레벨\t공격력\t방어력\t순발력\t체력\t공성\t방성\t순성\t총성\t체성\t(증가치)");
            Console.WriteLine("--------------------------------------------------------------------------------------------------");
        }

        // 첫 생성
        Reroll();

        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine();
            if (input == null) continue;

            var trimmed = input.Trim();

            if (string.Equals(trimmed, "q", StringComparison.OrdinalIgnoreCase))
                break;

            if (string.Equals(trimmed, "r", StringComparison.OrdinalIgnoreCase))
            {
                Reroll();
                continue;
            }

            int burst;
            if (trimmed.Length == 0)
            {
                burst = 1; // Enter
            }
            else if (!int.TryParse(trimmed, out burst) || burst <= 0)
            {
                Console.WriteLine("입력 예: (엔터)=1업, 10=10업, r=리롤, q=종료");
                continue;
            }

            if (burst > 5000)
            {
                Console.WriteLine("폭업 값이 너무 큽니다. (최대 5000)");
                continue;
            }

            for (int j = 0; j < burst; j++)
            {
                int dAtk = DiscreteMeanSampler.SampleFromMean(rng, atkAllowed, derived.AtkMean);
                int dDef = DiscreteMeanSampler.SampleFromMean(rng, defAllowed, derived.DefMean);
                int dSpd = DiscreteMeanSampler.SampleFromMean(rng, spdAllowed, derived.SpdMean);
                int dHp  = DiscreteMeanSampler.SampleFromMean(rng, hpAllowed,  derived.HpMean);

                curAtk += dAtk;
                curDef += dDef;
                curSpd += dSpd;
                curHp  += dHp;

                levelUpsDone++;
                int currentLevel = 1 + levelUpsDone;

                double denom = levelUpsDone; // (현재레벨 - 1)
                double atkG = (curAtk - baseAtk) / denom;
                double defG = (curDef - baseDef) / denom;
                double spdG = (curSpd - baseSpd) / denom;
                double hpG  = (curHp  - baseHp)  / denom;
                double totalG = atkG + defG + spdG;

                Console.WriteLine(
                    $"{currentLevel}\t\t{curAtk}\t{curDef}\t{curSpd}\t{curHp}\t" +
                    $"{atkG:0.00}\t{defG:0.00}\t{spdG:0.00}\t{totalG:0.00}\t{hpG:0.00}\t" +
                    $"(공{dAtk},방{dDef},순{dSpd},체{dHp})"
                );
            }
        }
    }

    // =========================
    // 3) 1000마리 배치 시뮬 (120까지 키운 결과 통계)
    // - 공성/방성/순성/총성/체성은 120레벨 시점의 평균 성장치로 집계
    // =========================
    private static void RunBatch(PetSpeciesConfig cfg, IRng rng, int count, int levelUps)
    {
        var gen = new InitialStatsGenerator();
        var sim = new GrowthSimulator();

        long sumAtk = 0, sumDef = 0, sumSpd = 0, sumHp = 0;
        int minAtk = int.MaxValue, minDef = int.MaxValue, minSpd = int.MaxValue, minHp = int.MaxValue;
        int maxAtk = int.MinValue, maxDef = int.MinValue, maxSpd = int.MinValue, maxHp = int.MinValue;

        int initAtk12 = 0, initAtk13 = 0, initAtk14 = 0, initAtkOther = 0;

        // 성장치(avg/min/max) 집계
        double sumAtkG = 0, sumDefG = 0, sumSpdG = 0, sumHpG = 0, sumTotalG = 0;

        double minAtkG = double.PositiveInfinity, minDefG = double.PositiveInfinity, minSpdG = double.PositiveInfinity, minHpG = double.PositiveInfinity, minTotalG = double.PositiveInfinity;
        double maxAtkG = double.NegativeInfinity, maxDefG = double.NegativeInfinity, maxSpdG = double.NegativeInfinity, maxHpG = double.NegativeInfinity, maxTotalG = double.NegativeInfinity;

        for (int i = 0; i < count; i++)
        {
            var pet = gen.CreateIndividual(cfg, rng);

            if (pet.Atk == 12) initAtk12++;
            else if (pet.Atk == 13) initAtk13++;
            else if (pet.Atk == 14) initAtk14++;
            else initAtkOther++;

            var r = sim.SimulateLevels(cfg, pet, rng, levelUps);

            // 최종 스탯 집계
            sumAtk += r.EndAtk; sumDef += r.EndDef; sumSpd += r.EndSpd; sumHp += r.EndHp;

            minAtk = Math.Min(minAtk, r.EndAtk); maxAtk = Math.Max(maxAtk, r.EndAtk);
            minDef = Math.Min(minDef, r.EndDef); maxDef = Math.Max(maxDef, r.EndDef);
            minSpd = Math.Min(minSpd, r.EndSpd); maxSpd = Math.Max(maxSpd, r.EndSpd);
            minHp  = Math.Min(minHp,  r.EndHp);  maxHp  = Math.Max(maxHp,  r.EndHp);

            // ===== 120레벨 시점 성장치(요청식) =====
            // 레벨1 시작, levelUps번 올렸으면 현재레벨=1+levelUps
            double denom = levelUps; // (현재레벨-1)

            double atkG = (r.EndAtk - r.Individual.Atk) / denom;
            double defG = (r.EndDef - r.Individual.Def) / denom;
            double spdG = (r.EndSpd - r.Individual.Spd) / denom;
            double hpG  = (r.EndHp  - r.Individual.Hp)  / denom;
            double totalG = atkG + defG + spdG;

            sumAtkG += atkG; sumDefG += defG; sumSpdG += spdG; sumHpG += hpG; sumTotalG += totalG;

            minAtkG = Math.Min(minAtkG, atkG); maxAtkG = Math.Max(maxAtkG, atkG);
            minDefG = Math.Min(minDefG, defG); maxDefG = Math.Max(maxDefG, defG);
            minSpdG = Math.Min(minSpdG, spdG); maxSpdG = Math.Max(maxSpdG, spdG);
            minHpG  = Math.Min(minHpG,  hpG);  maxHpG  = Math.Max(maxHpG,  hpG);
            minTotalG = Math.Min(minTotalG, totalG); maxTotalG = Math.Max(maxTotalG, totalG);
        }

        Console.WriteLine($"[배치 결과] {cfg.Name}({cfg.PetId})  count={count}, levelUps={levelUps}");
        Console.WriteLine();

        Console.WriteLine("[초기 공격력(표기) 분포]");
        Console.WriteLine($"  공12: {initAtk12}");
        Console.WriteLine($"  공13: {initAtk13}");
        Console.WriteLine($"  공14: {initAtk14}");
        if (initAtkOther > 0) Console.WriteLine($"  기타: {initAtkOther}");
        Console.WriteLine();

        Console.WriteLine("[최종 스탯 요약]");
        Console.WriteLine($"  공격력: avg={(double)sumAtk / count:0.00}  min={minAtk}  max={maxAtk}");
        Console.WriteLine($"  방어력: avg={(double)sumDef / count:0.00}  min={minDef}  max={maxDef}");
        Console.WriteLine($"  순발력: avg={(double)sumSpd / count:0.00}  min={minSpd}  max={maxSpd}");
        Console.WriteLine($"  체력  : avg={(double)sumHp  / count:0.00}  min={minHp}   max={maxHp}");
        Console.WriteLine();

        Console.WriteLine($"[120레벨 시점 성장치(요청식) avg / min / max]  (현재레벨={1 + levelUps})");
        Console.WriteLine($"  공성: avg={sumAtkG / count:0.00}  min={minAtkG:0.00}  max={maxAtkG:0.00}");
        Console.WriteLine($"  방성: avg={sumDefG / count:0.00}  min={minDefG:0.00}  max={maxDefG:0.00}");
        Console.WriteLine($"  순성: avg={sumSpdG / count:0.00}  min={minSpdG:0.00}  max={maxSpdG:0.00}");
        Console.WriteLine($"  총성: avg={sumTotalG / count:0.00}  min={minTotalG:0.00}  max={maxTotalG:0.00}");
        Console.WriteLine($"  체성: avg={sumHpG / count:0.00}  min={minHpG:0.00}  max={maxHpG:0.00}");
    }

    // =========================
    // 10단위 스냅샷 출력 (요청식: (현재-초기)/(현재레벨-1))
    // =========================
    private static void PrintSnapshotsEvery10(
        PetIndividual basePet,
        IReadOnlyList<GrowthSimulator.LevelUpDelta> deltas,
        int maxLevelUps)
    {
        int baseAtk = basePet.Atk;
        int baseDef = basePet.Def;
        int baseSpd = basePet.Spd;
        int baseHp  = basePet.Hp;

        int curAtk = baseAtk;
        int curDef = baseDef;
        int curSpd = baseSpd;
        int curHp  = baseHp;

        Console.WriteLine("현재레벨\t공격력\t방어력\t순발력\t체력\t공성\t방성\t순성\t총성\t체성");
        Console.WriteLine("--------------------------------------------------------------------------------");

        for (int i = 1; i <= maxLevelUps; i++)
        {
            var d = deltas[i - 1];
            curAtk += d.Atk;
            curDef += d.Def;
            curSpd += d.Spd;
            curHp  += d.Hp;

            if (i % 10 == 0)
            {
                // 레벨1 시작 가정: 현재레벨 = 1 + i
                int currentLevel = 1 + i;
                double denom = currentLevel - 1; // == i

                double atkG = (curAtk - baseAtk) / denom;
                double defG = (curDef - baseDef) / denom;
                double spdG = (curSpd - baseSpd) / denom;
                double hpG  = (curHp  - baseHp)  / denom;
                double totalG = atkG + defG + spdG;

                Console.WriteLine(
                    $"{currentLevel}\t\t{curAtk}\t{curDef}\t{curSpd}\t{curHp}\t" +
                    $"{atkG:0.00}\t{defG:0.00}\t{spdG:0.00}\t{totalG:0.00}\t{hpG:0.00}"
                );
            }
        }
    }
}
