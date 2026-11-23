using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nicotine.Server;

public sealed class NicotineConfig
{
    public bool Enabled { get; set; } = true;
    public PackConfig Packs { get; set; } = new();
    public ReliefConfig Relief { get; set; } = new();
    public AddictionConfig Addiction { get; set; } = new();
    public WithdrawalConfig Withdrawal { get; set; } = new();
    public OverdoConfig Overdo { get; set; } = new();

    public Dictionary<string, List<BuffConfig>> BuffSets { get; set; } = DefaultBuffSets();

    private static BuffConfig Skill(string skill, double value, double seconds, double delay = 1) =>
        new() { BuffType = "SkillRate", SkillName = skill, Value = value, Duration = seconds, Delay = delay };

    private static BuffConfig Rate(string type, double value, double seconds, double delay = 1) =>
        new() { BuffType = type, Value = value, Duration = seconds, Delay = delay };

    private static BuffConfig Effect(string type, double seconds) =>
        new() { BuffType = type, Duration = seconds, AbsoluteValue = false };

    private static Dictionary<string, List<BuffConfig>> DefaultBuffSets() => new()
    {
        ["Buffs_Nicotine_Calm"] =
        [
            Rate("HealthRate", 1, 60),
            Skill("StressResistance", 15, 240), Skill("Attention", 10, 240), Skill("Perception", 10, 240), Skill("Endurance", 10, 240),
            Skill("StressResistance", -10, 60, 241), Rate("EnergyRate", -0.08, 60, 241),
        ],
        ["Buffs_Nicotine_Overdo"] = [Rate("MaxStamina", -20, 120), Rate("StaminaRate", -1, 120)],
        ["Buffs_Nicotine_W_Restless"] = [Skill("StressResistance", -5, 240), Skill("Attention", -5, 240)],
        ["Buffs_Nicotine_W_Hunger"] = [Rate("EnergyRate", -0.06, 180), Rate("HydrationRate", -0.04, 180)],
        ["Buffs_Nicotine_W_Shakes"] = [Effect("HandsTremor", 25)],
        ["Buffs_Nicotine_W_Breath"] = [Rate("MaxStamina", -15, 240), Rate("StaminaRate", -0.5, 240)],
        ["Buffs_Nicotine_W_Headache"] = [Effect("Pain", 45)],
        ["Buffs_Nicotine_W_ShakesBad"] = [Effect("HandsTremor", 60), Skill("StressResistance", -10, 180)],
        ["Buffs_Nicotine_W_Tunnel"] = [Effect("QuantumTunnelling", 25)],
        ["Buffs_Nicotine_W_Dizzy"] = [new BuffConfig { BuffType = "Contusion", Value = 1, Duration = 8, AbsoluteValue = false, AppliesTo = ["Head"] }],
        ["Buffs_Nicotine_W_Sick"] = [Rate("HealthRate", -0.15, 60), Rate("EnergyRate", -0.1, 120), Rate("HydrationRate", -0.1, 120)],
    };
}

public sealed class PackConfig
{
    public List<string> Ids { get; set; } =
    [
        "573475fb24597737fb1379e1",
        "573476d324597737da2adc13",
        "573476f124597737e04bf328",
        "5734770f24597738025ee254",
    ];

    public int Cigarettes { get; set; } = 20;

    public double SmokeSeconds { get; set; } = 7.4;

    public bool RequireLighter { get; set; } = false;

    public List<string> LighterIds { get; set; } =
    [
        "56742c284bdc2d98058b456d",
        "56742c2e4bdc2d95058b456d",
        "5e2af37686f774755a234b65",
        "5e2af2bc86f7746d3f3c33fc",
    ];

    public string UsePrefab { get; set; } = "assets/content/weapons/usable_items/item_food_snacker_beef/item_food_snacker_beef_container.bundle";

    public string ItemSound { get; set; } = "food_snack";

    public double EnergyPerCigarette { get; set; } = 3;
    public double HydrationPerCigarette { get; set; } = -0.5;
    public double PainReliefSeconds { get; set; } = 180;

    public bool Describe { get; set; } = true;
}

public sealed class ReliefConfig
{
    public bool RemoveTremor { get; set; } = true;

