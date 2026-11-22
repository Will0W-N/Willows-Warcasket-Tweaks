# Warcasket Stat Tuner

A RimWorld mod that adjusts warcasket apparel armor and shield stats through global multipliers and per-item overrides. It also optionally replaces unarmed damage for pawns wearing detected warcasket apparel in vanilla and Combat Extended melee combat.

## Version 1.1.0

Compared with version 1.0.1, 1.1.0 adds:

- Built-in Ghoul detection and Warcasket compatibility, with the features togglable through the mod settings.
- Built-in Warcasket compatibility for Ghouls, including Ghoul unarmed damage and movement options.
- Temperature and vacuum-resistance settings for detected Warcaskets.
- Spacer-tier Warcaskets with advanced sensors are no longer affected by darkness, fog, sandstorms, or similar visibility hazards.
- Warcasket helmets completely block the rare combat eye Hediffs for gravel, sand, mud, dirt, dust, and other debris.
- Fixed a major combat bug that could permanently change certain pawns' base RHA and MPA values to the values for wearing no armor, even after the Warcasket was removed.
- Fixed a bug that could make Warcaskets with shields randomly disappear from the mod settings, preventing their shield stats from being modified.
- V.O.I.D. specific preset options that make Warcaskets more usable for players using the V.O.I.D. faction mod.
- A Work tab that lets you enable or disable individual jobs while pawns are in a casket. Jobs are organized beneath relevant skills, with descriptions and individual work-giver tasks.
- Reset buttons and other small usability improvements for restoring defaults and applying settings.
- Improved armor, shield, speed, environment, and foundry-loadout controls.

The Ghouls settings tab lets you configure Ghoul unarmed damage and set Ghoul walk speed directly in cells per second. The Armor tab has a separate optional speed setting for non-Ghoul warcasket wearers. With Anomaly's Ghoul predicate available, Ghouls can use the VFE Pirates Warcasket Foundry; the Harmony patch bypasses only the foundry's developmental-stage restriction for Ghouls and leaves its power and research checks intact. Ghoul status is checked when validating the foundry option, after foundry completion, and when a save initializes for pawns already wearing warcasket apparel. It is never checked in a tick loop. Ghoul combat and movement settings are applied only to pawns tracked at save load or foundry completion. Shield stats and VFE shield-component caches are refreshed at those same tracking events. A selected tracked Ghoul also receives the pawn ability Gizmos and the worn-apparel Gizmos required for warcasket abilities and VFE shield energy bars. Ghoul settings default to 19 blunt / 4 sharp and 4.6 cells per second. The supplied Entomb Your Ghoul mod folder contains only a compiled DLL, not source, so this implementation recreates foundry access and Ghoul stat integration without copying its binary.

When Vanilla Factions Expanded - Pirates is loaded, its warcasket customization dialog includes a live loadout preview. The preview reads the currently selected torso, shoulder, and helmet definitions, uses the tuner’s effective armor and shield settings, and shows CE bulk stats or vanilla mass without changing any definitions. Recommended armor values are 1.35 times each loaded definition's armor baseline, including Combat Extended patches when active. Shield capacity settings and previews use displayed hitpoints; stat-based VEF shield capacity is converted from its internal stat by multiplying by 100. The VEF capacity patch bypasses apparel factor stacking for tuned warcasket shields, while leaving original shield definitions and their offsets intact. Shield recharge values use their XML rate units. The settings page shows each piece's loaded default values and leaves pieces without shield stats unconfigured.

The in-game options menu uses RimWorld's vanilla `Mod` settings UI (`Listing_Standard`, `TabDrawer`, and `Widgets`), rather than a CE- or VFE-specific menu API. See [MOD-OPTIONS-MENU-GUIDE.md](MOD-OPTIONS-MENU-GUIDE.md) for the implementation pattern used here.

The Armor tab can restore armor and shield values from the loaded apparel definitions, restore one item's loaded defaults, apply the 1.35x suggested armor profile to all detected warcasket apparel, or reset armor settings and apply a 1.75x V.O.I.D. profile. Environment settings no longer change sharp or blunt armor, so armor defaults remain the values supplied by the loaded warcasket mods. Environmental vision protection is tiered from each worn warcasket torso: spacer tech level, advanced/spacer research prerequisites, or matching apparel tags qualify for advanced optics. Advanced shells ignore darkness and weather accuracy penalties and the Vanilla Events Expanded dust-storm Sight hediff; base shells receive no global weather immunity. A warcasket helmet prevents the Core `SandInEyes`, `DirtInEyes`, `MudInEyes`, `GravelInEyes`, and `WaterInEyes` hediffs and suppresses the matching `SandInEyes` combat-log text/icon. The Environment tab also includes temperature and available vacuum-resistance tweaks, with setting labels aligned left and toggles at the row's right edge. The separate Work tab groups jobs beneath every relevant in-game skill, with each skill and job on its own full-width row; jobs using multiple skills appear in each applicable section, and changing a checkbox updates that job everywhere. Each job's individual work-giver tasks are listed beneath its toggle. Basic tasks are labeled “Basic (unskilled)”. Skill groups and job descriptions have hover tooltips. By default, the Hunting (Shooting), Warden (Social), Mining, and Hauling work types and the separate melee/violent action capability are enabled for regular warcasket wearers. Regular warcasket movement speed can be restored to its 4.6 cells/second default without changing the speed override toggle. Tracked Ghouls remain unable to work regardless of those settings, and each pawn's own work incapabilities remain in effect. These options can be toggled in-game.

## Compatibility

Version 1.1.0 is incompatible with the following standalone patches because their features are now built into this mod:

- [Farhan's Warcasket Tweaks (Vacuum and Temperature)](https://steamcommunity.com/sharedfiles/filedetails/?id=3533261706)
- [Entomb your ghoul - in Warcasket](https://steamcommunity.com/sharedfiles/filedetails/?id=3547042639)
- [Warcaskets can haul!](https://steamcommunity.com/sharedfiles/filedetails/?id=2730543694)
- [VFE Pirates - Hardworking Warcaskets](https://steamcommunity.com/sharedfiles/filedetails/?id=3535194807)
- [Warcaskets can work!](https://steamcommunity.com/sharedfiles/filedetails/?id=2730151836)
- [Warcaskets temperature patch](https://steamcommunity.com/sharedfiles/filedetails/?id=3443266981)

Version 1.1.0 is not explicitly tested or guaranteed compatible with:

- [Warcaskets: Adeptus Astartes (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3486910367)
- [Ancient Arsenal: Warcaskets (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3576420572)

Use this mod with those two addons at your own risk.

The project has separate build targets for RimWorld 1.6 and 1.7. Both builds are still a work in progress but have been tested.