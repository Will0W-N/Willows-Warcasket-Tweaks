using System.Collections.Generic;
using Verse;

namespace WarcasketStatTuner
{
    public class CustomStatOverride : IExposable
    {
        // -1f means "use the global multiplier for this field".
        public float Sharp = -1f;
        public float Blunt = -1f;
        public float ShieldMax = -1f;
        public float ShieldRecharge = -1f;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Sharp, "sharp", -1f);
            Scribe_Values.Look(ref Blunt, "blunt", -1f);
            Scribe_Values.Look(ref ShieldMax, "shieldMax", -1f);
            Scribe_Values.Look(ref ShieldRecharge, "shieldRecharge", -1f);
        }
    }

    public class ModSettingsData : ModSettings
    {
        public float SharpMultiplier = 1f;
        public float BluntMultiplier = 1f;
        public float ShieldMaxMultiplier = 1f;
        public float ShieldRechargeMultiplier = 1f;
        public bool TuneUnarmedDamage = true;
        public float UnarmedBluntDamage = 19f;
        public float UnarmedSharpDamage = 4f;
        public bool TuneGhoulUnarmedDamage = true;
        public float GhoulUnarmedBluntDamage = 19f;
        public float GhoulUnarmedSharpDamage = 4f;
        public bool TuneGhoulMoveSpeed = true;
        public float GhoulMoveSpeed = 4.6f;
        public bool TuneWarcasketMoveSpeed;
        public float WarcasketMoveSpeed = 4.6f;
        public bool EnableTemperatureTweaks = true;
        public bool EnableVacuumTweaks = true;
        public bool EnableWarcasketWork = true;
        public List<string> EnabledWarcasketWorkTypes = new List<string>();
        public bool WarcasketWorkDefaultsInitialized;
        public bool AllowWarcasketViolence = true;

        public Dictionary<string, CustomStatOverride> Overrides =
            new Dictionary<string, CustomStatOverride>();

        public override void ExposeData()
        {
            Scribe_Values.Look(ref SharpMultiplier, "sharpMultiplier", 1f);
            Scribe_Values.Look(ref BluntMultiplier, "bluntMultiplier", 1f);
            Scribe_Values.Look(ref ShieldMaxMultiplier, "shieldMaxMultiplier", 1f);
            Scribe_Values.Look(ref ShieldRechargeMultiplier, "shieldRechargeMultiplier", 1f);
            Scribe_Values.Look(ref TuneUnarmedDamage, "tuneUnarmedDamage", true);
            Scribe_Values.Look(ref UnarmedBluntDamage, "unarmedBluntDamage", 19f);
            Scribe_Values.Look(ref UnarmedSharpDamage, "unarmedSharpDamage", 4f);
            Scribe_Values.Look(ref TuneGhoulUnarmedDamage, "tuneGhoulUnarmedDamage", true);
            Scribe_Values.Look(ref GhoulUnarmedBluntDamage, "ghoulUnarmedBluntDamage", 19f);
            Scribe_Values.Look(ref GhoulUnarmedSharpDamage, "ghoulUnarmedSharpDamage", 4f);
            Scribe_Values.Look(ref TuneGhoulMoveSpeed, "tuneGhoulMoveSpeed", true);
            Scribe_Values.Look(ref GhoulMoveSpeed, "ghoulMoveSpeed", 4.6f);
            Scribe_Values.Look(ref TuneWarcasketMoveSpeed, "tuneWarcasketMoveSpeed", false);
            Scribe_Values.Look(ref WarcasketMoveSpeed, "warcasketMoveSpeed", 4.6f);
            Scribe_Values.Look(ref EnableTemperatureTweaks, "enableTemperatureTweaks", true);
            Scribe_Values.Look(ref EnableVacuumTweaks, "enableVacuumTweaks", true);
            Scribe_Values.Look(ref EnableWarcasketWork, "enableWarcasketWork", true);
            Scribe_Collections.Look(
                ref EnabledWarcasketWorkTypes,
                "enabledWarcasketWorkTypes",
                LookMode.Value);
            Scribe_Values.Look(
                ref WarcasketWorkDefaultsInitialized,
                "warcasketWorkDefaultsInitialized",
                false);
            Scribe_Values.Look(
                ref AllowWarcasketViolence,
                "allowWarcasketViolence",
                true);

            Scribe_Collections.Look(
                ref Overrides,
                "overrides",
                LookMode.Value,
                LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && Overrides == null)
                Overrides = new Dictionary<string, CustomStatOverride>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && EnabledWarcasketWorkTypes == null)
                EnabledWarcasketWorkTypes = new List<string>();
        }

        public void InitializeWarcasketWorkDefaults()
        {
            if (WarcasketWorkDefaultsInitialized)
            {
                if (EnabledWarcasketWorkTypes.Count == 2 &&
                    EnabledWarcasketWorkTypes.Contains("Hauling") &&
                    EnabledWarcasketWorkTypes.Contains("Mining"))
                {
                    SetDefaultWarcasketWorkTypes();
                }
                return;
            }

            SetDefaultWarcasketWorkTypes();
            WarcasketWorkDefaultsInitialized = true;
        }

        public void SetDefaultWarcasketWorkTypes()
        {
            EnabledWarcasketWorkTypes.Clear();
            EnabledWarcasketWorkTypes.Add("Hauling");
            EnabledWarcasketWorkTypes.Add("Mining");
            EnabledWarcasketWorkTypes.Add("Hunting");
            EnabledWarcasketWorkTypes.Add("Warden");
            AllowWarcasketViolence = true;
        }
    }
}