    public double TunnelVisionCut { get; set; } = 1;
    public double ContusionCut { get; set; } = 0.75;
    public double StunCut { get; set; } = 0.5;

    public double NegativeBuffCut { get; set; } = 0.6;

    public double PositiveBuffBoost { get; set; } = 0.25;

    public double PositiveBuffBoostMaxSeconds { get; set; } = 90;
    public double PositiveBuffBoostCap { get; set; } = 0.4;

    public string CalmBuffSet { get; set; } = "Buffs_Nicotine_Calm";

    public double RepeatWindowSeconds { get; set; } = 90;

    public double RepeatFactor { get; set; } = 0.5;
}

public sealed class AddictionConfig
{
    public double PerCigarette { get; set; } = 1.5;

    public double Ceiling { get; set; } = 110;

    public double Threshold { get; set; } = 20;

    public double WindowMinutesLight { get; set; } = 25;
    public double WindowMinutesHeavy { get; set; } = 6;

    public double RampMinutes { get; set; } = 15;

    public double RecoveryPerMinute { get; set; } = 0.1;
}

public sealed class WithdrawalConfig
{
    public double IntervalSecondsMild { get; set; } = 150;
    public double IntervalSecondsSevere { get; set; } = 45;
    public double Jitter { get; set; } = 0.3;

    public double CravingNoticeAt { get; set; } = 0.05;

    public List<WithdrawalEffect> Effects { get; set; } =
    [
        new() { BuffSet = "Buffs_Nicotine_W_Restless", MinSeverity = 0.05, Weight = 3, Message = "You're getting restless. You need a smoke." },
        new() { BuffSet = "Buffs_Nicotine_W_Hunger", MinSeverity = 0.15, Weight = 2, Message = "Your stomach churns." },
        new() { BuffSet = "Buffs_Nicotine_W_Shakes", MinSeverity = 0.25, MaxSeverity = 0.6, Weight = 3, Message = "Your hands won't stop shaking." },
        new() { BuffSet = "Buffs_Nicotine_W_Breath", MinSeverity = 0.35, Weight = 2, Message = "You're short of breath." },
        new() { BuffSet = "Buffs_Nicotine_W_Headache", MinSeverity = 0.5, Weight = 2, Message = "A pounding headache sets in." },
        new() { BuffSet = "Buffs_Nicotine_W_ShakesBad", MinSeverity = 0.6, Weight = 3, Message = "The shakes are getting worse." },
        new() { BuffSet = "Buffs_Nicotine_W_Tunnel", MinSeverity = 0.6, Weight = 2, Message = "Your vision narrows." },
        new() { BuffSet = "Buffs_Nicotine_W_Dizzy", MinSeverity = 0.75, Weight = 2, Message = "A wave of dizziness hits you." },
        new() { BuffSet = "Buffs_Nicotine_W_Sick", MinSeverity = 0.85, Weight = 1, Message = "You feel sick to your core." },
    ];
}

public sealed class OverdoConfig
{
    public int ChainLimit { get; set; } = 3;
    public double ChainWindowSeconds { get; set; } = 120;
    public string BuffSet { get; set; } = "Buffs_Nicotine_Overdo";
    public double CoughSeconds { get; set; } = 120;
    public double CoughIntervalMin { get; set; } = 10;
    public double CoughIntervalMax { get; set; } = 22;

    public string CoughPhrase { get; set; } = "Toxic";

    public string Message { get; set; } = "Too many, too fast. You can't stop coughing.";
}

public sealed class WithdrawalEffect
{
    public string BuffSet { get; set; } = "";
    public double MinSeverity { get; set; }
    public double MaxSeverity { get; set; } = 1;
    public double Weight { get; set; } = 1;
    public string? Message { get; set; }
}

public sealed class BuffConfig
{
    public string BuffType { get; set; } = "";
    public double Chance { get; set; } = 1;
    public double Delay { get; set; } = 1;
    public double Duration { get; set; }
    public double Value { get; set; }
    public bool AbsoluteValue { get; set; } = true;
    public string SkillName { get; set; } = "";
    public List<string>? AppliesTo { get; set; }
}

public static class NicotineJson
{
    public static readonly JsonSerializerOptions Read = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static readonly JsonSerializerOptions Write = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };
}
