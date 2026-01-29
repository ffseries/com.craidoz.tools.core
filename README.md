# CraidoZ Tools Core

Unity package that provides editor utilities and code-assist attributes.

Version: 0.0.3

## Features
- PlayerPref Management window (`CraidoZ Tools/PlayerPref Management`)
  - Set / load / delete PlayerPrefs
  - Type selection (bool, int, float, string)
  - Type mismatch warning
- Prefab Unpack tools (`CraidoZ Tools/Prefab`)
  - Unpack Selected
  - Unpack Selected (Completely)
  - Unpack To New Prefab (window)
- ShowIf attributes (CodeAssist)
  - Conditionally show fields in the Inspector based on another field
  - Supports bool, int, float, string, and enum
  - Multiple expected values
  - Comparisons via attributes: ShowIf, ShowIfNot, ShowIfGreater/Less (int/float)

## Installation
Add this repo as a Unity package (Git URL) or copy into `Packages/`.

## Usage

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
