using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace WarcasketStatTuner
{
    internal static class WarcasketShieldPatch
    {
        private const string VefAssemblyName = "VEF";
        private const string ShieldCompTypeName = "VEF.Apparels.CompShieldBubble";

        private static FieldInfo parentField;
        private static Type shieldCompType;
        private static FieldInfo cachedMaxShieldField;
        private static FieldInfo cachedMaxRechargeField;

        public static void Initialize(Harmony harmony)
        {
            Assembly vefAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == VefAssemblyName);
            shieldCompType = vefAssembly?.GetType(ShieldCompTypeName, false);
            if (shieldCompType == null)
                return;

            cachedMaxShieldField = shieldCompType.GetField(
                "cachedMaxShield",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            cachedMaxRechargeField = shieldCompType.GetField(
                "cachedMaxRecharge",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo energyMaxGetter = shieldCompType.GetProperty(
                "EnergyMax",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetGetMethod(true);

            if (energyMaxGetter == null)
                return;

            parentField = typeof(ThingComp).GetField(
                "parent",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (parentField == null)
            {
                Log.Error("[WarcasketStatTuner] Could not resolve ThingComp.parent for shield capacity patch.");
                return;
            }

            harmony.Patch(
                energyMaxGetter,
                prefix: new HarmonyMethod(
                    typeof(WarcasketShieldPatch),
                    nameof(EnergyMaxPrefix)));
        }

        private static bool EnergyMaxPrefix(object __instance, ref float __result)
        {
            Thing parent = parentField.GetValue(__instance) as Thing;
            if (!(parent is Apparel apparel) ||
                !DefCache.CachedDefs.TryGetValue(
                    apparel.def.defName,
                    out DefCache.CachedStats cached) ||
                !cached.HasShieldMax)
            {
                return true;
            }

            __result = StatPatcher.GetEffectiveShieldMax(apparel.def);
            return false;
        }

        public static void RefreshGhoulShieldComponents(Pawn pawn)
        {
            if (shieldCompType == null || pawn?.apparel?.WornApparel == null)
                return;

            foreach (Apparel apparel in pawn.apparel.WornApparel)
            {
                if (apparel?.def == null ||
                    !DefCache.CachedDefs.TryGetValue(
                        apparel.def.defName,
                        out DefCache.CachedStats cached) ||
                    !cached.HasShield)
                {
                    continue;
                }

                StatPatcher.ApplyShieldSettings(apparel.def);
                foreach (ThingComp comp in apparel.AllComps)
                {
                    if (!shieldCompType.IsInstanceOfType(comp))
                        continue;

                    cachedMaxShieldField?.SetValue(comp, 0f);
                    cachedMaxRechargeField?.SetValue(comp, 0f);
                }
            }
        }
    }
}
