using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace WarcasketStatTuner
{
    [StaticConstructorOnStartup]
    public static class DefCache
    {
        public class CachedStats
        {
            public ThingDef Def;
            public float OriginalSharp;
            public float OriginalBlunt;

            public bool HasShield;
            public bool HasShieldMax;
            public bool HasShieldRecharge;
            public bool HasShieldMaxStat;
            public bool HasShieldRechargeStat;
            public bool ShieldMaxIsCompValue;
            public bool ShieldRechargeIsCompValue;
            public bool ShieldMaxIsFactor;
            public bool ShieldRechargeIsOffset;
            public float OriginalShieldMax;
            public float OriginalShieldRecharge;

            public CompProperties ShieldComp;
            public FieldInfo ShieldMaxField;
            public FieldInfo ShieldRechargeField;
            public StatModifier ShieldMaxFactorOffset;
            public StatModifier ShieldRechargeOffset;
        }

        public static readonly Dictionary<string, CachedStats> CachedDefs =
            new Dictionary<string, CachedStats>();

        static DefCache()
        {
            ScanDefs();
        }

        private static void ScanDefs()
        {
            CachedDefs.Clear();
            StatDef shieldMaxFactorStat =
                DefDatabase<StatDef>.GetNamedSilentFail("VEF_EnergyShieldEnergyMaxFactor");

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def == null || def.apparel == null)
                    continue;

                if (!IsWarcasket(def))
                    continue;

                var cached = new CachedStats { Def = def };

                cached.OriginalSharp = def.GetStatValueAbstract(StatDefOf.ArmorRating_Sharp);
                cached.OriginalBlunt = def.GetStatValueAbstract(StatDefOf.ArmorRating_Blunt);

                CompProperties shieldComp = FindShieldComp(def);
                bool hasShieldMaxStat = HasStatBase(def, StatDefOf.EnergyShieldEnergyMax);
                bool hasShieldRechargeStat = HasStatBase(def, StatDefOf.EnergyShieldRechargeRate);
                FieldInfo maxField = shieldComp == null
                    ? null
                    : FindFloatField(
                        shieldComp,
                        "energyShieldEnergyMax",
                        "energyShieldMax",
                        "maxEnergy",
                        "shieldMax");

                FieldInfo rechargeField = shieldComp == null
                    ? null
                    : FindFloatField(
                        shieldComp,
                        "energyShieldRechargeRate",
                        "energyRechargeRate",
                        "rechargeRate",
                        "shieldRecharge");

                StatModifier maxFactorOffset = FindStatOffset(def, shieldMaxFactorStat);
                StatModifier rechargeOffset = FindStatOffset(def, StatDefOf.EnergyShieldRechargeRate);

                float shieldStatMax = hasShieldMaxStat && StatDefOf.EnergyShieldEnergyMax != null
                    ? def.GetStatValueAbstract(StatDefOf.EnergyShieldEnergyMax)
                    : 0f;
                float shieldStatRecharge = hasShieldRechargeStat && StatDefOf.EnergyShieldRechargeRate != null
                    ? def.GetStatValueAbstract(StatDefOf.EnergyShieldRechargeRate)
                    : 0f;
                float shieldCompMax = maxField == null
                    ? 0f
                    : Convert.ToSingle(maxField.GetValue(shieldComp));
                float shieldCompRecharge = rechargeField == null
                    ? 0f
                    : Convert.ToSingle(rechargeField.GetValue(shieldComp));

                cached.HasShieldMax =
                    IsPositiveFinite(shieldCompMax) || hasShieldMaxStat;
                cached.HasShieldRecharge =
                    IsPositiveFinite(shieldCompRecharge) || hasShieldRechargeStat;
                cached.HasShieldMaxStat = hasShieldMaxStat;
                cached.HasShieldRechargeStat = hasShieldRechargeStat;
                cached.ShieldMaxIsCompValue = IsPositiveFinite(shieldCompMax);
                cached.ShieldRechargeIsCompValue = IsPositiveFinite(shieldCompRecharge);
                cached.HasShield = cached.HasShieldMax || cached.HasShieldRecharge;
                if (cached.HasShield)
                {
                    cached.ShieldComp = shieldComp;
                    cached.ShieldMaxField = maxField;
                    cached.ShieldRechargeField = rechargeField;
                    cached.ShieldMaxFactorOffset = maxFactorOffset;
                    cached.ShieldRechargeOffset = rechargeOffset;
                    cached.ShieldMaxIsFactor =
                        maxFactorOffset != null && maxField == null && !hasShieldMaxStat;
                    cached.ShieldRechargeIsOffset =
                        rechargeOffset != null && rechargeField == null && !hasShieldRechargeStat;

                    cached.OriginalShieldMax = maxField != null
                        && cached.ShieldMaxIsCompValue
                        ? shieldCompMax
                        : hasShieldMaxStat
                            ? shieldStatMax * 100f
                            : 0f;

                    cached.OriginalShieldRecharge = rechargeField != null
                        && cached.ShieldRechargeIsCompValue
                        ? shieldCompRecharge
                        : hasShieldRechargeStat
                            ? shieldStatRecharge
                            : 0f;
                }

                CachedDefs[def.defName] = cached;
            }

            Log.Message($"[WarcasketStatTuner] Cached {CachedDefs.Count} warcasket ThingDefs.");
        }

        public static bool IsWarcasket(ThingDef def)
        {
            // Check the whole hierarchy because expansions may subclass a shared warcasket apparel class.
            for (Type type = def.thingClass; type != null; type = type.BaseType)
            {
                string typeName = type.FullName ?? type.Name;
                if (ContainsWarcasket(typeName))
                    return true;
            }

            // Third-party defs do not always use a warcasket-specific apparel class or tag.
            if (ContainsWarcasket(def.defName) || ContainsWarcasket(def.label))
                return true;

            if (def.apparel?.tags != null)
            {
                foreach (string tag in def.apparel.tags)
                {
                    if (ContainsWarcasket(tag))
                        return true;
                }
            }

            if (def.modExtensions != null)
            {
                foreach (DefModExtension ext in def.modExtensions)
                {
                    if (ext != null && ContainsWarcasket(ext.GetType().FullName ?? ext.GetType().Name))
                        return true;
                }
            }

            return false;
        }

        private static StatModifier FindStatOffset(ThingDef def, StatDef stat)
        {
            if (stat == null || def.equippedStatOffsets == null)
                return null;

            return def.equippedStatOffsets.FirstOrDefault(offset => offset.stat == stat);
        }

        private static bool ContainsWarcasket(string value)
        {
            return !string.IsNullOrEmpty(value) &&
                value.IndexOf("Warcasket", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static CompProperties FindShieldComp(ThingDef def)
        {
            if (def.comps == null)
                return null;

            foreach (CompProperties comp in def.comps)
            {
                if (comp == null) continue;

                string typeName = comp.GetType().FullName ?? comp.GetType().Name;
                if (typeName.IndexOf("Shield", StringComparison.OrdinalIgnoreCase) >= 0)
                    return comp;
            }

            return null;
        }

        private static bool HasStatBase(ThingDef def, StatDef stat)
        {
            return stat != null &&
                def.statBases != null &&
                def.statBases.Any(statBase => statBase.stat == stat);
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static FieldInfo FindFloatField(object instance, params string[] candidateNames)
        {
            Type type = instance.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.IgnoreCase;

            foreach (string candidate in candidateNames)
            {
                FieldInfo field = type.GetField(candidate, flags);
                if (field != null &&
                    (field.FieldType == typeof(float) ||
                     field.FieldType == typeof(double)))
                {
                    return field;
                }
            }

            // Fallback: any float field whose name contains one of the candidate fragments.
            foreach (FieldInfo field in type.GetFields(
                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (field.FieldType != typeof(float)) continue;
                string lower = field.Name.ToLowerInvariant();
                if (candidateNames.Any(c => lower.Contains(c.ToLowerInvariant())))
                    return field;
            }

            return null;
        }
    }
}