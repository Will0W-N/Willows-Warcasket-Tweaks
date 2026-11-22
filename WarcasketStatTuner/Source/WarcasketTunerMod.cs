using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace WarcasketStatTuner
{
    public class WarcasketTunerMod : Mod
    {
        private enum SettingsTab
        {
            General,
            Armor,
            Melee,
            Environment,
            Work,
            Ghouls
        }

        private class RecommendedStats
        {
            public float Sharp;
            public float Blunt;
            public float? ShieldMax;
            public float? ShieldRecharge;
        }

        private static readonly string[] PartWords =
        {
            "Warcasket", "Apparel", "Bodysuit", "BodySuit", "Shoulders",
            "Shoulder", "Helmet", "Helm", "Headgear", "Armor", "Body", "Suit",
            "VFEP", "WCE", "AOBA", "Eventide", "Shipcracker"
        };

        public static ModSettingsData Settings;

        private const int TabCount = 6;
        private const float PageMargin = 16f;
        private const float ScrollbarWidth = 16f;
        private const float RecommendedArmorMultiplier = 1.35f;

        private readonly List<TabRecord> tabs = new List<TabRecord>(TabCount);
        private readonly Vector2[] tabScroll = new Vector2[TabCount];
        private readonly float[] tabContentHeight = new float[TabCount];
        private readonly Listing_Standard listing = new Listing_Standard();
        private SettingsTab currentTab = SettingsTab.General;
        private bool tabsBuilt;
        private bool ghoulTabBuilt;
        private string searchText = string.Empty;
        private readonly Dictionary<string, string> textBuffers = new Dictionary<string, string>();
        private readonly HashSet<string> expandedSets = new HashSet<string>();
        private readonly HashSet<string> expandedVariants = new HashSet<string>();

        public WarcasketTunerMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<ModSettingsData>();
            Settings.InitializeWarcasketWorkDefaults();
            var harmony = new Harmony("WarcasketStatTuner");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            try
            {
                WarcasketMeleePatch.PatchCombatExtended(harmony);
            }
            catch (Exception exception)
            {
                Log.Error("[WarcasketStatTuner] Could not patch Combat Extended unarmed melee damage. " +
                    exception);
            }

            try
            {
                WarcasketShieldPatch.Initialize(harmony);
            }
            catch (Exception exception)
            {
                Log.Error("[WarcasketStatTuner] Could not patch VFE shield capacity calculation. " +
                    exception);
            }

            try
            {
                GhoulWarcasketIntegration.Initialize(harmony);
            }
            catch (Exception exception)
            {
                Log.Error("[WarcasketStatTuner] Ghoul integration failed to initialize; the main settings remain available. " +
                    exception);
            }

            try
            {
                WarcasketLoadoutPreview.Initialize(harmony);
            }
            catch (Exception exception)
            {
                Log.Error("[WarcasketStatTuner] Could not initialize the VFE Pirates loadout preview: " +
                    exception);
            }

            LongEventHandler.ExecuteWhenFinished(delegate
            {
                WarcasketUtilitySettings.ApplySettings(Settings);
                StatPatcher.ApplyAllSettings();
            });
        }

        public override string SettingsCategory()
        {
            return "Warcasket Stat Tuner";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            EnsureTabs();

            Rect body = new Rect(inRect);
            float bodyHeight = Mathf.Max(60f, body.height - TabDrawer.TabHeight);
            Rect sectionRect = new Rect(
                body.x,
                body.yMax - bodyHeight,
                body.width,
                bodyHeight);

            Widgets.DrawMenuSection(sectionRect);
            TabDrawer.DrawTabs(sectionRect, tabs);
            DrawTabBody(sectionRect.ContractedBy(PageMargin));
        }

        private void EnsureTabs()
        {
            bool ghoulModDetected = GhoulWarcasketIntegration.IsAvailable;
            if (tabsBuilt && ghoulTabBuilt == ghoulModDetected)
                return;

            tabsBuilt = true;
            ghoulTabBuilt = ghoulModDetected;
            tabs.Clear();
            tabs.Add(new TabRecord("General", delegate { currentTab = SettingsTab.General; },
                () => currentTab == SettingsTab.General));
            tabs.Add(new TabRecord("Armor", delegate { currentTab = SettingsTab.Armor; },
                () => currentTab == SettingsTab.Armor));
            tabs.Add(new TabRecord("Melee", delegate { currentTab = SettingsTab.Melee; },
                () => currentTab == SettingsTab.Melee));
            tabs.Add(new TabRecord("Environment", delegate { currentTab = SettingsTab.Environment; },
                () => currentTab == SettingsTab.Environment));
            tabs.Add(new TabRecord("Work", delegate { currentTab = SettingsTab.Work; },
                () => currentTab == SettingsTab.Work));
            if (ghoulModDetected)
            {
                tabs.Add(new TabRecord("Ghouls", delegate { currentTab = SettingsTab.Ghouls; },
                    () => currentTab == SettingsTab.Ghouls));
            }
            else if (currentTab == SettingsTab.Ghouls)
            {
                currentTab = SettingsTab.General;
            }
        }

        private void DrawTabBody(Rect outRect)
        {
            int index = (int)currentTab;
            Rect viewRect = new Rect(
                0f,
                0f,
                outRect.width - ScrollbarWidth,
                Mathf.Max(tabContentHeight[index], outRect.height));

            Widgets.BeginScrollView(outRect, ref tabScroll[index], viewRect);
            listing.maxOneColumn = true;
            listing.Begin(viewRect);

            switch (currentTab)
            {
                case SettingsTab.General:
                    DrawGlobalSettings(listing);
                    break;
                case SettingsTab.Armor:
                    DrawWarcasketSettings(listing);
                    break;
                case SettingsTab.Melee:
                    DrawMeleeSettings(listing);
                    break;
                case SettingsTab.Environment:
                    DrawEnvironmentSettings(listing);
                    break;
                case SettingsTab.Work:
                    DrawWorkSettings(listing);
                    break;
                case SettingsTab.Ghouls:
                    DrawGhoulSettings(listing);
                    break;
            }

            listing.End();
            if (Event.current.type == EventType.Layout)
                tabContentHeight[index] = listing.CurHeight;

            Widgets.EndScrollView();
        }

        private void DrawGlobalSettings(Listing_Standard listing)
        {
            listing.Label("<b>Global Multipliers</b>");
            listing.GapLine();

            listing.Label($"Sharp Rating: {Settings.SharpMultiplier:F2}x");
            Settings.SharpMultiplier = listing.Slider(Settings.SharpMultiplier, 0.25f, 3.0f);

            listing.Label($"Blunt Rating: {Settings.BluntMultiplier:F2}x");
            Settings.BluntMultiplier = listing.Slider(Settings.BluntMultiplier, 0.25f, 3.0f);

            listing.Label($"Barrier Max Capacity: {Settings.ShieldMaxMultiplier:F2}x");
            Settings.ShieldMaxMultiplier = listing.Slider(Settings.ShieldMaxMultiplier, 0.25f, 3.0f);

            listing.Label($"Barrier Recharge Rate: {Settings.ShieldRechargeMultiplier:F2}x");
            Settings.ShieldRechargeMultiplier = listing.Slider(Settings.ShieldRechargeMultiplier, 0.25f, 3.0f);

            listing.Gap(10f);
            if (ButtonWithTooltip(
                    listing,
                    "Reset All to Default",
                    "Restore global armor and shield multipliers, reset regular warcasket unarmed damage, and clear per-item overrides."))
            {
                Settings.SharpMultiplier = 1f;
                Settings.BluntMultiplier = 1f;
                Settings.ShieldMaxMultiplier = 1f;
                Settings.ShieldRechargeMultiplier = 1f;
                Settings.TuneUnarmedDamage = true;
                Settings.UnarmedBluntDamage = 19f;
                Settings.UnarmedSharpDamage = 4f;
                Settings.Overrides.Clear();
                textBuffers.Clear();
                StatPatcher.ApplyAllSettings();
            }

            listing.Gap(18f);
        }

        private void DrawMeleeSettings(Listing_Standard listing)
        {
            listing.Label("<b>Unarmed Damage for Warcasket Wearers</b>");
            listing.GapLine();
            DrawLabeledToggle(listing, "Replace unarmed hit damage", ref Settings.TuneUnarmedDamage);
            DrawOverrideField(listing, "unarmed_blunt", ref Settings.UnarmedBluntDamage, 0f, 500f, "Blunt damage per hit");
            DrawOverrideField(listing, "unarmed_sharp", ref Settings.UnarmedSharpDamage, 0f, 500f, "Sharp damage per hit");
            listing.Gap(8f);
            if (ButtonWithTooltip(
                    listing,
                    "Reset to default",
                    "Restore the default warcasket wearer unarmed damage settings."))
            {
                Settings.TuneUnarmedDamage = true;
                Settings.UnarmedBluntDamage = 19f;
                Settings.UnarmedSharpDamage = 4f;
                textBuffers.Remove("unarmed_blunt");
                textBuffers.Remove("unarmed_sharp");
            }

            listing.Label("These values replace each unarmed hit for pawns wearing detected warcasket apparel.");
        }

        private void DrawGhoulSettings(Listing_Standard listing)
        {
            listing.Label("<b>Ghoul Melee Damage</b>");
            listing.GapLine();
            DrawLabeledToggle(listing, "Replace Ghoul unarmed hit damage", ref Settings.TuneGhoulUnarmedDamage);
            DrawOverrideField(listing, "ghoul_unarmed_blunt", ref Settings.GhoulUnarmedBluntDamage, 0f, 500f, "Blunt damage per hit");
            DrawOverrideField(listing, "ghoul_unarmed_sharp", ref Settings.GhoulUnarmedSharpDamage, 0f, 500f, "Sharp damage per hit");

            listing.Gap(12f);
            listing.Label("<b>Ghoul Movement Speed</b>");
            listing.GapLine();
            DrawLabeledToggle(listing, "Adjust Ghoul movement speed", ref Settings.TuneGhoulMoveSpeed);
            DrawFloatField(
                listing,
                "ghoul_move_speed",
                ref Settings.GhoulMoveSpeed,
                0.1f,
                20f,
                "Ghoul walk speed (cells per second)");

            listing.Gap(8f);
            if (ButtonWithTooltip(
                    listing,
                    "Reset ALL Ghoul Settings to Defaults",
                    "Restore Ghoul unarmed damage and walk speed settings."))
            {
                Settings.TuneGhoulUnarmedDamage = true;
                Settings.GhoulUnarmedBluntDamage = 19f;
                Settings.GhoulUnarmedSharpDamage = 4f;
                Settings.TuneGhoulMoveSpeed = true;
                Settings.GhoulMoveSpeed = 4.6f;
                textBuffers.Remove("ghoul_unarmed_blunt");
                textBuffers.Remove("ghoul_unarmed_sharp");
                textBuffers.Remove("ghoul_move_speed");
            }

            listing.Label("These settings apply to tracked Ghouls wearing detected warcasket apparel. Ghouls remain unable to work even when warcasket work is enabled.");
        }

        private void DrawEnvironmentSettings(Listing_Standard listing)
        {
            listing.Label("<b>Warcasket Temperature</b>");
            listing.GapLine();

            bool temperatureEnabled = Settings.EnableTemperatureTweaks;
            DrawLabeledToggle(
                listing,
                "Enable warcasket temperature tweaks",
                ref Settings.EnableTemperatureTweaks);
            bool vacuumEnabled = Settings.EnableVacuumTweaks;
            DrawLabeledToggle(
                listing,
                "Enable Hazard warcasket vacuum resistance",
                ref Settings.EnableVacuumTweaks);
            if (temperatureEnabled != Settings.EnableTemperatureTweaks ||
                vacuumEnabled != Settings.EnableVacuumTweaks)
                ApplyUtilitySettings();

            listing.Label("Applies the former temperature patch values to supported VFE Pirates spacer, Hazard, and Mechlord warcaskets. Hazard vacuum resistance is added when the stat is available.");
            listing.Label("Warcasket helmets block named physical eye irritants. Only spacer/advanced warcasket torsos ignore darkness, fog, and the Vanilla Events Expanded dust-storm sight effect.");
            listing.GapLine();

            if (ButtonWithTooltip(
                    listing,
                    "Reset Environment Options to Defaults",
                    "Enable the integrated warcasket temperature and vacuum-resistance tweaks."))
            {
                Settings.EnableTemperatureTweaks = true;
                Settings.EnableVacuumTweaks = true;
                ApplyUtilitySettings();
            }
        }

        private void DrawWorkSettings(Listing_Standard listing)
        {
            listing.Label("<b>Warcasket Work, Hauling, and Cleaning</b>");
            listing.GapLine();

            bool workEnabled = Settings.EnableWarcasketWork;
            DrawLabeledToggle(
                listing,
                "Enable selected work types for warcasket pawns",
                ref Settings.EnableWarcasketWork);
            if (workEnabled != Settings.EnableWarcasketWork)
                ApplyUtilitySettings();

            listing.Label("Choose which work types non-Ghoul warcasket wearers may perform. A Ghoul wearing a warcasket remains unable to work.");
            listing.Label("Checked jobs are enabled. A pawn's own restrictions still apply.");
            listing.Gap(8f);
            listing.Label("<b>Combat capability</b>");
            bool violenceEnabled = Settings.AllowWarcasketViolence;
            DrawLabeledToggle(
                listing,
                "Allow melee and other violent actions",
                ref Settings.AllowWarcasketViolence);
            if (violenceEnabled != Settings.AllowWarcasketViolence)
                ApplyUtilitySettings();
            listing.Label("Melee is a combat capability, not a work type. This also affects hunting and other work that requires violent actions.");
            listing.GapLine();
            listing.Label("<b>Work types by skill</b>");
            listing.Label("A work type that uses multiple skills appears under each relevant skill; changing either entry changes that job everywhere.");
            listing.Gap(6f);

            IEnumerable<IGrouping<SkillDef, WorkTypeDef>> workGroups =
                DefDatabase<WorkTypeDef>.AllDefsListForReading
                    .Where(def => def.visible)
                    .SelectMany(workType => GetWorkTypeSkills(workType)
                        .Select(skill => new { Skill = skill, WorkType = workType }))
                    .GroupBy(entry => entry.Skill, entry => entry.WorkType)
                    .OrderBy(
                        group => group.Key == null ? "General / unskilled" : group.Key.LabelCap.ToString(),
                        StringComparer.OrdinalIgnoreCase);

            foreach (IGrouping<SkillDef, WorkTypeDef> group in workGroups)
            {
                string category = group.Key == null
                    ? "General / unskilled"
                    : group.Key.LabelCap.ToString();
                Rect headerRect = listing.GetRect(Text.LineHeight + 8f);
                Widgets.DrawBoxSolid(headerRect, new Color(0.16f, 0.18f, 0.2f, 0.9f));
                Widgets.Label(headerRect.ContractedBy(6f, 3f), "<b>" + category + "</b>");
                if (!string.IsNullOrEmpty(group.Key?.description))
                    TooltipHandler.TipRegion(headerRect, group.Key.description);
                listing.Gap(3f);

                foreach (WorkTypeDef workType in group
                    .Distinct()
                    .OrderBy(GetWorkTypeDisplayName, StringComparer.OrdinalIgnoreCase))
                {
                    Rect row = listing.GetRect(Text.LineHeight + 6f);
                    DrawWorkTypeCheckbox(row, workType);
                    DrawWorkTypeSubJobs(listing, workType);
                }

                listing.Gap(7f);
            }

            listing.Gap(8f);
            if (ButtonWithTooltip(
                    listing,
                    "Reset Work Options to Defaults",
                    "Enable the default work list: basic violent actions plus Hunting, Warden, Mining, and Hauling."))
            {
                Settings.EnableWarcasketWork = true;
                Settings.SetDefaultWarcasketWorkTypes();
                Settings.WarcasketWorkDefaultsInitialized = true;
                ApplyUtilitySettings();
            }
        }

        private void DrawWorkTypeCheckbox(Rect rect, WorkTypeDef workType)
        {
            bool enabled = Settings.EnabledWarcasketWorkTypes.Contains(workType.defName);
            DrawLabeledToggle(rect, GetWorkTypeDisplayName(workType), ref enabled);
            if (enabled)
            {
                if (!Settings.EnabledWarcasketWorkTypes.Contains(workType.defName))
                    Settings.EnabledWarcasketWorkTypes.Add(workType.defName);
            }
            else
            {
                Settings.EnabledWarcasketWorkTypes.Remove(workType.defName);
            }

            if (!string.IsNullOrEmpty(workType.description))
                TooltipHandler.TipRegion(rect, workType.description);
        }

        private static void DrawWorkTypeSubJobs(Listing_Standard listing, WorkTypeDef workType)
        {
            if (workType.workGiversByPriority == null)
                return;

            foreach (WorkGiverDef workGiver in workType.workGiversByPriority
                .Where(def => def != null)
                .OrderBy(def => def.LabelCap.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                Rect subJobRect = listing.GetRect(Text.LineHeight);
                Rect indentedRect = new Rect(
                    subJobRect.x + 22f,
                    subJobRect.y,
                    Mathf.Max(0f, subJobRect.width - 22f),
                    subJobRect.height);
                string subJobName = string.IsNullOrWhiteSpace(workGiver.label)
                    ? Regex.Replace(workGiver.defName, "(?<=[a-z])(?=[A-Z])", " ")
                    : workGiver.LabelCap.ToString();
                Widgets.Label(indentedRect, "  - " + subJobName);
                if (!string.IsNullOrEmpty(workGiver.description))
                    TooltipHandler.TipRegion(indentedRect, workGiver.description);
            }
        }

        private static IEnumerable<SkillDef> GetWorkTypeSkills(WorkTypeDef workType)
        {
            if (workType.relevantSkills == null || workType.relevantSkills.Count == 0)
            {
                yield return null;
                yield break;
            }

            foreach (SkillDef skill in workType.relevantSkills.Distinct())
                yield return skill;
        }

        private static string GetWorkTypeDisplayName(WorkTypeDef workType)
        {
            if (workType.defName == "BasicWorker")
                return "Basic (unskilled)";

            return workType.labelShort.NullOrEmpty()
                ? workType.LabelCap.ToString()
                : workType.labelShort.CapitalizeFirst();
        }

        private static bool ButtonWithTooltip(
            Listing_Standard listing,
            string label,
            string tooltip)
        {
            Rect rect = listing.GetRect(Text.LineHeight + 8f);
            bool clicked = Widgets.ButtonText(rect, label);
            if (!string.IsNullOrEmpty(tooltip))
                TooltipHandler.TipRegion(rect, tooltip);
            return clicked;
        }

        private static void DrawLabeledToggle(
            Listing_Standard listing,
            string label,
            ref bool value)
        {
            DrawLabeledToggle(listing.GetRect(Text.LineHeight + 4f), label, ref value);
        }

        private static void DrawLabeledToggle(Rect row, string label, ref bool value)
        {
            const float checkboxSize = 24f;
            Rect labelRect = new Rect(row.x, row.y, Mathf.Max(0f, row.width - checkboxSize - 8f), row.height);
            float checkboxY = row.y + (row.height - checkboxSize) * 0.5f;
            float checkboxX = row.xMax - checkboxSize;

            Widgets.Label(labelRect, label);
            Widgets.Checkbox(checkboxX, checkboxY, ref value, checkboxSize, false, true, null, null);
        }

        private void DrawWarcasketSettings(Listing_Standard listing)
        {
            listing.Label("<b>Apply settings to every loaded warcasket</b>");
            if (ButtonWithTooltip(
                    listing,
                    "Apply Suggested Settings to All Warcaskets (1.35x)",
                    "Clear current per-item armor and shield overrides, then set the global sharp and blunt armor multipliers to 1.35x."))
                ApplyArmorProfile(RecommendedArmorMultiplier);

            if (ButtonWithTooltip(
                    listing,
                    "⚠ V.O.I.D. Profile: Reset armor/shield settings, apply 1.75x armor",
                    "Clear current per-item overrides, reset shield multipliers, and apply 1.75x to loaded sharp and blunt armor baselines."))
                ApplyArmorProfile(1.75f);

            if (ButtonWithTooltip(
                    listing,
                    "Restore All Armor/Shield Defaults from Loaded Mods",
                    "Clear all per-item overrides and restore armor and shield multipliers to 1.0x, using each loaded definition's baseline values."))
                ResetArmorSettings();

            listing.Label("Profiles clear existing item overrides and affect every detected warcasket. Restore Loaded Mod Defaults clears overrides and restores each item's original armor and shield values before applying any multiplier.");
            listing.Gap(12f);
            listing.Label("<b>Regular Warcasket Movement Speed</b>");
            listing.GapLine();
            DrawLabeledToggle(
                listing,
                "Set movement speed for non-Ghoul warcasket wearers",
                ref Settings.TuneWarcasketMoveSpeed);
            DrawFloatField(
                listing,
                "warcasket_move_speed",
                ref Settings.WarcasketMoveSpeed,
                0.1f,
                20f,
                "Warcasket walk speed (cells per second)");
            if (ButtonWithTooltip(
                    listing,
                    "Reset walk speed to default (4.6)",
                    "Set regular warcasket walk speed back to 4.6 cells per second without changing whether the speed override is enabled."))
            {
                Settings.WarcasketMoveSpeed = 4.6f;
                textBuffers.Remove("warcasket_move_speed");
            }
            listing.Gap(10f);
            listing.Label("<b>Warcaskets by Mod and Variant</b>");
            listing.GapLine();

            Rect searchRect = listing.GetRect(Text.LineHeight);
            searchText = Widgets.TextField(searchRect, searchText);
            listing.Gap(6f);

            if (DefCache.CachedDefs.Count == 0)
            {
                listing.Label("No warcasket apparel was detected in the loaded mods.");
                return;
            }

            IEnumerable<DefCache.CachedStats> matchingDefs = DefCache.CachedDefs.Values
                .Where(MatchesSearch)
                .OrderBy(cached => cached.Def.modContentPack?.Name ?? "RimWorld")
                .ThenBy(cached => GetVariantName(cached), StringComparer.OrdinalIgnoreCase)
                .ThenBy(cached => cached.Def.LabelCap.ToString(), StringComparer.OrdinalIgnoreCase);

            var sets = matchingDefs.GroupBy(
                cached => cached.Def.modContentPack?.Name ?? "RimWorld",
                StringComparer.OrdinalIgnoreCase);

            bool anyMatch = false;
            foreach (var set in sets)
            {
                anyMatch = true;
                string setKey = set.Key;
                bool setExpanded = !string.IsNullOrWhiteSpace(searchText) || expandedSets.Contains(setKey);
                string setButtonLabel = (setExpanded ? "▼ " : "▶ ") + setKey;
                if (ButtonWithTooltip(
                        listing,
                        setButtonLabel,
                        (setExpanded ? "Collapse" : "Expand") +
                        " warcasket apparel from " + setKey + "."))
                    Toggle(expandedSets, setKey);

                if (!setExpanded)
                {
                    listing.GapLine();
                    continue;
                }

                foreach (var variant in set.GroupBy(
                    GetVariantName,
                    StringComparer.OrdinalIgnoreCase))
                {
                    string variantName = string.IsNullOrEmpty(variant.Key) ? "Standard / Other" : variant.Key;
                    string variantKey = setKey + "|" + variantName;
                    bool variantExpanded =
                        !string.IsNullOrWhiteSpace(searchText) || expandedVariants.Contains(variantKey);

                    listing.Gap(3f);
                    string variantButtonLabel =
                        "    " + (variantExpanded ? "▼ " : "▶ ") + variantName;
                    if (ButtonWithTooltip(
                            listing,
                            variantButtonLabel,
                            (variantExpanded ? "Collapse" : "Expand") +
                            " the " + variantName + " warcasket variant."))
                        Toggle(expandedVariants, variantKey);

                    if (!variantExpanded)
                        continue;

                    foreach (DefCache.CachedStats cached in variant)
                        DrawItemSettings(listing, cached);
                }

                listing.GapLine();
            }

            if (!anyMatch)
                listing.Label("No warcaskets match that search.");
        }

        private void DrawItemSettings(Listing_Standard listing, DefCache.CachedStats cached)
        {
            ThingDef def = cached.Def;
            string itemKey = def.defName;
            bool shielded = cached.HasShield;

            listing.Gap(5f);
            listing.Label($"<b>{def.LabelCap}</b>  ({def.defName})");

            Settings.Overrides.TryGetValue(itemKey, out CustomStatOverride ovr);
            if (ovr == null)
            {
                ovr = new CustomStatOverride();
                Settings.Overrides[itemKey] = ovr;
            }

            if (shielded)
            {
                if (cached.HasShieldMax && !StatPatcher.IsValidShieldOverride(ovr.ShieldMax))
                    ovr.ShieldMax = -1f;
                if (cached.HasShieldRecharge && !IsUsableShieldValue(ovr.ShieldRecharge))
                    ovr.ShieldRecharge = -1f;
            }

            float currentSharp = ovr != null && ovr.Sharp >= 0f
                ? ovr.Sharp
                : cached.OriginalSharp * Settings.SharpMultiplier;
            float currentBlunt = ovr != null && ovr.Blunt >= 0f
                ? ovr.Blunt
                : cached.OriginalBlunt * Settings.BluntMultiplier;
            listing.Label($"Current: Sharp {currentSharp:F1} RHA | Blunt {currentBlunt:F1} MPa");
            listing.Label($"Loaded mod defaults: Sharp {cached.OriginalSharp:F2} RHA | Blunt {cached.OriginalBlunt:F2} MPa");
            RecommendedStats recommendedStats = GetRecommendedStats(cached);
            listing.Label($"Recommended ({RecommendedArmorMultiplier:F2}x loaded base): " +
                $"Sharp {recommendedStats.Sharp:F2} RHA | Blunt {recommendedStats.Blunt:F2} MPa");
            if (shielded)
            {
                listing.Label("Current barrier: " + GetShieldSummary(cached, def, false));
                listing.Label("XML/CE default: " + GetShieldSummary(cached, def, true));
            }

            DrawOverrideField(listing, itemKey + "_sharp", ref ovr.Sharp, 0f, 500f, "Sharp RHA override (-1 = auto)");
            DrawOverrideField(listing, itemKey + "_blunt", ref ovr.Blunt, 0f, 500f, "Blunt MPa override (-1 = auto)");

            if (shielded)
            {
                if (cached.HasShieldMax)
                {
                    string label = cached.ShieldMaxIsFactor
                        ? "Barrier Max factor override (-1 = auto)"
                        : "Barrier capacity override, HP (-1 = default)";
                    DrawOverrideField(listing, itemKey + "_shieldMax", ref ovr.ShieldMax, 0f, 10000f, label);
                }

                if (cached.HasShieldRecharge)
                {
                    string label = cached.ShieldRechargeIsOffset
                        ? "Barrier Recharge offset override (-1 = auto)"
                        : "Barrier Recharge rate override (-1 = auto)";
                    DrawOverrideField(listing, itemKey + "_shieldRecharge", ref ovr.ShieldRecharge, 0.001f, 100f, label);
                }
            }

            if (ButtonWithTooltip(
                    listing,
                    "Restore loaded mod armor/shield defaults for this item",
                    "Clear this item's custom values and restore the armor and shield stats loaded from its mod definition."))
            {
                ovr.Sharp = cached.OriginalSharp;
                ovr.Blunt = cached.OriginalBlunt;
                if (cached.HasShieldMax)
                    ovr.ShieldMax = cached.OriginalShieldMax;
                if (cached.HasShieldRecharge)
                    ovr.ShieldRecharge = cached.OriginalShieldRecharge;
                textBuffers.Remove(itemKey + "_sharp");
                textBuffers.Remove(itemKey + "_blunt");
                textBuffers.Remove(itemKey + "_shieldMax");
                textBuffers.Remove(itemKey + "_shieldRecharge");
                StatPatcher.ApplyAllSettings();
            }

            string applyLabel = shielded
                ? "Apply recommended armor and shield stats"
                : "Apply recommended armor stats";
            if (ButtonWithTooltip(
                    listing,
                    applyLabel,
                    "Set this item's sharp and blunt armor to 1.35x its loaded baseline and restore its loaded shield values."))
            {
                ovr.Sharp = recommendedStats.Sharp;
                ovr.Blunt = recommendedStats.Blunt;
                if (recommendedStats.ShieldMax.HasValue)
                    ovr.ShieldMax = recommendedStats.ShieldMax.Value;
                if (recommendedStats.ShieldRecharge.HasValue)
                    ovr.ShieldRecharge = recommendedStats.ShieldRecharge.Value;

                textBuffers.Remove(itemKey + "_sharp");
                textBuffers.Remove(itemKey + "_blunt");
                if (cached.HasShieldMax)
                    textBuffers.Remove(itemKey + "_shieldMax");
                if (cached.HasShieldRecharge)
                    textBuffers.Remove(itemKey + "_shieldRecharge");
                StatPatcher.ApplyAllSettings();
            }

            if (ButtonWithTooltip(
                    listing,
                    "Clear overrides for this item",
                    "Remove this item's custom values so it follows the global armor and shield multipliers."))
            {
                Settings.Overrides.Remove(itemKey);
                textBuffers.Remove(itemKey + "_sharp");
                textBuffers.Remove(itemKey + "_blunt");
                textBuffers.Remove(itemKey + "_shieldMax");
                textBuffers.Remove(itemKey + "_shieldRecharge");
                StatPatcher.ApplyAllSettings();
            }
        }

        private void ApplyArmorProfile(float multiplier)
        {
            ResetArmorSettings(false);
            Settings.SharpMultiplier = multiplier;
            Settings.BluntMultiplier = multiplier;
            StatPatcher.ApplyAllSettings();
        }

        private void ResetArmorSettings(bool apply = true)
        {
            Settings.SharpMultiplier = 1f;
            Settings.BluntMultiplier = 1f;
            Settings.ShieldMaxMultiplier = 1f;
            Settings.ShieldRechargeMultiplier = 1f;
            Settings.Overrides.Clear();
            textBuffers.Clear();
            if (apply)
                StatPatcher.ApplyAllSettings();
        }

        private void DrawFloatField(
            Listing_Standard listing,
            string key,
            ref float value,
            float min,
            float max,
            string label)
        {
            if (!textBuffers.TryGetValue(key, out string buffer))
            {
                buffer = value.ToString("F1");
                textBuffers[key] = buffer;
            }

            Rect row = listing.GetRect(Text.LineHeight);
            Rect labelRect = new Rect(row.x, row.y, row.width * 0.62f, row.height);
            Rect fieldRect = new Rect(row.x + row.width * 0.62f, row.y, row.width * 0.38f, row.height);

            Widgets.Label(labelRect, label);
            Widgets.TextFieldNumeric(fieldRect, ref value, ref buffer, min, max);
            textBuffers[key] = buffer;
        }

        private static void ApplyUtilitySettings()
        {
            WarcasketUtilitySettings.ApplySettings(Settings);
            StatPatcher.ApplyAllSettings();
        }

        private bool MatchesSearch(DefCache.CachedStats cached)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return true;

            string source = cached.Def.modContentPack?.Name ?? "RimWorld";
            string haystack = cached.Def.defName + " " + cached.Def.LabelCap + " " + source;
            return haystack.IndexOf(searchText.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetVariantName(DefCache.CachedStats cached)
        {
            string name = cached.Def.defName;
            string source = cached.Def.modContentPack?.Name ?? string.Empty;

            if (name.StartsWith("MDWC", StringComparison.OrdinalIgnoreCase))
                return "Medic";
            if (name.StartsWith("SCWC", StringComparison.OrdinalIgnoreCase))
                return "Shipcracker";
            if (name.StartsWith("TXWC", StringComparison.OrdinalIgnoreCase))
                return "Toxin";

            name = Regex.Replace(name, "^(MDWC|SCWC|TXWC)", string.Empty, RegexOptions.IgnoreCase);
            foreach (string partWord in PartWords)
                name = Regex.Replace(name, Regex.Escape(partWord), string.Empty, RegexOptions.IgnoreCase);

            name = Regex.Replace(name, "^DVD\\s*", string.Empty, RegexOptions.IgnoreCase);
            name = Regex.Replace(name, "[^A-Za-z0-9]+", " ").Trim();
            if (name.Equals("chin cardboard", StringComparison.OrdinalIgnoreCase))
                return "Cardboard";

            name = Regex.Replace(name, "^OLWC\\s*", string.Empty, RegexOptions.IgnoreCase).Trim();
            if (ContainsAny(source, "expanded") &&
                ContainsAny(name, "guardianx"))
            {
                name = Regex.Replace(name, "GuardianX", "Guardian", RegexOptions.IgnoreCase);
            }

            if (ContainsAny(source, "eventide") && name.Length == 0)
                return "Eventide";
            if (ContainsAny(source, "expanded", "aoba", "warcaskets expanded"))
                name = Regex.Replace(name, "\\s+X$", string.Empty, RegexOptions.IgnoreCase).Trim();
            return name.Length == 0 ? "Standard / Other" : name;
        }

        private static RecommendedStats GetRecommendedStats(DefCache.CachedStats cached)
        {
            return new RecommendedStats
            {
                Sharp = cached.OriginalSharp * RecommendedArmorMultiplier,
                Blunt = cached.OriginalBlunt * RecommendedArmorMultiplier,
                ShieldMax = cached.HasShieldMax ? cached.OriginalShieldMax : (float?)null,
                ShieldRecharge = cached.HasShieldRecharge
                    ? cached.OriginalShieldRecharge
                    : (float?)null
            };
        }

        private static string GetShieldSummary(
            DefCache.CachedStats cached,
            ThingDef def,
            bool recommended)
        {
            var parts = new List<string>();
            if (cached.HasShieldMax)
            {
                float value = recommended
                    ? cached.OriginalShieldMax
                    : StatPatcher.GetEffectiveShieldMax(def);
                string unit = cached.ShieldMaxIsFactor ? " max factor" : " HP max";
                parts.Add($"{value:F0}{unit}");
            }

            if (cached.HasShieldRecharge)
            {
                float value = recommended
                    ? cached.OriginalShieldRecharge
                    : StatPatcher.GetEffectiveShieldRecharge(def);
                string unit = cached.ShieldRechargeIsOffset ? " recharge offset" : "/s recharge";
                parts.Add($"{value:F3}{unit}");
            }

            return string.Join(" | ", parts);
        }

        private static bool IsUsableShieldValue(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool ContainsAny(string value, params string[] terms)
        {
            foreach (string term in terms)
            {
                if (value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private void DrawOverrideField(
            Listing_Standard listing,
            string key,
            ref float value,
            float min,
            float max,
            string label)
        {
            if (!textBuffers.TryGetValue(key, out string buffer))
            {
                buffer = value >= 0f ? value.ToString("F1") : string.Empty;
                textBuffers[key] = buffer;
            }

            Rect row = listing.GetRect(Text.LineHeight);
            Rect labelRect = new Rect(row.x, row.y, row.width * 0.62f, row.height);
            Rect fieldRect = new Rect(row.x + row.width * 0.62f, row.y, row.width * 0.38f, row.height);

            Widgets.Label(labelRect, label);
            Widgets.TextFieldNumeric(fieldRect, ref value, ref buffer, min, max);

            if (string.IsNullOrEmpty(buffer))
                value = -1f;

            textBuffers[key] = buffer;
        }

        private static void Toggle(HashSet<string> expanded, string key)
        {
            if (!expanded.Add(key))
                expanded.Remove(key);
        }

        public override void WriteSettings()
        {
            WarcasketUtilitySettings.ApplySettings(Settings);
            StatPatcher.ApplyAllSettings();
            base.WriteSettings();
        }
    }
}
