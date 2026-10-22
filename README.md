# CraidoZ Tools Core

Unity package that provides editor utilities and code-assist attributes.

Version: 0.0.7

## Features
- Animation Retarget (`CraidoZ Tools/Animation/Animation Retarget`)
  - Separate window with rig selection, bone mapping and review steps
  - Clickable skeletons, searchable target picker and path/name suggestions
  - Manual confirmation, explicit skipping, duplicate-target and parent-chain checks
  - JSON mapping presets tied to source/target model assets
  - In-memory example rigs via **Try example rigs**, without creating assets
  - Generic animation playback, scrubbing and target `.anim` generation
  - Partial mapping preview, independent preview/export looping, and orbit/zoom/pan controls
  - Multiple selection with bulk Skip / Include in the details panel
  - Mesh-only node filtering, per-pair position/scale controls and export diagnostics
- PlayerPref Management window (`CraidoZ Tools/PlayerPref Management`)
  - Set / load / delete PlayerPrefs
  - Type selection (bool, int, float, string)
  - Type mismatch warning
- Prefab Unpack tools (`CraidoZ Tools/Prefab`)
  - Unpack Selected
  - Unpack Selected (Completely)
  - Unpack To New Prefab (window)
- Scene Grid tool (`CraidoZ Tools/Scene Grid`)
  - Toggle grid in Scene View (XY/XZ/YZ plane)
  - Draw grid lines in front of scene objects for consistent visibility
  - Show/Hide Scene View button from CraidoZ menu
  - Configure cell size, range, cell indices, line color, and text color
- ShowIf attributes (CodeAssist)
  - Conditionally show fields in the Inspector based on another field
  - Supports bool, int, float, string, and enum
  - Multiple expected values
  - Comparisons via attributes: ShowIf, ShowIfNot, ShowIfGreater/Less (int/float)

## Installation
Add this repo as a Unity package (Git URL) or copy into `Packages/`.

## Usage

### Animation Retarget
Open `CraidoZ Tools/Animation/Animation Retarget`.

1. Choose source and target model/prefab assets and a Generic source animation clip.
2. Select a source row or click its joint in the skeleton view.
3. Click the corresponding target joint, or use the searchable **Choose target bone** picker.
4. Inspect the highlighted pair and select **Confirm this pair**, or explicitly skip an unused transform.
5. Resolve flagged rows in **Review**. **Suggest remaining pairs**, **Confirm all valid pairs**
   and **Skip unmapped extras** help with similar rigs; inspect the suggestions before confirming.
6. Select **Preview motion**, then play or scrub to inspect source and target motion.
7. In **Review**, choose **Loop exported clip** and, under **Advanced export settings**,
   sample rate and event copying. The **Generate animation clip** button stays at the bottom
   of the window while you scroll. Blockers are shown above; other notes have expandable details.
   Save inside `Assets`. Existing files receive a unique suffix instead of being overwritten.
   Use the resulting clip with an Animator on the target model's root.

Save a JSON mapping preset to reuse the pairing and movement settings. Incomplete presets
are allowed. **Try example rigs** demonstrates mapping without creating assets; export
requires actual models and a clip.

The preview shows transform positions in the supplied reference pose, with an orbit
view plus front, side and top presets. Right-drag (or Alt + left-drag) rotates,
the mouse wheel zooms around the cursor, and middle-drag or Shift + left-drag pans.
No navigation mode switch is needed. Both views move together for comparison;
**Reset view** restores the initial angle and framing.
Left click still selects a source joint or assigns a target joint. When several
joints overlap, a menu lists the nearby joints with their hierarchy paths.
The preview displays animated skeletons; it does not render model meshes. It
can play before mapping is complete, even with no mapped pairs. The source plays
its available Transform tracks; the target plays individually valid pairs without requiring
mapped ancestors or confirmation. Missing/duplicate/invalid assignments are omitted.
Unmapped target joints keep their local reference pose and still follow animated parents.
The preview reports mapped and not-ready counts. Export still requires complete, valid,
confirmed included pairs and matching parent chains; a partial preview cannot be baked directly.
The searchable target picker is also available for dense rigs. Renderer-only transforms are hidden and
skipped by default; choose **All nodes** or **Meshes only** in the list filter to inspect them. A transform used by
a SkinnedMeshRenderer (or its bone ancestors) is kept even when it also has a renderer.
Accessory bones such as hair, whiskers and belts require a pair or an explicit skip.
Check multiple rows (or **Select visible**) to switch the right panel to a selection summary
with **Skip selected** / **Include selected**. Target pairing and movement parameters are
hidden for multiple selections; click one source row to return to editing that pair.
Bulk actions preserve each row's target and individual movement parameters, even when
the selected rows have different values. Included pairs must be confirmed again. Skipping a
parent does not silently skip its children: remaining child mappings are flagged for review.
Changing the list filters clears the checked selection so hidden rows are not selected accidentally.
Changing model assets clears the mapping after an unsaved-change prompt.
Presets are matched by model asset GUID and hierarchy paths; they require those assets
to be available in the consuming project. Name suggestions never overwrite existing pairs
and never mark a pair confirmed. A suggestion is not proof of anatomical equivalence.

