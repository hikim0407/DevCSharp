using System.Text.Json.Serialization;

namespace PetGrowthSim.Core.Config;

/// <summary>
/// Species (pet kind) configuration loaded from JSON.
/// - IMPORTANT: nested type names are suffixed with "Config" to avoid C# name collisions
///   with same-named properties (e.g., Baseline vs BaselineConfig).
/// </summary>
public sealed class PetSpeciesConfig
{
    [JsonPropertyName("schemaVersion")] public string SchemaVersion { get; init; } = "1.0";
    [JsonPropertyName("petId")] public string PetId { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";

    // ========== Baseline ==========
    [JsonPropertyName("baseline")] public BaselineConfig Baseline { get; init; } = new();

    public sealed class BaselineConfig
    {
        [JsonPropertyName("stats")] public StatsConfig Stats { get; init; } = new();
        [JsonPropertyName("growthRate")] public double GrowthRate { get; init; }
        [JsonPropertyName("piseong")] public double Piseong { get; init; }
    }

    public sealed class StatsConfig
    {
        [JsonPropertyName("atk")] public int Atk { get; init; }
        [JsonPropertyName("def")] public int Def { get; init; }
        [JsonPropertyName("spd")] public int Spd { get; init; }
        [JsonPropertyName("hp")]  public int Hp  { get; init; }
    }

    // ========== Initial Stat Generation ==========
    [JsonPropertyName("initialStatGeneration")]
    public InitialStatGenerationConfig InitialStatGeneration { get; init; } = new();

    public sealed class InitialStatGenerationConfig
    {
        [JsonPropertyName("adjustment")] public AdjustmentConfig Adjustment { get; init; } = new();
        [JsonPropertyName("rounding")] public RoundingConfig Rounding { get; init; } = new();
        [JsonPropertyName("clampAfterAdjustment")] public ClampAfterAdjustmentConfig ClampAfterAdjustment { get; init; } = new();
    }

    public sealed class AdjustmentConfig
    {
        [JsonPropertyName("precision")] public PrecisionConfig Precision { get; init; } = new();
        [JsonPropertyName("range")] public AdjustmentRangeConfig Range { get; init; } = new();
        [JsonPropertyName("globalCap")] public GlobalCapConfig GlobalCap { get; init; } = new();

        // ✅ NEW: correlation (optional)
        [JsonPropertyName("correlation")]
        public CorrelationConfig? Correlation { get; init; }
    }

    public sealed class CorrelationConfig
    {
        [JsonPropertyName("mode")] public string Mode { get; init; } = "";

        [JsonPropertyName("attackGate")]
        public AttackGateConfig? AttackGate { get; init; }
    }

    public sealed class AttackGateConfig
    {
        [JsonPropertyName("tiers")]
        public List<AttackGateTierConfig> Tiers { get; init; } = new();
    }

    public sealed class AttackGateTierConfig
    {
        [JsonPropertyName("atkAdjMinInclusive")] public double AtkAdjMinInclusive { get; init; }
        [JsonPropertyName("atkAdjMaxExclusive")] public double AtkAdjMaxExclusive { get; init; }

        [JsonPropertyName("otherAdjMax")]
        public OtherAdjMaxConfig OtherAdjMax { get; init; } = new();
    }

    public sealed class OtherAdjMaxConfig
    {
        [JsonPropertyName("def")] public double Def { get; init; }
        [JsonPropertyName("spd")] public double Spd { get; init; }
    }

    public sealed class PrecisionConfig
    {
        [JsonPropertyName("atk_def_spd_step")] public double AtkDefSpdStep { get; init; } = 0.1;
        [JsonPropertyName("hp_step")] public int HpStep { get; init; } = 1;
    }

    public sealed class AdjustmentRangeConfig
    {
        [JsonPropertyName("atk")] public DoubleRangeConfig Atk { get; init; } = new();
        [JsonPropertyName("def")] public DoubleRangeConfig Def { get; init; } = new();
        [JsonPropertyName("spd")] public DoubleRangeConfig Spd { get; init; } = new();
        [JsonPropertyName("hp")]  public IntRangeConfig Hp  { get; init; } = new();
    }

    public sealed class DoubleRangeConfig
    {
        [JsonPropertyName("min")] public double Min { get; init; }
        [JsonPropertyName("max")] public double Max { get; init; }
    }

    public sealed class IntRangeConfig
    {
        [JsonPropertyName("min")] public int Min { get; init; }
        [JsonPropertyName("max")] public int Max { get; init; }
    }

    public sealed class GlobalCapConfig
    {
        [JsonPropertyName("atk_def_spd_maxAbs")] public double AtkDefSpdMaxAbs { get; init; } = 2.0;
        // NEW: sum cap (abs)
        [JsonPropertyName("atk_def_spd_sumMaxAbs")] public double AtkDefSpdSumMaxAbs { get; init; } = 0.0; // 0이면 비활성
    }

    public sealed class RoundingConfig
    {
        [JsonPropertyName("atk_def_spd_display")] public string AtkDefSpdDisplay { get; init; } = "ROUND_HALF_UP";
        [JsonPropertyName("hp_display")] public string HpDisplay { get; init; } = "INTEGER";
    }

    public sealed class ClampAfterAdjustmentConfig
    {
        [JsonPropertyName("atk")] public IntRangeConfig Atk { get; init; } = new();
        [JsonPropertyName("def")] public IntRangeConfig Def { get; init; } = new();
        [JsonPropertyName("spd")] public IntRangeConfig Spd { get; init; } = new();
        [JsonPropertyName("hp")]  public IntRangeConfig Hp  { get; init; } = new();
    }

    // ========== Growth Profile (normal/max + mapping) ==========
    [JsonPropertyName("growthProfile")]
    public GrowthProfileConfig GrowthProfile { get; init; } = new();

    public sealed class GrowthProfileConfig
    {
        [JsonPropertyName("normal")] public GrowthTierConfig Normal { get; init; } = new();
        [JsonPropertyName("max")]    public GrowthTierConfig Max { get; init; } = new();
        [JsonPropertyName("mapping")] public GrowthMappingConfig Mapping { get; init; } = new();
    }

    public sealed class GrowthTierConfig
    {
        [JsonPropertyName("growthRate")] public double GrowthRate { get; init; }
        [JsonPropertyName("piseong")] public double Piseong { get; init; }

        [JsonPropertyName("meanIncrements")]
        public MeanIncrementsConfig MeanIncrements { get; init; } = new();
    }

    public sealed class MeanIncrementsConfig
    {
        [JsonPropertyName("atk")] public double Atk { get; init; }
        [JsonPropertyName("def")] public double Def { get; init; }
        [JsonPropertyName("spd")] public double Spd { get; init; }
    }

    public sealed class GrowthMappingConfig
    {
        [JsonPropertyName("sumAdjForMax")] public double SumAdjForMax { get; init; } = 1.8;

        [JsonPropertyName("qClamp")] public QClampConfig QClamp { get; init; } = new();

        [JsonPropertyName("perStatRedistribution")]
        public PerStatRedistributionConfig PerStatRedistribution { get; init; } = new();

        [JsonPropertyName("hpMean")]
        public HpMeanConfig HpMean { get; init; } = new();
    }

    public sealed class QClampConfig
    {
        [JsonPropertyName("min")] public double Min { get; init; } = 0.0;
        [JsonPropertyName("max")] public double Max { get; init; } = 1.0;
    }

    public sealed class PerStatRedistributionConfig
    {
        [JsonPropertyName("enabled")] public bool Enabled { get; init; } = true;
        [JsonPropertyName("k")] public double K { get; init; } = 0.05;
    }

    public sealed class HpMeanConfig
    {
        [JsonPropertyName("enabled")] public bool Enabled { get; init; } = true;
        [JsonPropertyName("hpAdjK")] public double HpAdjK { get; init; } = 0.12;
        // NEW
        [JsonPropertyName("clamp")] public DoubleClampConfig? Clamp { get; init; }
    }

    public sealed class DoubleClampConfig
    {
        [JsonPropertyName("min")] public double Min { get; init; } = double.NegativeInfinity;
        [JsonPropertyName("max")] public double Max { get; init; } = double.PositiveInfinity;
    }

    // ========== Growth Rules (hard constraints) ==========
    [JsonPropertyName("growthRules")] public GrowthRulesConfig GrowthRules { get; init; } = new();

    public sealed class GrowthRulesConfig
    {
        [JsonPropertyName("levelUpIncrements")] public LevelUpIncrementsConfig LevelUpIncrements { get; init; } = new();
    }

    public sealed class LevelUpIncrementsConfig
    {
        [JsonPropertyName("atk")] public StatIncrementRuleConfig Atk { get; init; } = new();
        [JsonPropertyName("def")] public StatIncrementRuleConfig Def { get; init; } = new();
        [JsonPropertyName("spd")] public StatIncrementRuleConfig Spd { get; init; } = new();
        [JsonPropertyName("hp")]  public HpIncrementRuleConfig Hp  { get; init; } = new();
    }

    /// <summary>
    /// For atk/def/spd: optional min/max plus disallowed values.
    /// If min/max are missing in JSON, they default to 0..3 (per your project convention).
    /// </summary>
    public sealed class StatIncrementRuleConfig
    {
        [JsonPropertyName("min")] public int Min { get; init; } = 0;
        [JsonPropertyName("max")] public int Max { get; init; } = 3;

        [JsonPropertyName("disallow")] public int[] Disallow { get; init; } = Array.Empty<int>();
    }

    /// <summary>
    /// For hp: min/max are required (e.g., 8..11).
    /// </summary>
    public sealed class HpIncrementRuleConfig
    {
        [JsonPropertyName("min")] public int Min { get; init; }
        [JsonPropertyName("max")] public int Max { get; init; }
    }
}
