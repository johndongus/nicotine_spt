using System.Collections.Generic;
using Newtonsoft.Json;

namespace Nicotine.Client
{
#pragma warning disable CS0649
    internal sealed class StateResponse
    {
        public SavedState State;
        public NicotineConfig Config;
    }

    internal sealed class SavedState
    {
        [JsonProperty("addiction")] public float Addiction;

        [JsonProperty("sinceLastSmoke")] public float SinceLastSmoke = -1;

        [JsonProperty("smoked")] public int Smoked;
    }

    internal sealed class NicotineConfig
    {
        public bool Enabled = true;
        public PackConfig Packs = new PackConfig();
        public ReliefConfig Relief = new ReliefConfig();
        public AddictionConfig Addiction = new AddictionConfig();
        public WithdrawalConfig Withdrawal = new WithdrawalConfig();
        public OverdoConfig Overdo = new OverdoConfig();
    }

    internal sealed class OverdoConfig
    {
        public int ChainLimit = 3;
        public float ChainWindowSeconds = 120;
        public string BuffSet = "Buffs_Nicotine_Overdo";
        public float CoughSeconds = 120;
        public float CoughIntervalMin = 10;
        public float CoughIntervalMax = 22;
        public string CoughPhrase = "Toxic";
        public string Message = "Too many, too fast. You can't stop coughing.";
    }

    internal sealed class PackConfig
    {
        public List<string> Ids = new List<string>
        {
            "573475fb24597737fb1379e1",
            "573476d324597737da2adc13",
            "573476f124597737e04bf328",
            "5734770f24597738025ee254",
        };

        public bool RequireLighter;

        public List<string> LighterIds = new List<string>
        {
            "56742c284bdc2d98058b456d",
            "56742c2e4bdc2d95058b456d",
            "5e2af37686f774755a234b65",
            "5e2af2bc86f7746d3f3c33fc",
        };
    }

    internal sealed class ReliefConfig
    {
        public bool RemoveTremor = true;
        public float TunnelVisionCut = 1f;
        public float ContusionCut = 0.75f;
        public float StunCut = 0.5f;
        public float NegativeBuffCut = 0.6f;
        public float PositiveBuffBoost = 0.25f;
        public float PositiveBuffBoostMaxSeconds = 90;
        public float PositiveBuffBoostCap = 0.4f;
        public string CalmBuffSet = "Buffs_Nicotine_Calm";
        public float RepeatWindowSeconds = 90;
        public float RepeatFactor = 0.5f;
    }

    internal sealed class AddictionConfig
    {
        public float PerCigarette = 1.5f;
        public float Ceiling = 110;
        public float Threshold = 20;
        public float WindowMinutesLight = 25;
        public float WindowMinutesHeavy = 6;
        public float RampMinutes = 15;
        public float RecoveryPerMinute = 0.1f;
    }

    internal sealed class WithdrawalConfig
    {
        public float IntervalSecondsMild = 150;
        public float IntervalSecondsSevere = 45;
        public float Jitter = 0.3f;
        public float CravingNoticeAt = 0.05f;
        public List<WithdrawalEffect> Effects = new List<WithdrawalEffect>();
    }

    internal sealed class WithdrawalEffect
    {
        public string BuffSet = "";
        public float MinSeverity;
        public float MaxSeverity = 1;
        public float Weight = 1;
        public string Message;
    }
#pragma warning restore CS0649
}
