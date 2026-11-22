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
    internal static class WarcasketLoadoutPreview
    {
        private const string PiratesAssemblyName = "VFEPirates";
        private const string DialogTypeName = "VFEPirates.Buildings.Dialog_WarcasketCustomization";
        private const string CombatExtendedPackageId = "CETeam.CombatExtended";
        private const float DefaultPanelWidth = 320f;
        private const float PanelGap = 8f;
        private const float PanelPadding = 9f;
        private const float ScreenMargin = 8f;
        private const float PanelHeight = 440f;

        private sealed class PiecePreview
        {
            public ThingDef Def;
            public string Category;
            public float Sharp;
            public float Blunt;
            public float Bulk;
            public float WornBulk;
            public float Mass;
            public bool HasBulk;
            public bool HasWornBulk;
            public bool HasShield;
            public bool HasShieldMax;
            public bool HasShieldRecharge;
            public bool ShieldMaxIsFactor;
            public bool ShieldRechargeIsOffset;
            public float ShieldMax;
            public float ShieldRecharge;
        }

        private sealed class PreviewCache
        {
            public object Window;
            public object Project;
            public Pawn Pawn;
            public ThingDef Armor;
            public ThingDef Shoulders;
            public ThingDef Helmet;
            public bool IsGhoul;
            public bool CombatExtended;
            public float SharpMultiplier;
            public float BluntMultiplier;
            public float ShieldMaxMultiplier;
            public float ShieldRechargeMultiplier;
            public readonly float[] SharpOverrides = new float[3];
            public readonly float[] BluntOverrides = new float[3];
            public readonly float[] ShieldMaxOverrides = new float[3];
            public readonly float[] ShieldRechargeOverrides = new float[3];
            public readonly List<PiecePreview> Pieces = new List<PiecePreview>(3);
            public float TotalBulk;
            public float TotalWornBulk;
        }

        private static FieldInfo projectField;
        private static FieldInfo pawnField;
        private static FieldInfo isArmorField;
        private static FieldInfo isShouldersField;
        private static FieldInfo isHelmetField;
        private static FieldInfo projectArmorField;
        private static FieldInfo projectShouldersField;
        private static FieldInfo projectHelmetField;
        private static FieldInfo windowRectField;
        private static StatDef bulkStat;
        private static StatDef wornBulkStat;
        private static StatDef massStat;
        private static bool combatExtended;
        private static PreviewCache cache;
        private static Vector2 scrollPosition;
        private static WarcasketLoadoutPreviewWindow previewWindow;

        public static void Initialize(Harmony harmony)
        {
            Assembly piratesAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == PiratesAssemblyName);
            Type dialogType = piratesAssembly?.GetType(DialogTypeName, false);
            if (dialogType == null)
                return;

            projectField = FindField(dialogType, "project");
            pawnField = FindField(dialogType, "pawn");

            Type projectType = projectField?.FieldType;
            projectArmorField = FindField(projectType, "armorDef");
            projectShouldersField = FindField(projectType, "shoulderPadsDef");
            projectHelmetField = FindField(projectType, "helmetDef");

            Type warcasketDefType = projectArmorField?.FieldType;
            isArmorField = FindField(warcasketDefType, "isArmor");
            isShouldersField = FindField(warcasketDefType, "isShoulderPads");
            isHelmetField = FindField(warcasketDefType, "isHelmet");

            if (projectField == null ||
                pawnField == null ||
                projectArmorField == null ||
                projectShouldersField == null ||
                projectHelmetField == null ||
                isArmorField == null ||
                isShouldersField == null ||
                isHelmetField == null)
            {
                Log.Error("[WarcasketStatTuner] Could not resolve VFE Pirates customization dialog selection fields.");
                return;
            }

            MethodInfo drawMethod = dialogType.GetMethod(
                "DoWindowContents",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                BindingFlags.DeclaredOnly,
                null,
                new[] { typeof(Rect) },
                null);
            MethodInfo postOpen = typeof(Window).GetMethod(
                "PostOpen",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                BindingFlags.DeclaredOnly);
            MethodInfo postClose = typeof(Window).GetMethod(
                "PostClose",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                BindingFlags.DeclaredOnly);
            windowRectField = FindField(typeof(Window), "windowRect");
            if (drawMethod == null || postOpen == null || postClose == null || windowRectField == null)
            {
                Log.Error("[WarcasketStatTuner] Could not resolve VFE Pirates customization dialog drawing methods.");
                return;
            }

            combatExtended = LoadedModManager.RunningModsListForReading.Any(mod =>
                string.Equals(mod.PackageId, CombatExtendedPackageId, StringComparison.OrdinalIgnoreCase));
            if (combatExtended)
            {
                bulkStat = DefDatabase<StatDef>.GetNamedSilentFail("Bulk");
                wornBulkStat = DefDatabase<StatDef>.GetNamedSilentFail("WornBulk");
            }
            else
            {
                massStat = DefDatabase<StatDef>.GetNamedSilentFail("Mass");
            }

            harmony.Patch(postOpen, postfix: new HarmonyMethod(
                typeof(WarcasketLoadoutPreview), nameof(WindowPostOpenPostfix)));
            harmony.Patch(postClose, postfix: new HarmonyMethod(
                typeof(WarcasketLoadoutPreview), nameof(WindowPostClosePostfix)));
            harmony.Patch(drawMethod, postfix: new HarmonyMethod(
                typeof(WarcasketLoadoutPreview), nameof(DoWindowContentsPostfix)));
            Log.Message("[WarcasketStatTuner] Added live preview to the VFE Pirates warcasket customization dialog.");
        }

        private static FieldInfo FindField(Type type, string name)
        {
            return type?.GetField(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                BindingFlags.Static | BindingFlags.DeclaredOnly);
        }

        private static bool IsCustomizationWindow(Window window)
        {
            return window != null && window.GetType().FullName == DialogTypeName;
        }

        private static void WindowPostOpenPostfix(Window __instance)
        {
            if (IsCustomizationWindow(__instance))
                EnsurePreviewWindow(__instance);
        }

        private static void WindowPostClosePostfix(Window __instance)
        {
            if (IsCustomizationWindow(__instance) &&
                previewWindow != null &&
                ReferenceEquals(previewWindow.ParentWindow, __instance))
            {
                Find.WindowStack.TryRemove(previewWindow, false);
                previewWindow = null;
                cache = null;
            }
        }

        internal static void PreviewWindowClosed(WarcasketLoadoutPreviewWindow window)
        {
            if (!ReferenceEquals(previewWindow, window))
                return;

            previewWindow = null;
            cache = null;
        }

        private static void DoWindowContentsPostfix(Window __instance)
        {
            if (IsCustomizationWindow(__instance) &&
                Find.WindowStack.Windows.Contains(__instance))
                EnsurePreviewWindow(__instance);
        }

        private static void EnsurePreviewWindow(Window parent)
        {
            if (Find.WindowStack.Windows.Contains(previewWindow) &&
                ReferenceEquals(previewWindow.ParentWindow, parent))
            {
                return;
            }

            if (previewWindow != null)
                Find.WindowStack.TryRemove(previewWindow, false);

            previewWindow = new WarcasketLoadoutPreviewWindow(parent, DefaultPanelWidth, PanelHeight);
            Find.WindowStack.Add(previewWindow);
            PositionPreviewWindow(parent, previewWindow);
        }

        private static void PositionPreviewWindow(Window parent, WarcasketLoadoutPreviewWindow panel)
        {
            Rect parentRect = (Rect)windowRectField.GetValue(parent);
            float width = panel.windowRect.width;
            float height = panel.windowRect.height;
            float rightX = parentRect.xMax + PanelGap;
            float leftX = parentRect.xMin - width - PanelGap;
            float maxX = UI.screenWidth - width - ScreenMargin;
            float x;

            if (rightX <= maxX)
            {
                x = rightX;
            }
            else if (leftX >= ScreenMargin)
            {
                x = leftX;
            }
            else
            {
                float roomOnRight = UI.screenWidth - ScreenMargin - rightX;
                float roomOnLeft = parentRect.xMin - PanelGap - ScreenMargin;
                if (roomOnRight >= roomOnLeft && roomOnRight > 0f)
                {
                    width = Mathf.Min(width, roomOnRight);
                    x = rightX;
                }
                else if (roomOnLeft > 0f)
                {
                    width = Mathf.Min(width, roomOnLeft);
                    x = parentRect.xMin - width - PanelGap;
                }
                else
                {
                    Rect movedParent = parentRect;
                    movedParent.x = ScreenMargin;
                    windowRectField.SetValue(parent, movedParent);
                    x = movedParent.xMax + PanelGap;
                    width = Mathf.Min(width, UI.screenWidth - x - ScreenMargin);
                }
            }

            Rect panelRect = panel.windowRect;
            panelRect.width = width;
            panelRect.height = Mathf.Min(height, UI.screenHeight - ScreenMargin * 2f);
            panelRect.x = Mathf.Clamp(x, ScreenMargin, Mathf.Max(ScreenMargin, UI.screenWidth - width - ScreenMargin));
            panelRect.y = Mathf.Clamp(parentRect.y, ScreenMargin, Mathf.Max(ScreenMargin, UI.screenHeight - panelRect.height - ScreenMargin));
            panel.windowRect = panelRect;
        }

        internal static void DrawPreviewWindow(Rect inRect)
        {
            if (previewWindow == null ||
                previewWindow.ParentWindow == null ||
                !Find.WindowStack.Windows.Contains(previewWindow.ParentWindow))
            {
                if (previewWindow != null)
                    Find.WindowStack.TryRemove(previewWindow, false);
                previewWindow = null;
                cache = null;
                return;
            }

            PreviewCache preview = GetPreview(previewWindow.ParentWindow);
            Rect panelRect = new Rect(0f, 0f, inRect.width, inRect.height);
            Widgets.DrawMenuSection(panelRect);

            Rect innerRect = panelRect.ContractedBy(PanelPadding);
            float contentHeight = 54f;
            if (preview.Pawn != null)
                contentHeight += 22f;
            if (HasAnyBulkStat)
                contentHeight += 32f;
            contentHeight += preview.Pieces.Count * 112f;
            contentHeight += preview.Pieces.Count(piece => piece.HasShield) * 22f;
            if (preview.Pieces.Count == 0)
                contentHeight += 36f;

            var viewRect = new Rect(0f, 0f, innerRect.width - 16f, contentHeight);
            Widgets.BeginScrollView(innerRect, ref scrollPosition, viewRect);
            DrawPreviewContents(preview, viewRect.width);
            Widgets.EndScrollView();
        }

        private static PreviewCache GetPreview(object window)
        {
            object project = projectField.GetValue(window);
            Pawn pawn = pawnField.GetValue(window) as Pawn;
            ThingDef armor = project == null ? null : projectArmorField.GetValue(project) as ThingDef;
            ThingDef shoulders = project == null ? null : projectShouldersField.GetValue(project) as ThingDef;
            ThingDef helmet = project == null ? null : projectHelmetField.GetValue(project) as ThingDef;
            ModSettingsData settings = WarcasketTunerMod.Settings;

            if (CacheMatches(
                    cache,
                    window,
                    project,
                    pawn,
                    armor,
                    shoulders,
                    helmet,
                    settings))
            {
                return cache;
            }

            var updated = new PreviewCache
            {
                Window = window,
                Project = project,
                Pawn = pawn,
                Armor = armor,
                Shoulders = shoulders,
                Helmet = helmet,
                IsGhoul = GhoulWarcasketIntegration.IsGhoulPawn(pawn),
                CombatExtended = combatExtended,
                SharpMultiplier = settings?.SharpMultiplier ?? 1f,
                BluntMultiplier = settings?.BluntMultiplier ?? 1f,
                ShieldMaxMultiplier = settings?.ShieldMaxMultiplier ?? 1f,
                ShieldRechargeMultiplier = settings?.ShieldRechargeMultiplier ?? 1f
            };

            ThingDef[] selected = { armor, shoulders, helmet };
            foreach (int i in Enumerable.Range(0, selected.Length))
            {
                ThingDef def = selected[i];
                updated.SharpOverrides[i] = GetOverride(settings, def, true);
                updated.BluntOverrides[i] = GetOverride(settings, def, false);
                updated.ShieldMaxOverrides[i] = GetShieldOverride(settings, def, true);
                updated.ShieldRechargeOverrides[i] = GetShieldOverride(settings, def, false);
                if (def == null)
                    continue;

                var piece = new PiecePreview
                {
                    Def = def,
                    Category = GetCategory(def),
                    Sharp = StatPatcher.GetEffectiveArmorSharp(def),
                    Blunt = StatPatcher.GetEffectiveArmorBlunt(def)
                };

                if (DefCache.CachedDefs.TryGetValue(def.defName, out DefCache.CachedStats cached) &&
                    cached.HasShield)
                {
                    piece.HasShieldMax = cached.HasShieldMax;
                    piece.HasShieldRecharge = cached.HasShieldRecharge;
                    piece.ShieldMaxIsFactor = cached.ShieldMaxIsFactor;
                    piece.ShieldRechargeIsOffset = cached.ShieldRechargeIsOffset;
                    piece.ShieldMax = StatPatcher.GetEffectiveShieldMax(def);
                    piece.ShieldRecharge = StatPatcher.GetEffectiveShieldRecharge(def);
                    piece.HasShield = piece.HasShieldMax || piece.HasShieldRecharge;
                    if (!piece.HasShieldMax)
                        piece.ShieldMax = 0f;
                    if (!piece.HasShieldRecharge)
                        piece.ShieldRecharge = 0f;
                }

                if (combatExtended)
                {
                    piece.HasBulk = bulkStat != null;
                    piece.HasWornBulk = wornBulkStat != null;
                    piece.Bulk = piece.HasBulk ? GetAbstractStat(def, bulkStat) : 0f;
                    piece.WornBulk = piece.HasWornBulk ? GetAbstractStat(def, wornBulkStat) : 0f;
                    updated.TotalBulk += piece.Bulk;
                    updated.TotalWornBulk += piece.WornBulk;
                }
                else
                {
                    piece.Mass = GetAbstractStat(def, massStat);
                }

                updated.Pieces.Add(piece);
            }

            cache = updated;
            return updated;
        }

        private static bool CacheMatches(
            PreviewCache current,
            object window,
            object project,
            Pawn pawn,
            ThingDef armor,
            ThingDef shoulders,
            ThingDef helmet,
            ModSettingsData settings)
        {
            if (current == null ||
                !ReferenceEquals(current.Window, window) ||
                !ReferenceEquals(current.Project, project) ||
                !ReferenceEquals(current.Pawn, pawn) ||
                !ReferenceEquals(current.Armor, armor) ||
                !ReferenceEquals(current.Shoulders, shoulders) ||
                !ReferenceEquals(current.Helmet, helmet) ||
                current.CombatExtended != combatExtended ||
                current.SharpMultiplier != (settings?.SharpMultiplier ?? 1f) ||
                current.BluntMultiplier != (settings?.BluntMultiplier ?? 1f) ||
                current.ShieldMaxMultiplier != (settings?.ShieldMaxMultiplier ?? 1f) ||
                current.ShieldRechargeMultiplier != (settings?.ShieldRechargeMultiplier ?? 1f))
            {
                return false;
            }

            return current.SharpOverrides[0] == GetOverride(settings, armor, true) &&
                current.BluntOverrides[0] == GetOverride(settings, armor, false) &&
                current.ShieldMaxOverrides[0] == GetShieldOverride(settings, armor, true) &&
                current.ShieldRechargeOverrides[0] == GetShieldOverride(settings, armor, false) &&
                current.SharpOverrides[1] == GetOverride(settings, shoulders, true) &&
                current.BluntOverrides[1] == GetOverride(settings, shoulders, false) &&
                current.ShieldMaxOverrides[1] == GetShieldOverride(settings, shoulders, true) &&
                current.ShieldRechargeOverrides[1] == GetShieldOverride(settings, shoulders, false) &&
                current.SharpOverrides[2] == GetOverride(settings, helmet, true) &&
                current.BluntOverrides[2] == GetOverride(settings, helmet, false) &&
                current.ShieldMaxOverrides[2] == GetShieldOverride(settings, helmet, true) &&
                current.ShieldRechargeOverrides[2] == GetShieldOverride(settings, helmet, false);
        }

        private static float GetOverride(ModSettingsData settings, ThingDef def, bool sharp)
        {
            if (settings == null ||
                def == null ||
                !settings.Overrides.TryGetValue(def.defName, out CustomStatOverride ovr) ||
                ovr == null)
            {
                return -1f;
            }

            return sharp ? ovr.Sharp : ovr.Blunt;
        }

        private static float GetShieldOverride(ModSettingsData settings, ThingDef def, bool max)
        {
            if (settings == null ||
                def == null ||
                !settings.Overrides.TryGetValue(def.defName, out CustomStatOverride ovr) ||
                ovr == null)
            {
                return -1f;
            }

            return max ? ovr.ShieldMax : ovr.ShieldRecharge;
        }

        private static float GetAbstractStat(ThingDef def, StatDef stat)
        {
            return stat == null ? 0f : def.GetStatValueAbstract(stat);
        }

        private static string GetCategory(ThingDef def)
        {
            if (isArmorField.GetValue(def) is bool isArmor && isArmor)
                return "Torso";
            if (isShouldersField.GetValue(def) is bool isShoulders && isShoulders)
                return "Shoulders";
            if (isHelmetField.GetValue(def) is bool isHelmet && isHelmet)
                return "Helmet";
            return "Other";
        }

        private static bool HasAnyBulkStat => bulkStat != null || wornBulkStat != null;

        private static void DrawPreviewContents(PreviewCache preview, float width)
        {
            float y = 0f;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, width, 26f), "Loadout Preview");
            y += 28f;

            Text.Font = GameFont.Small;
            if (preview.Pawn != null)
            {
                string target = "Target: " + preview.Pawn.LabelShortCap;
                if (preview.IsGhoul)
                    target += " (Ghoul)";
                Widgets.Label(new Rect(0f, y, width, 22f), target);
                y += 22f;
            }

            if (preview.CombatExtended && HasAnyBulkStat)
            {
                string totals = string.Empty;
                if (bulkStat != null)
                    totals = $"Total Bulk: {preview.TotalBulk:F2}";
                if (wornBulkStat != null)
                {
                    if (totals.Length > 0)
                        totals += "   ";
                    totals += $"Total Worn Bulk: {preview.TotalWornBulk:F2}";
                }

                Widgets.Label(new Rect(0f, y, width, 32f), totals);
                y += 32f;
            }

            if (preview.Pieces.Count == 0)
            {
                Widgets.Label(new Rect(0f, y + 8f, width, 44f), "No warcasket pieces selected.");
                return;
            }

            string sharpUnit = preview.CombatExtended ? "mm RHA" : "%";
            string bluntUnit = preview.CombatExtended ? "MPa" : "%";
            bool hasLoadStats = preview.CombatExtended
                ? HasAnyBulkStat
                : massStat != null;
            foreach (PiecePreview piece in preview.Pieces)
            {
                if (y > 0f)
                    Widgets.DrawLineHorizontal(0f, y + 2f, width);
                y += 9f;

                Widgets.Label(
                    new Rect(8f, y, width - 8f, 22f),
                    $"<b>{piece.Def.LabelCap}</b>  —  {piece.Category}");
                y += 24f;
                Widgets.Label(
                    new Rect(14f, y, width - 14f, 20f),
                    $"Sharp Armor:  {FormatArmor(piece.Sharp, preview.CombatExtended)} {sharpUnit}");
                y += 20f;
                Widgets.Label(
                    new Rect(14f, y, width - 14f, 20f),
                    $"Blunt Armor:  {FormatArmor(piece.Blunt, preview.CombatExtended)} {bluntUnit}");
                y += 20f;

                if (piece.HasShield)
                {
                    var shieldStats = new List<string>();
                    if (piece.HasShieldMax)
                    {
                        string maxLabel = piece.ShieldMaxIsFactor ? "Max factor" : "Max energy";
                        shieldStats.Add(piece.ShieldMaxIsFactor
                            ? $"{maxLabel}: {piece.ShieldMax:F2}"
                            : $"{maxLabel}: {piece.ShieldMax:F0} HP");
                    }

                    if (piece.HasShieldRecharge)
                    {
                        string rechargeLabel = piece.ShieldRechargeIsOffset
                            ? "Recharge offset"
                            : "Recharge";
                        string rechargeValue = piece.ShieldRechargeIsOffset
                            ? piece.ShieldRecharge.ToString("F2")
                            : piece.ShieldRecharge.ToString("F3") + "/s";
                        shieldStats.Add($"{rechargeLabel}: {rechargeValue}");
                    }

                    Widgets.Label(new Rect(14f, y, width - 14f, 20f),
                        "Barrier: " + string.Join("  |  ", shieldStats));
                    y += 22f;
                }

                if (hasLoadStats)
                {
                    string load;
                    if (preview.CombatExtended)
                    {
                        load = string.Empty;
                        if (piece.HasBulk)
                            load = $"Bulk: {piece.Bulk:F2}";
                        if (piece.HasWornBulk)
                        {
                            if (load.Length > 0)
                                load += "   ";
                            load += $"Worn Bulk: {piece.WornBulk:F2}";
                        }
                    }
                    else
                    {
                        load = $"Mass: {piece.Mass:F2} kg";
                    }

                    Widgets.Label(new Rect(14f, y, width - 14f, 20f), load);
                    y += 20f;
                }
            }
        }

        private static string FormatArmor(float value, bool combatExtended)
        {
            return (combatExtended ? value : value * 100f).ToString("F1");
        }

        private static string FormatBulk(float value, bool available)
        {
            return available ? value.ToString("F2") : "n/a";
        }
    }

    internal sealed class WarcasketLoadoutPreviewWindow : Window
    {
        private readonly float initialWidth;
        private readonly float initialHeight;

        public Window ParentWindow { get; }

        public WarcasketLoadoutPreviewWindow(Window parentWindow, float width, float height)
            : base(null)
        {
            ParentWindow = parentWindow;
            initialWidth = width;
            initialHeight = height;
            draggable = true;
            doCloseX = false;
            doCloseButton = false;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
            focusWhenOpened = false;
            closeOnAccept = false;
            closeOnCancel = false;
            closeOnClickedOutside = false;
            forcePause = false;
            drawShadow = true;
        }

        public override Vector2 InitialSize => new Vector2(initialWidth, initialHeight);

        public override void DoWindowContents(Rect inRect)
        {
            WarcasketLoadoutPreview.DrawPreviewWindow(inRect);
        }

        public override void PostClose()
        {
            base.PostClose();
            WarcasketLoadoutPreview.PreviewWindowClosed(this);
        }
    }
}
