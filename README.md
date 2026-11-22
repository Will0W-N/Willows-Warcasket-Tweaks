# Willow's Warcasket Tweaks

A centralized configuration tool for **RimWorld warcaskets**, bringing armor customization, barrier adjustments, melee tuning, environmental protection, work-type management, and Anomaly Ghoul integration into one mod.

Designed to replace a collection of smaller warcasket utility mods with a single, cohesive settings framework.

---

## Features

### Core Adjustments & Combat

- **Global & Per-Item Stats:** Adjust global multipliers or edit individual apparel values for Sharp armor, Blunt armor, Barrier capacity, and Barrier recharge rate.
- **Melee Overrides:** Configure unarmed Sharp and Blunt melee damage for pawns wearing warcasket apparel.
- **Combat Extended Integration:** Built around Combat Extended, with automatic detection and stat adjustment for warcaskets from Vanilla Factions Expanded – Pirates and supported addon mods.
- **Foundry Loadout Preview:** Adds a live stat preview to the VFE Pirates warcasket customization dialog, displaying effective armor values, shield specifications, CE bulk, or vanilla mass.

### New in Version 1.1.0

#### Anomaly Ghoul Integration

- **Ghoul Entombment:** Allows Anomaly Ghouls to be entombed in VFE Pirates warcaskets through a dedicated Harmony patch.
- **Ghoul Configuration:** Adds separate settings for Ghoul unarmed damage and movement speed.
- **Movement Speed:** Ghoul movement speed defaults to 4.6 cells per second.
- **Work Restrictions:** Tracked Ghouls remain strictly non-working, regardless of warcasket work permissions.

#### Environmental & Vision Protection

- **Tiered Vision Protection:** Spacer and advanced-tier warcaskets ignore darkness, fog, and weather-related accuracy penalties, including dust storms from Vanilla Events Expanded. Base shells do not receive blanket weather immunity.
- **Environmental Eye Protection:** Warcasket helmets completely block environmental eye hediffs caused by sand, dirt, mud, gravel, and water. Corresponding combat-log text and icons are also suppressed.
- **Temperature & Vacuum Resistance:** Includes integrated settings for adjusting warcasket temperature and vacuum resistance.

#### Work-Type Management

- **Comprehensive Work Tab:** Manage warcasket work permissions through a dedicated settings section, with work types organized beneath their relevant skills.
- **Individual Work Controls:** Toggle individual work-giver subtasks and independently configure hauling, cleaning, mining, crafting, and more.
- **Tooltips:** Work permissions include relevant tooltips to clarify their functionality.

#### V.O.I.D. Compatibility

- **V.O.I.D. Preset:** Includes specialized configuration presets intended to make warcaskets more viable when playing with the V.O.I.D. faction mod.

#### Bug Fixes & Usability

- Fixed an issue where base RHA/MPa values could change after removing equipment.
- Fixed shield-tracking bugs.
- Added quick-reset buttons for configuration settings.
- Improved settings layout with neatly aligned UI rows using RimWorld's `Listing_Standard` patterns.

---

## Requirements

### Supported RimWorld Versions

- RimWorld **1.6**
- RimWorld **1.7**

Separate build targets are provided for each version.

### Required Dependencies

| Mod | Package ID |
|---|---|
| Harmony | `brrainz.harmony` |
| Vanilla Factions Expanded – Pirates | `OskarPotocki.VFE.Pirates` |
| Combat Extended | `CETeam.CombatExtended` |

---

## Supported Addons & Auto-Detection

Warcaskets are automatically detected across supported addon mods, including:

- More Warcaskets
- Eventide
- Toxin
- Shipcracker
- Medic
- Overlord
- Cardboard
- WarCasket Expanded
- Mechlord Warcasket

---

## Incompatibilities

**Do not use the following mods or patches alongside Willow's Warcasket Tweaks.**

Version 1.1.0 incorporates and expands upon their functionality natively. Running these alongside this mod may result in overlapping or conflicting features.

- Farhan's Warcasket Tweaks (Vacuum and Temperature)
- Entomb your ghoul - in Warcasket
- Warcaskets can haul!
- Warcaskets can work!
- VFE Pirates - Hardworking Warcaskets
- Warcaskets temperature patch

### Untested Compatibility

The following mods have not been tested. Use at your own risk:

- Warcaskets: Adeptus Astartes (Continued)
- Ancient Arsenal: Warcaskets (Continued)

---

## Settings Menu

The in-game settings menu uses the same format Combat Extended and the Vanilla Expanded series does so its recognizable. 

| Section | Available Settings |
|---|---|
| **General** | Global multipliers for Sharp armor, Blunt armor, Barrier capacity, and Barrier recharge rate. |
| **Armor** | Individual armor adjustments, organized by detected warcasket mods and variants. Includes quick-reset profiles, a suggested balanced **1.35x** profile, and a **1.75x V.O.I.D.** preset. |
| **Melee** | Custom unarmed Sharp and Blunt damage values for human warcasket wearers. |
| **Environment** | Vision protection, temperature resistance, and vacuum resistance settings. |
| **Work** | Work-type permissions organized by skill, with individual controls for relevant work-giver subtasks. |
| **Ghouls** | Ghoul-specific unarmed damage and movement speed configuration. |

---

## Feedback & Bug Reports

Found a bug, encountered a warcasket detection issue, or have an idea for a future feature?

**Discord:** `@willow627`

<img width="467" height="710" alt="image" src="https://github.com/user-attachments/assets/2c00e8f4-dc34-45b1-ba67-72d6633017db" />
<img width="886" height="664" alt="image" src="https://github.com/user-attachments/assets/60b8f460-7b54-4ce6-8f0e-057b0eb49175" />
<img width="884" height="672" alt="image" src="https://github.com/user-attachments/assets/b36ec073-89e5-4418-81c0-da1c18ca2bd7" />
<img width="890" height="663" alt="image" src="https://github.com/user-attachments/assets/91a10ca0-80de-4c1a-846a-5d1b04818098" />
<img width="886" height="676" alt="image" src="https://github.com/user-attachments/assets/75e20c6a-6465-4d1e-bb79-868999b1af56" />
<img width="883" height="671" alt="image" src="https://github.com/user-attachments/assets/54890b34-cb0b-49dd-9a75-7e2be40650a2" />
<img width="879" height="661" alt="image" src="https://github.com/user-attachments/assets/29edc79c-c0f2-40e9-86ef-5ff031d2afe0" />
