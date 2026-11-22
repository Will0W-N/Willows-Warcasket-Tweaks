using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WarcasketStatTuner
{
    internal static class WarcasketUtilitySettings
    {
        private sealed class OriginalStatBase
        {
            public bool HadValue;
            public float Value;
        }

        private sealed class OriginalStatOffset
        {
            public bool HadValue;
            public float Value;
        }

        private static readonly Dictionary<string, OriginalStatBase> originalStatBases =
            new Dictionary<string, OriginalStatBase>();
        private static readonly Dictionary<string, OriginalStatOffset> originalStatOffsets =
            new Dictionary<string, OriginalStatOffset>();
        private static readonly Dictionary<TraitDef, WorkTags> originalDisabledWorkTags =
            new Dictionary<TraitDef, WorkTags>();

        public static void ApplySettings(ModSettingsData settings)
        {
            if (settings == null)
                return;

            ApplyTemperatureSettings(settings.EnableTemperatureTweaks);
            ApplyVacuumSettings(settings.EnableVacuumTweaks);
            ApplyWorkSettings(settings.EnableWarcasketWork);
        }

        private static void ApplyTemperatureSettings(bool enabled)
        {
            SetStats("VFEP_Warcasket_Hazard", enabled,
                ("Mass", 60f), ("Flammability", 0f), ("ArmorRating_Heat", 2f),
                ("Insulation_Cold", 225f), ("Insulation_Heat", 300f), ("EquipDelay", 1f));

            SetStats("VFEP_Warcasket_Siegebreaker", enabled,
                ("Mass", 50f), ("ArmorRating_Heat", 1f), ("Insulation_Cold", 125f),
                ("Insulation_Heat", 80f), ("EquipDelay", 1f));
            SetStats("VFEP_Warcasket_Guardian", enabled,
                ("Mass", 50f), ("ArmorRating_Heat", 1f), ("Insulation_Cold", 125f),
                ("Insulation_Heat", 80f), ("EquipDelay", 1f),
                ("VEF_EnergyShieldRadiusApparel", 5f));
            SetStats("VFEP_Warcasket_Controller", enabled,
                ("Mass", 50f), ("ArmorRating_Heat", 1f), ("Insulation_Cold", 125f),
                ("Insulation_Heat", 80f), ("EquipDelay", 1f));
            SetStats("VFEP_Warcasket_Sarcophagus", enabled,
                ("Mass", 50f), ("ArmorRating_Heat", 1f), ("Insulation_Cold", 125f),
                ("Insulation_Heat", 80f), ("EquipDelay", 1f));
            SetStats("VFEP_Warcasket_Brute", enabled,
                ("Mass", 50f), ("ArmorRating_Heat", 1f), ("Insulation_Cold", 125f),
                ("Insulation_Heat", 80f), ("EquipDelay", 1f));
            SetStats("Warcasket_Mechlord", enabled,
                ("Mass", 50f), ("ArmorRating_Heat", 1.5f), ("Insulation_Cold", 125f),
                ("Insulation_Heat", 300f), ("EquipDelay", 1f));
        }

        private static void ApplyVacuumSettings(bool enabled)
        {
            SetOffset("VFEP_Warcasket_Hazard", "VacuumResistance", 0.6f, enabled);
            SetOffset("VFEP_WarcasketShoulders_Hazard", "VacuumResistance", 0.1f, enabled);
            SetOffset("VFEP_WarcasketHelmet_Hazard", "VacuumResistance", 0.3f, enabled);
        }

        private static void ApplyWorkSettings(bool enabled)
        {
            ApplyWorkTrait("VFEP_WarcasketTrait", enabled);
            ApplyWorkTrait("VFEP_WarcasketTrait_Mech", enabled);
        }

        private static void ApplyWorkTrait(string defName, bool enabled)
        {
            TraitDef trait = DefDatabase<TraitDef>.GetNamedSilentFail(defName);
            if (trait == null)
                return;

            if (!originalDisabledWorkTags.TryGetValue(trait, out WorkTags original))
            {
                original = trait.disabledWorkTags;
                originalDisabledWorkTags[trait] = original;
            }

            trait.disabledWorkTags = enabled
                ? (SettingsAllowViolence() ? WorkTags.None : WorkTags.Violent)
                : original;
        }

        private static bool SettingsAllowViolence()
        {
            return WarcasketTunerMod.Settings?.AllowWarcasketViolence ?? true;
        }

        private static void SetStats(
            string defName,
            bool enabled,
            params (string StatName, float Value)[] values)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null)
                return;

            foreach ((string statName, float value) in values)
            {
                StatDef stat = DefDatabase<StatDef>.GetNamedSilentFail(statName);
                if (stat == null)
                    continue;

                string key = defName + "\0" + stat.defName;
                if (!originalStatBases.TryGetValue(key, out OriginalStatBase original))
                {
                    StatModifier modifier = def.statBases?.FirstOrDefault(item => item.stat == stat);
                    original = new OriginalStatBase
                    {
                        HadValue = modifier != null,
                        Value = modifier?.value ?? 0f
                    };
                    originalStatBases[key] = original;
                }

                if (enabled)
                {
                    def.SetStatBaseValue(stat, value);
                }
                else
                {
                    RestoreStatBase(def, stat, original);
                }
            }
        }

        private static void RestoreStatBase(ThingDef def, StatDef stat, OriginalStatBase original)
        {
            if (original.HadValue)
            {
                def.SetStatBaseValue(stat, original.Value);
                return;
            }

            def.statBases?.RemoveAll(modifier => modifier.stat == stat);
        }

        private static void SetOffset(string defName, string statName, float value, bool enabled)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            StatDef stat = DefDatabase<StatDef>.GetNamedSilentFail(statName);
            if (def == null || stat == null)
                return;

            string key = defName + "\0" + stat.defName;
            if (!originalStatOffsets.TryGetValue(key, out OriginalStatOffset original))
            {
                StatModifier modifier = def.equippedStatOffsets?
                    .FirstOrDefault(item => item.stat == stat);
                original = new OriginalStatOffset
                {
                    HadValue = modifier != null,
                    Value = modifier?.value ?? 0f
                };
                originalStatOffsets[key] = original;
            }

            if (enabled)
            {
                if (def.equippedStatOffsets == null)
                    def.equippedStatOffsets = new List<StatModifier>();

                StatModifier modifier = def.equippedStatOffsets
                    .FirstOrDefault(item => item.stat == stat);
                if (modifier == null)
                {
                    modifier = new StatModifier { stat = stat };
                    def.equippedStatOffsets.Add(modifier);
                }

                modifier.value = value;
            }
            else if (original.HadValue)
            {
                StatModifier modifier = def.equippedStatOffsets?
                    .FirstOrDefault(item => item.stat == stat);
                if (modifier == null)
                {
                    modifier = new StatModifier { stat = stat };
                    def.equippedStatOffsets.Add(modifier);
                }

                modifier.value = original.Value;
            }
            else
            {
                def.equippedStatOffsets?.RemoveAll(modifier => modifier.stat == stat);
            }
        }

    }
}
