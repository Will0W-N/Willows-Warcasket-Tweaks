using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace WarcasketStatTuner
{
    [HarmonyPatch(typeof(Verb_MeleeAttackDamage), "DamageInfosToApply")]
    internal static class WarcasketMeleePatch
    {
        private const string CombatExtendedAssembly = "CombatExtended";
        private const string CombatExtendedVerbType = "CombatExtended.Verb_MeleeAttackCE";
        private const string CombatExtendedArmorUtilityType = "CombatExtended.ArmorUtilityCE";
        private const string ZeroArmorMessage = "penAmount or armorAmount are zero";

        [ThreadStatic]
        private static Thing currentParryThing;

        private static bool Prefix(
            Verb_MeleeAttack __instance,
            LocalTargetInfo target,
            ref IEnumerable<DamageInfo> __result)
        {
            ModSettingsData settings = WarcasketTunerMod.Settings;
            Pawn caster = __instance.CasterPawn;
            if (settings == null ||
                caster == null ||
                (__instance.EquipmentSource != null &&
                 !ReferenceEquals(__instance.EquipmentSource, caster)))
            {
                return true;
            }

            bool isGhoul = GhoulWarcasketIntegration.IsTrackedGhoul(caster);
            if (isGhoul)
            {
                if (!settings.TuneGhoulUnarmedDamage)
                    return true;
            }
            else if (!settings.TuneUnarmedDamage || !WearsWarcasket(caster))
            {
                return true;
            }

            var damages = new List<DamageInfo>(2);
            float bluntDamage = isGhoul
                ? settings.GhoulUnarmedBluntDamage
                : settings.UnarmedBluntDamage;
            float sharpDamage = isGhoul
                ? settings.GhoulUnarmedSharpDamage
                : settings.UnarmedSharpDamage;

            if (bluntDamage > 0f)
            {
                damages.Add(new DamageInfo(
                    DamageDefOf.Blunt,
                    bluntDamage,
                    0f,
                    -1f,
                    caster));
            }

            if (sharpDamage > 0f)
            {
                damages.Add(new DamageInfo(
                    DamageDefOf.Cut,
                    sharpDamage,
                    0f,
                    -1f,
                    caster));
            }

            __result = damages;
            return false;
        }

        public static void PatchCombatExtended(Harmony harmony)
        {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(candidate => candidate.GetName().Name == CombatExtendedAssembly);
            if (assembly == null)
                return;

            Type verbType = assembly.GetType(CombatExtendedVerbType, false);
            MethodInfo target = verbType?.GetMethod(
                "DamageInfosToApply",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly,
                null,
                new[] { typeof(LocalTargetInfo), typeof(bool) },
                null);
            if (target == null)
            {
                Log.Error("[WarcasketStatTuner] Combat Extended is loaded, but its melee damage method could not be resolved.");
                return;
            }

            harmony.Patch(
                target,
                prefix: new HarmonyMethod(typeof(WarcasketMeleePatch), nameof(Prefix)));

            PatchGunParryArmorLog(harmony, assembly);
        }

        private static void PatchGunParryArmorLog(Harmony harmony, Assembly assembly)
        {
            Type armorUtilityType = assembly.GetType(CombatExtendedArmorUtilityType, false);
            MethodInfo applyParryDamage = armorUtilityType?.GetMethod(
                "ApplyParryDamage",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly,
                null,
                new[] { typeof(DamageInfo), typeof(Thing) },
                null);
            MethodInfo errorOnce = typeof(Log).GetMethod(
                "ErrorOnce",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(int) },
                null);

            if (applyParryDamage == null || errorOnce == null)
            {
                Log.Error("[WarcasketStatTuner] Could not resolve CE parry or Verse.Log.ErrorOnce for gun-parry log filtering.");
                return;
            }

            harmony.Patch(
                applyParryDamage,
                prefix: new HarmonyMethod(
                    typeof(WarcasketMeleePatch),
                    nameof(ParryDamagePrefix)),
                finalizer: new HarmonyMethod(
                    typeof(WarcasketMeleePatch),
                    nameof(ParryDamageFinalizer)));
            harmony.Patch(
                errorOnce,
                prefix: new HarmonyMethod(
                    typeof(WarcasketMeleePatch),
                    nameof(ErrorOncePrefix)));
        }

        private static void ParryDamagePrefix(Thing parryThing, out Thing __state)
        {
            __state = currentParryThing;
            currentParryThing = parryThing;
        }

        private static Exception ParryDamageFinalizer(
            Exception __exception,
            Thing __state)
        {
            currentParryThing = __state;
            return __exception;
        }

        private static bool ErrorOncePrefix(string text, int key)
        {
            return currentParryThing == null ||
                currentParryThing.def?.apparel != null ||
                text == null ||
                text.IndexOf(ZeroArmorMessage, StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool WearsWarcasket(Pawn pawn)
        {
            if (pawn.apparel == null)
                return false;

            foreach (Apparel apparel in pawn.apparel.WornApparel)
            {
                if (apparel?.def != null && DefCache.CachedDefs.ContainsKey(apparel.def.defName))
                    return true;
            }

            return false;
        }
    }
}
