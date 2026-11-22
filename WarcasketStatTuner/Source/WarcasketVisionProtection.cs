using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace WarcasketStatTuner
{
    internal static class WarcasketVisionProtection
    {
        internal const string DustStormHediffDefName = "VEE_DustStorm";
        private static readonly HashSet<string> PhysicalEyeDebuffs =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "SandInEyes",
                "DirtInEyes",
                "MudInEyes",
                "GravelInEyes",
                "WaterInEyes"
            };

        public static bool HasAdvancedTorso(Pawn pawn)
        {
            if (pawn?.apparel?.WornApparel == null)
                return false;

            return pawn.apparel.WornApparel.Any(apparel =>
                DefCache.IsWarcasket(apparel.def) &&
                CoversBodyGroup(apparel.def, "Torso") &&
                IsAdvancedTier(apparel.def));
        }

        public static bool HasWarcasketHelmet(Pawn pawn)
        {
            if (pawn?.apparel?.WornApparel == null)
                return false;

            return pawn.apparel.WornApparel.Any(apparel =>
                DefCache.IsWarcasket(apparel.def) &&
                (CoversBodyGroup(apparel.def, "Head") ||
                 CoversBodyGroup(apparel.def, "Eye")));
        }

        public static bool IsEnvironmentalVisionHediff(HediffDef def)
        {
            return def != null &&
                string.Equals(
                    def.defName,
                    DustStormHediffDefName,
                    StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsPhysicalEyeDebuff(HediffDef def)
        {
            return def != null && PhysicalEyeDebuffs.Contains(def.defName);
        }

        private static bool IsAdvancedTier(ThingDef def)
        {
            if (def.techLevel >= TechLevel.Spacer)
                return true;

            if (def.researchPrerequisites != null &&
                def.researchPrerequisites.Any(IsAdvancedResearch))
            {
                return true;
            }

            return def.apparel?.tags != null &&
                def.apparel.tags.Any(tag =>
                    tag.IndexOf("Spacer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    tag.IndexOf("Advanced", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    tag.IndexOf("Specialised", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    tag.IndexOf("Specialized", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsAdvancedResearch(ResearchProjectDef research)
        {
            if (research == null)
                return false;

            string name = research.defName ?? string.Empty;
            return name.IndexOf("Spacer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Advanced", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Specialised", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Specialized", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool CoversBodyGroup(ThingDef def, string groupName)
        {
            return def.apparel?.bodyPartGroups != null &&
                def.apparel.bodyPartGroups.Any(group =>
                    group != null &&
                    !string.IsNullOrEmpty(group.defName) &&
                    group.defName.IndexOf(groupName, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }

    [HarmonyPatch]
    internal static class WarcasketWeatherAccuracyPatch
    {
        private static readonly FieldInfo WeatherFactor =
            AccessTools.Field(typeof(ShotReport), "factorFromWeather");
        private static readonly FieldInfo DarknessOffset =
            AccessTools.Field(typeof(ShotReport), "offsetFromDarkness");

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(ShotReport),
                nameof(ShotReport.HitReportFor),
                new[] { typeof(Thing), typeof(Verb), typeof(LocalTargetInfo) });
        }

        private static void Postfix(Verb __1, ref ShotReport __result)
        {
            Pawn shooter = __1?.Caster as Pawn;
            if (!WarcasketVisionProtection.HasAdvancedTorso(shooter))
                return;

            object report = __result;
            WeatherFactor?.SetValue(report, 1f);
            DarknessOffset?.SetValue(report, 0f);
            __result = (ShotReport)report;
        }
    }

    [HarmonyPatch]
    internal static class WarcasketDustStormSightPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(PawnCapacityUtility),
                nameof(PawnCapacityUtility.CalculateCapacityLevel),
                new[]
                {
                    typeof(HediffSet),
                    typeof(PawnCapacityDef),
                    typeof(List<PawnCapacityUtility.CapacityImpactor>),
                    typeof(bool)
                });
        }

        private static void Postfix(
            HediffSet __0,
            PawnCapacityDef __1,
            ref float __result)
        {
            if (__0 == null || __1?.defName != "Sight")
            {
                return;
            }

            HediffDef dustStormDef = DefDatabase<HediffDef>.GetNamedSilentFail(
                WarcasketVisionProtection.DustStormHediffDefName);
            if (dustStormDef == null)
                return;

            Pawn pawn = Traverse.Create(__0).Field("pawn").GetValue<Pawn>();
            if (!WarcasketVisionProtection.HasAdvancedTorso(pawn))
                return;

            Hediff dustStorm = __0.hediffs.FirstOrDefault(hediff => hediff.def == dustStormDef);
            if (dustStorm == null)
                return;

            pawn.health.RemoveHediff(dustStorm);
            __result = PawnCapacityUtility.CalculateCapacityLevel(__0, __1, null, false);
        }
    }

    [HarmonyPatch]
    internal static class WarcasketEyeAndWeatherHediffPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return typeof(Pawn_HealthTracker).GetMethods(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method =>
                    method.Name == nameof(Pawn_HealthTracker.AddHediff) ||
                    method.Name == nameof(Pawn_HealthTracker.GetOrAddHediff));
        }

        private static bool Prefix(Pawn_HealthTracker __instance, object[] __args)
        {
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (pawn == null || __args == null)
                return true;

            HediffDef hediffDef = __args
                .Select(GetHediffDef)
                .FirstOrDefault(def => def != null);
            if (hediffDef == null)
                return true;

            if (WarcasketVisionProtection.HasAdvancedTorso(pawn) &&
                WarcasketVisionProtection.IsEnvironmentalVisionHediff(hediffDef))
            {
                return false;
            }

            return !WarcasketVisionProtection.HasWarcasketHelmet(pawn) ||
                !WarcasketVisionProtection.IsPhysicalEyeDebuff(hediffDef);
        }

        private static HediffDef GetHediffDef(object argument)
        {
            if (argument is HediffDef hediffDef)
                return hediffDef;
            if (argument is Hediff hediff)
                return hediff.def;
            return null;
        }
    }

    internal static class WarcasketEyeCombatLogFilter
    {
        public static bool ShouldSuppress(LogEntry entry)
        {
            LogEntryDef def = Traverse.Create(entry).Field("def").GetValue<LogEntryDef>();
            if (def?.defName != "SandInEyes")
                return false;

            for (Type type = entry.GetType(); type != null; type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly))
                {
                    if (!IsTargetField(field.Name) ||
                        !typeof(Thing).IsAssignableFrom(field.FieldType))
                    {
                        continue;
                    }

                    Pawn target = field.GetValue(entry) as Pawn;
                    return WarcasketVisionProtection.HasWarcasketHelmet(target);
                }
            }

            List<Pawn> concernedPawns = entry.GetConcerns().OfType<Pawn>().ToList();
            return concernedPawns.Count == 1 &&
                WarcasketVisionProtection.HasWarcasketHelmet(concernedPawns[0]);
        }

        private static bool IsTargetField(string fieldName)
        {
            return fieldName.IndexOf("recipient", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fieldName.IndexOf("target", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fieldName.IndexOf("victim", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    [HarmonyPatch(typeof(LogEntry), nameof(LogEntry.ToGameStringFromPOV))]
    internal static class WarcasketEyeCombatLogPatch
    {
        private static bool Prefix(LogEntry __instance, ref string __result)
        {
            if (!WarcasketEyeCombatLogFilter.ShouldSuppress(__instance))
                return true;

            __result = string.Empty;
            return false;
        }
    }

    [HarmonyPatch(typeof(LogEntry), nameof(LogEntry.IconFromPOV))]
    internal static class WarcasketEyeCombatLogIconPatch
    {
        private static void Postfix(LogEntry __instance, ref Texture2D __result)
        {
            if (WarcasketEyeCombatLogFilter.ShouldSuppress(__instance))
                __result = null;
        }
    }
}