**Loop preview** in step 2 repeats playback only in this window. **Loop exported clip**
in Review sets the new clip's Loop Time flag for Animator playback. These controls are
independent. Looping does not blend or repair a mismatch between the first and last pose.

Movement settings apply to both preview and export:

- **Keep target rest pose** holds the target bone's local position, preserving its length.
- **Transfer movement offset** adds the source's animated displacement from its reference
  position, converted between source and target parent reference coordinate systems.
- **Scale movement offset** also multiplies that displacement by the chosen multiplier.
- **Allow scale changes** transfers the source animated/reference scale ratio onto the
  target reference scale. When disabled, the target reference scale is preserved.

Rotations transfer changes relative to the source reference pose into the target's
parent reference axes. Reference poses must represent the same stance and facing direction.
Use offset transfer selectively on moving roots/hips; transferring every bone's position
can change proportions. Curves from other characters or skipped paths are listed and
excluded. Clips from another model asset produce a reference-pose warning.

This is a Generic Transform baker for corresponding hierarchies. It does not solve IK,
foot locking, mismatched chain lengths, Humanoid muscle curves, blendshapes or materials.
Non-positive or non-uniform reference scales are rejected explicitly. Target-only bones
retain their local pose while following their animated parents. Preview uses isolated
Transform clones; original models, clips and scenes are not changed. Export samples at
1–120 Hz, includes the exact endpoint, and limits each bake to 36000 intervals / 5 million
channel samples. Inspect the result before using it in a production controller.

Animation Retarget was validated in Unity 6000.4.11f1 with focused editor checks and live
preview/export testing. Unity 2022.3 remains the package's declared minimum; this release
has not been separately validated on that editor version.

### PlayerPref Management
Open `CraidoZ Tools/PlayerPref Management` from the Unity menu.

### Prefab Unpack
Menu:
- `CraidoZ Tools/Prefab/Unpack Selected` (Ctrl+Alt+U)
- `CraidoZ Tools/Prefab/Unpack Selected (Completely)` (Ctrl+Alt+Shift+U)
- `CraidoZ Tools/Prefab/Unpack To New Prefab` (Window)

Window notes:
- You can drag multiple prefab/FBX assets from Project.
- Prefix behavior: empty = keep original name. With one item, prefix replaces the name. With multiple items, prefix is added before the original name.

### Scene Grid
Menu:
- `CraidoZ Tools/Scene Grid/Enable Grid`
- `CraidoZ Tools/Scene Grid/Show Scene View Button`
- `CraidoZ Tools/Scene Grid/Settings`

Scene View toolbar:
- `Grid <Plane>` toggles grid rendering on selected plane through world origin.
- `▼` opens quick settings dropdown for grid parameters.

### ShowIf attributes
Add the attribute to any serialized field.

```csharp
using Craidoz.Tools.CodeAssist;
using UnityEngine;

public class Example : MonoBehaviour
{
    public bool showAdvanced;
    public int mode;
    public float speed;

    [ShowIf("showAdvanced")]
    public int advancedValue;

    [ShowIfNot("mode", 0)]
    public string nonDefaultModeLabel;

    [ShowIfGreater("speed", 2.5f)]
    public float fastOnlyValue;
}
```

Enum examples:
```csharp
[ShowIf("mode", nameof(Mode.Advanced))]
public int advancedValue;

[ShowIf("mode", (int)Mode.Advanced)]
public int advancedValueByIndex;
```

## Notes
- `ShowIf` compares against serialized fields. The compared field must be in the same object (or sibling field in a nested object).
- `ShowIf` with enum names uses the enum label text, so `nameof(MyEnum.Value)` is recommended.

## Update Log
- 0.0.7 (2026-10-22)
  - Added Animation Retarget for Generic rigs, with source/target selection, manual bone mapping,
    exact path/name suggestions, confirmation, and reusable JSON mapping presets.
  - Added skeleton motion preview with playback, scrubbing, orbit, zoom and pan. Partial mappings
    can be previewed while unmapped target joints retain their local reference pose.
  - Added mesh filtering and bulk Skip / Include. Multiple selection preserves each bone's
    individual settings and shows shared actions in the details panel.
  - Added `.anim` export with reference-pose rotation conversion, per-pair position/scale settings,
    configurable sample rate, event copying, and independent preview/export loop controls.
  - Added a clearer Review page with expandable diagnostics and a fixed Generate button.
    Export requires valid, confirmed included pairs and matching parent chains; existing assets
    are preserved by generating a unique output filename.
  - Balanced popup GUI scopes before applying a bone selection or closing the picker.
- 0.0.6
  - Updated Scene Grid line rendering to stay visible in front of scene objects.
- 0.0.5
  - Improved Scene Grid toolbar behavior and dropdown positioning in Scene View.
  - Added grid plane selection (XY/XZ/YZ) and color settings for lines/text.
- 0.0.4
  - Added Hierarchy tools: Find Components, Find Missing Scripts (with remove), Find Missing References (table view).
  - Added Prefab Unpack window with multi-asset drag & drop.
  - Improved Missing References UI layout and table formatting.
  - Added Scene Grid tool with Scene View toggle/dropdown settings.
