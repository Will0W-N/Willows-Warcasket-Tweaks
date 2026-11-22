using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace WarcasketStatTuner
{
    internal static class GhoulWarcasketIntegration
    {
        private const string WarcasketAssemblyName = "VFEPirates";
        private const string FoundryTypeName = "VFEPirates.Buildings.Building_WarcasketFoundry";
        private const string PiratesModTypeName = "VFEPirates.VFEPiratesMod";
        private static readonly HashSet<Pawn> trackedGhouls = new HashSet<Pawn>();
        private static PropertyInfo isGhoulProperty;
        private static FieldInfo occupantField;
        private static FieldInfo compPowerField;
        private static FieldInfo armorDefsField;
        private static FieldInfo shoulderDefsField;
        private static FieldInfo helmetDefsField;
        private static bool predicateFailureLogged;
        private static bool workEligibilityPatchInstalled;

        public static bool IsAvailable
        {
            get { return HasGhoulPredicate; }
        }

        public static void Initialize(Harmony harmony)
        {
            ResolveGhoulPredicate();
            PatchMoveSpeed(harmony);
            PatchWorkEligibility(harmony);

            if (!HasGhoulPredicate)
                return;

            PatchFoundryGhoulEntombment(harmony);
            PatchPawnGizmos(harmony);
        }

        public static bool IsTrackedGhoul(Pawn pawn)
        {
            return pawn != null && trackedGhouls.Contains(pawn);
        }

        public static bool IsGhoulPawn(Pawn pawn)
        {
            return IsTrackedGhoul(pawn);
        }

        public static void ScanLoadedPawns()
        {
            trackedGhouls.Clear();
            if (!HasGhoulPredicate)
                return;

            foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_Alive)
            {
                if (IsWarcasketEntombed(pawn))
                    TrackIfGhoul(pawn);
            }

            Log.Message($"[WarcasketStatTuner] Found {trackedGhouls.Count} entombed Ghoul pawn(s) on load.");
        }

        private static void ResolveGhoulPredicate()
        {
            isGhoulProperty = typeof(Pawn).GetProperty(
                "IsGhoul",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (isGhoulProperty?.PropertyType == typeof(bool) &&
                isGhoulProperty.GetGetMethod(true) != null)
            {
                return;
            }
            isGhoulProperty = null;
        }

        private static void PatchFoundryGhoulEntombment(Harmony harmony)
        {
            Assembly warcasketAssembly = FindLoadedAssembly(WarcasketAssemblyName);
            Type foundryType = warcasketAssembly?.GetType(FoundryTypeName, false);
            if (foundryType == null)
                return;

            MethodInfo cannotUseNow = foundryType.GetMethod(
                "CannotUseNowReason",
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance,
                null,
                new[] { typeof(Pawn) },
                null);
            MethodInfo finishProject = foundryType.GetMethod(
                "FinishWarCasketProject",
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            occupantField = FindField(foundryType, "occupant");
            compPowerField = FindField(foundryType, "compPower");

            Type piratesModType = warcasketAssembly.GetType(PiratesModTypeName, false);
            armorDefsField = FindStaticField(piratesModType, "allArmorDefs");
            shoulderDefsField = FindStaticField(piratesModType, "allShoulderPadsDefs");
            helmetDefsField = FindStaticField(piratesModType, "allHelmetDefs");

            MethodInfo eligibilityPostfix = typeof(GhoulWarcasketIntegration).GetMethod(
                nameof(FoundryCannotUseNowReasonPostfix),
                BindingFlags.NonPublic | BindingFlags.Static);
            if (cannotUseNow != null)
                TryPatch(harmony, cannotUseNow, eligibilityPostfix);

            MethodInfo finishPrefix = typeof(GhoulWarcasketIntegration).GetMethod(
                nameof(FoundryFinishPrefix),
                BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo finishPostfix = typeof(GhoulWarcasketIntegration).GetMethod(
                nameof(FoundryFinishPostfix),
                BindingFlags.NonPublic | BindingFlags.Static);
            if (finishProject != null && occupantField != null)
            {
                try
                {
                    harmony.Patch(
                        finishProject,
                        prefix: new HarmonyMethod(finishPrefix),
                        postfix: new HarmonyMethod(finishPostfix));
                }
                catch (Exception exception)
                {
                    Log.Warning("[WarcasketStatTuner] Ghoul tracking at foundry completion was not installed: " +
                        exception.Message);
                }
            }
        }

        private static Assembly FindLoadedAssembly(string assemblyName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => string.Equals(
                    assembly.GetName().Name,
                    assemblyName,
                    StringComparison.Ordinal));
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(
                    name,
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field;
            }

            return null;
        }

        private static FieldInfo FindStaticField(Type type, string name)
        {
            return type?.GetField(
                name,
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static);
        }

        private static bool HasResearchedWarcasket(FieldInfo defsField)
        {
            if (defsField?.GetValue(null) is not IEnumerable defs)
                return false;

            foreach (object item in defs)
            {
                if (item is ThingDef def && def.IsResearchFinished)
                    return true;
            }

            return false;
        }

        private static void FoundryCannotUseNowReasonPostfix(
            object __instance,
            Pawn selPawn,
            ref string __result)
        {
            if (!string.Equals(
                    __result,
                    "VFEP.ChildrenCannotUseThis".Translate().ToString(),
                    StringComparison.Ordinal) ||
                selPawn == null ||
                !CheckGhoulStatus(selPawn))
            {
                return;
            }

            object powerComp = compPowerField?.GetValue(__instance);
            PropertyInfo powerOnProperty = powerComp?.GetType().GetProperty(
                "PowerOn",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (powerOnProperty == null ||
                !(powerOnProperty.GetValue(powerComp, null) is bool powerOn) ||
                !powerOn)
            {
                __result = "NoPower".Translate().ToString();
                return;
            }

            if (!HasResearchedWarcasket(armorDefsField) ||
                !HasResearchedWarcasket(shoulderDefsField) ||
                !HasResearchedWarcasket(helmetDefsField))
            {
                __result = "VFEP.NoWarcaskets".Translate().ToString();
                return;
            }

            __result = null;
        }

        private static void FoundryFinishPrefix(object __instance, out Pawn __state)
        {
            __state = occupantField?.GetValue(__instance) as Pawn;
        }

        private static void FoundryFinishPostfix(Pawn __state)
        {
            TrackCompletedGhoul(__state);
        }

        private static void TrackCompletedGhoul(Pawn pawn)
        {
            if (pawn == null || !HasGhoulPredicate)
                return;

            if (!CheckGhoulStatus(pawn))
            {
                trackedGhouls.Remove(pawn);
                return;
            }

            trackedGhouls.Add(pawn);
            WarcasketShieldPatch.RefreshGhoulShieldComponents(pawn);
        }

        private static void PatchPawnGizmos(Harmony harmony)
        {
            MethodInfo target = typeof(Pawn).GetMethod(
                "GetGizmos",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            if (target == null ||
                !typeof(IEnumerable<Gizmo>).IsAssignableFrom(target.ReturnType))
            {
                Log.Error("[WarcasketStatTuner] Could not find Pawn.GetGizmos() for Ghoul ability and apparel Gizmo routing.");
                return;
            }

            MethodInfo postfix = typeof(GhoulWarcasketIntegration).GetMethod(
                nameof(PawnGetGizmosPostfix),
                BindingFlags.NonPublic | BindingFlags.Static);
            TryPatch(harmony, target, postfix);
        }

        private static void PawnGetGizmosPostfix(
            Pawn __instance,
            ref IEnumerable<Gizmo> __result)
        {
            if (IsTrackedGhoul(__instance))
                __result = IncludeGhoulWarcasketGizmos(__result, __instance);
        }

        private static IEnumerable<Gizmo> IncludeGhoulWarcasketGizmos(
            IEnumerable<Gizmo> original,
            Pawn pawn)
        {
            var gizmos = original?.ToList() ?? new List<Gizmo>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (Gizmo gizmo in gizmos)
                AddGizmoKey(gizmo, seen);

            foreach (Gizmo gizmo in gizmos)
                yield return gizmo;

            if (pawn.apparel?.WornApparel == null)
                yield break;

            List<Apparel> warcasketApparel = pawn.apparel.WornApparel
                .Where(apparel => apparel?.def != null && DefCache.IsWarcasket(apparel.def))
                .ToList();
            if (warcasketApparel.Count == 0)
                yield break;

            if (pawn.abilities != null)
            {
                foreach (Gizmo gizmo in pawn.abilities.GetGizmos())
                {
                    if (AddGizmoKey(gizmo, seen))
                        yield return gizmo;
                }
            }

            foreach (Apparel apparel in warcasketApparel)
            {
                foreach (Gizmo gizmo in apparel.GetWornGizmos())
                {
                    if (AddGizmoKey(gizmo, seen))
                        yield return gizmo;
                }
            }
        }

        private static bool AddGizmoKey(Gizmo gizmo, HashSet<string> seen)
        {
            if (gizmo == null)
                return false;

            string gizmoType = gizmo.GetType().FullName ?? gizmo.GetType().Name;
            string label = GetGizmoLabel(gizmo);
            if (string.IsNullOrEmpty(label))
            {
                ThingComp sourceComp = GetGizmoComp(gizmo);
                if (sourceComp == null)
                    return true;

                label = RuntimeHelpers.GetHashCode(sourceComp).ToString();
            }

            string key = gizmoType + "\0" + label;
            return seen.Add(key);
        }

        private static string GetGizmoLabel(Gizmo gizmo)
        {
            for (Type type = gizmo.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo labelField = type.GetField(
                    "defaultLabel",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                    BindingFlags.DeclaredOnly);
                if (labelField?.FieldType == typeof(string))
                    return (string)labelField.GetValue(gizmo) ?? string.Empty;
            }

            return string.Empty;
        }

        private static ThingComp GetGizmoComp(Gizmo gizmo)
        {
            for (Type type = gizmo.GetType(); type != null; type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(
                             BindingFlags.Public | BindingFlags.NonPublic |
                             BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (!typeof(ThingComp).IsAssignableFrom(field.FieldType))
                        continue;

                    ThingComp comp = field.GetValue(gizmo) as ThingComp;
                    if (comp != null)
                        return comp;
                }
            }

            return null;
        }

        private static void PatchMoveSpeed(Harmony harmony)
        {
            MethodInfo target = typeof(StatExtension).GetMethod(
                "GetStatValue",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(Thing), typeof(StatDef), typeof(bool), typeof(int) },
                null);
            if (target == null ||
                !target.IsStatic ||
                target.DeclaringType != typeof(StatExtension))
            {
                Log.Error("[WarcasketStatTuner] Could not find the declared static " +
                    "StatExtension.GetStatValue(Thing, StatDef, bool, int) method.");
                return;
            }

            MethodInfo postfix = typeof(GhoulWarcasketIntegration).GetMethod(
                nameof(MoveSpeedPostfix),
                BindingFlags.NonPublic | BindingFlags.Static);
            TryPatch(harmony, target, postfix);
        }

        private static void PatchWorkEligibility(Harmony harmony)
        {
            MethodInfo target = typeof(Pawn).GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(method =>
                {
                    if (method.Name != "WorkTypeIsDisabled" ||
                        method.ReturnType != typeof(bool))
                    {
                        return false;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    return parameters.Length == 1 &&
                        parameters[0].ParameterType == typeof(WorkTypeDef);
                });
            if (target == null)
            {
                Log.Error("[WarcasketStatTuner] Could not find Pawn.WorkTypeIsDisabled(WorkTypeDef); " +
                    "the Ghoul work safeguard and work-type checklist will not be applied.");
                return;
            }

            MethodInfo postfix = typeof(GhoulWarcasketIntegration).GetMethod(
                nameof(PawnWorkTypeIsDisabledPostfix),
                BindingFlags.NonPublic | BindingFlags.Static);
            try
            {
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                workEligibilityPatchInstalled = true;
            }
            catch (Exception exception)
            {
                Log.Error("[WarcasketStatTuner] Could not install the Ghoul work safeguard and " +
                    "work-type checklist: " + exception);
            }
        }

        private static void PawnWorkTypeIsDisabledPostfix(
            Pawn __instance,
            object[] __args,
            ref bool __result)
        {
            if (__instance == null ||
                __args == null ||
                __args.Length == 0 ||
                !(__args[0] is WorkTypeDef workType))
            {
                return;
            }

            if (IsTrackedGhoul(__instance))
            {
                __result = true;
                return;
            }

            ModSettingsData settings = WarcasketTunerMod.Settings;
            if (!workEligibilityPatchInstalled ||
                settings == null ||
                !settings.EnableWarcasketWork ||
                !WearsWarcasket(__instance))
            {
                return;
            }

            if (!settings.EnabledWarcasketWorkTypes.Contains(workType.defName))
                __result = true;
        }

        private static void TryPatch(Harmony harmony, MethodBase target, MethodInfo postfix)
        {
            try
            {
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            }
            catch (Exception exception)
            {
                Log.Warning($"[WarcasketStatTuner] Optional Ghoul integration patch " +
                    $"{target.DeclaringType?.FullName}.{target.Name} was not installed: {exception.Message}");
            }
        }

        private static void MoveSpeedPostfix(object[] __args, ref float __result)
        {
            ModSettingsData settings = WarcasketTunerMod.Settings;
            if (settings == null ||
                __args == null ||
                __args.Length < 2 ||
                !ReferenceEquals(__args[1], StatDefOf.MoveSpeed) ||
                !(__args[0] is Pawn pawn) ||
                pawn == null)
            {
                return;
            }

            if (IsTrackedGhoul(pawn))
            {
                if (settings.TuneGhoulMoveSpeed)
                    __result = settings.GhoulMoveSpeed;
                return;
            }

            if (settings.TuneWarcasketMoveSpeed && WearsWarcasket(pawn))
                __result = settings.WarcasketMoveSpeed;
        }

        private static bool WearsWarcasket(Pawn pawn)
        {
            return pawn.apparel?.WornApparel?.Any(apparel =>
                apparel?.def != null && DefCache.IsWarcasket(apparel.def)) == true;
        }

        private static void TrackIfGhoul(Pawn pawn)
        {
            if (pawn == null || !HasGhoulPredicate)
                return;

            bool isGhoul = IsWarcasketEntombed(pawn) && CheckGhoulStatus(pawn);
            if (isGhoul)
            {
                trackedGhouls.Add(pawn);
                WarcasketShieldPatch.RefreshGhoulShieldComponents(pawn);
            }
            else
                trackedGhouls.Remove(pawn);
        }

        private static bool IsWarcasketEntombed(Pawn pawn)
        {
            return pawn?.apparel?.WornApparel != null &&
                pawn.apparel.WornApparel.Any(apparel =>
                    apparel?.def != null && DefCache.IsWarcasket(apparel.def));
        }

        private static bool HasGhoulPredicate =>
            isGhoulProperty?.PropertyType == typeof(bool);

        private static bool CheckGhoulStatus(Pawn pawn)
        {
            try
            {
                return (bool)isGhoulProperty.GetValue(pawn, null);
            }
            catch (TargetInvocationException exception)
            {
                if (!predicateFailureLogged)
                {
                    Log.Error("[WarcasketStatTuner] Ghoul status check failed: " +
                        exception.InnerException?.Message);
                    predicateFailureLogged = true;
                }

                return false;
            }
        }

    }

    public sealed class GhoulWarcasketGameComponent : GameComponent
    {
        public GhoulWarcasketGameComponent(Game game)
        {
        }

        public override void FinalizeInit()
        {
            GhoulWarcasketIntegration.ScanLoadedPawns();
        }
    }
}
