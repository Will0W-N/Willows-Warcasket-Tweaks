using System;
using RimWorld;
using Verse;

namespace WarcasketStatTuner
{
    public static class StatPatcher
    {
        public static void ApplyAllSettings()
        {
            ModSettingsData settings = WarcasketTunerMod.Settings;
            if (settings == null)
                return;

            foreach (var pair in DefCache.CachedDefs)
            {
                DefCache.CachedStats cached = pair.Value;
                ThingDef def = cached.Def;

                float sharp = GetEffectiveArmorSharp(def);
                float blunt = GetEffectiveArmorBlunt(def);

                def.SetStatBaseValue(StatDefOf.ArmorRating_Sharp, sharp);
                def.SetStatBaseValue(StatDefOf.ArmorRating_Blunt, blunt);
                ApplyShieldSettings(def);
            }
        }

        public static void ApplyShieldSettings(ThingDef def)
        {
            if (def == null ||
                !DefCache.CachedDefs.TryGetValue(def.defName, out DefCache.CachedStats cached) ||
                !cached.HasShield)
            {
                return;
            }

            float shieldMax = GetEffectiveShieldMax(def);
            float shieldRecharge = GetEffectiveShieldRecharge(def);

            if (cached.ShieldComp != null && cached.ShieldRechargeIsCompValue &&
                cached.ShieldRechargeField != null)
            {
                cached.ShieldRechargeField.SetValue(
                    cached.ShieldComp,
                    Convert.ChangeType(shieldRecharge, cached.ShieldRechargeField.FieldType));
            }

            if (cached.HasShieldMax &&
                cached.HasShieldMaxStat &&
                StatDefOf.EnergyShieldEnergyMax != null &&
                def.statBases != null)
            {
                def.SetStatBaseValue(StatDefOf.EnergyShieldEnergyMax, shieldMax / 100f);
            }

            if (cached.HasShieldRecharge &&
                cached.HasShieldRechargeStat &&
                StatDefOf.EnergyShieldRechargeRate != null &&
                def.statBases != null)
            {
                def.SetStatBaseValue(StatDefOf.EnergyShieldRechargeRate, shieldRecharge);
            }
        }

        public static float GetEffectiveArmorSharp(ThingDef def)
        {
            ModSettingsData settings = WarcasketTunerMod.Settings;
            if (settings == null || def == null)
                return def?.GetStatValueAbstract(StatDefOf.ArmorRating_Sharp) ?? 0f;

            if (settings.Overrides.TryGetValue(def.defName, out CustomStatOverride ovr) &&
                ovr != null &&
                ovr.Sharp >= 0f)
            {
                return ovr.Sharp;
            }

            if (DefCache.CachedDefs.TryGetValue(def.defName, out DefCache.CachedStats cached))
                return cached.OriginalSharp * settings.SharpMultiplier;

            return def.GetStatValueAbstract(StatDefOf.ArmorRating_Sharp);
        }

        public static float GetEffectiveArmorBlunt(ThingDef def)
        {
            ModSettingsData settings = WarcasketTunerMod.Settings;
            if (settings == null || def == null)
                return def?.GetStatValueAbstract(StatDefOf.ArmorRating_Blunt) ?? 0f;

            if (settings.Overrides.TryGetValue(def.defName, out CustomStatOverride ovr) &&
                ovr != null &&
                ovr.Blunt >= 0f)
            {
                return ovr.Blunt;
            }

            if (DefCache.CachedDefs.TryGetValue(def.defName, out DefCache.CachedStats cached))
                return cached.OriginalBlunt * settings.BluntMultiplier;

            return def.GetStatValueAbstract(StatDefOf.ArmorRating_Blunt);
        }

        public static float GetEffectiveShieldMax(ThingDef def)
        {
            if (def == null)
                return 0f;

            ModSettingsData settings = WarcasketTunerMod.Settings;
            if (DefCache.CachedDefs.TryGetValue(def.defName, out DefCache.CachedStats cached) &&
                cached.HasShieldMax)
            {
                if (settings != null &&
                    settings.Overrides.TryGetValue(def.defName, out CustomStatOverride ovr) &&
                    ovr != null &&
                    IsValidShieldOverride(ovr.ShieldMax))
                {
                    return ovr.ShieldMax;
                }

                float multiplier = settings?.ShieldMaxMultiplier ?? 1f;
                float value = cached.OriginalShieldMax * multiplier;
                return IsValidShieldOverride(value) ? value : cached.OriginalShieldMax;
            }

            return 0f;
        }

        public static float GetEffectiveShieldRecharge(ThingDef def)
        {
            if (def == null)
                return 0f;

            ModSettingsData settings = WarcasketTunerMod.Settings;
            if (DefCache.CachedDefs.TryGetValue(def.defName, out DefCache.CachedStats cached) &&
                cached.HasShieldRecharge)
            {
                if (settings != null &&
                    settings.Overrides.TryGetValue(def.defName, out CustomStatOverride ovr) &&
                    ovr != null &&
                    IsUsableShieldValue(ovr.ShieldRecharge))
                {
                    return ovr.ShieldRecharge;
                }

                float multiplier = settings?.ShieldRechargeMultiplier ?? 1f;
                float value = cached.OriginalShieldRecharge * multiplier;
                return IsUsableShieldValue(value) ? value : cached.OriginalShieldRecharge;
            }

            return 0f;
        }

        private static bool IsUsableShieldValue(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static bool IsValidShieldOverride(float value)
        {
            return value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}