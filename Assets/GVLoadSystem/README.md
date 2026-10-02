# GVLoadSystem

A ScriptableObject-based save/load system for Unity that stores typed game data in `GroupValues` assets, serializes them to JSON, and applies them at runtime through a simple MonoBehaviour interface.

---

## Requirements

- Unity 2022.3 LTS or newer
- .NET Standard 2.1

---

## Installation

1. Import the package through the Unity Asset Store or the Package Manager.
2. The package lives under `Assets/GVLoadSystem/`. No additional setup is required.

---

## Quick Start

### 1 — Create a GroupValues asset

`Right-click in the Project window → Create → LoadSystem → GroupValues`

Add fields and entries in the Inspector. Each entry has a **name** (key), a **type**, and a default value.

### 2 — Add a LoaderMono to a GameObject

Attach `LoaderMono` to a persistent GameObject (e.g. a GameManager).

In the Inspector:
- Assign your `GroupValues` asset to the **Values** slot.
- Set the **SO Path** to the folder where the asset lives (default: `Assets/Resources/LoadSystem/SavedFiles/`).
- Toggle **Load On Awake**, **Save On Quit**, and **Save On Disable** as needed.

### 3 — Read and write values at runtime

```csharp
// Get a reference to LoaderMono
[SerializeField] LoaderMono loader;

// Read
float volume = loader.GetValue<float>("masterVolume");

// Write (does not save to disk — call SaveData() when done)
loader.SetValue("masterVolume", 0.8f);
loader.SaveData();
```

### 4 — Reset to defaults

```csharp
loader.ResetData();
```

---

## Core Concepts

### GroupValues

A `ScriptableObject` that holds typed fields and entries. It acts as the schema and default-value store. At runtime the system serializes deltas to a hidden JSON file alongside the asset.

Supported types: `bool`, `float`, `double`, `short`, `int`, `long`, `Vector2`, `Vector3`, `char`, `string`, `byte`, and **custom data classes**.

### SimpleGroupValues

A lightweight singleton alternative for small amounts of project-wide data that does not need a full GroupValues asset. Accessible via static API:

```csharp
SimpleGroupValues.Set("score", 1200);
int score = SimpleGroupValues.Get<int>("score");
```

### GVEntryReference

A serializable reference to a single entry inside a GroupValues asset. Assign it in the Inspector and use it to read/write a specific key without hardcoding strings:

```csharp
[SerializeField] GVEntryReference _speedRef;

float speed = _speedRef.Get<float>();
_speedRef.Set(12f);
```

If the key is renamed in the GroupValues asset, the Inspector shows a warning until the reference is re-linked.

### LoaderMono

The runtime bridge between `ALoader` (the serializable loader core) and the scene. Key settings:

| Setting | Default | Description |
|---|---|---|
| Load On Awake | true | Loads JSON on `Awake`. |
| Save On Quit | true | Saves JSON on `OnApplicationQuit`. |
| Save On Disable | false | Saves JSON on `OnDisable`. Use with caution during scene unloads. |

---

## Custom Data Types

You can store arbitrary C# classes inside a GroupValues entry by marking the class with `[CustomGVData("YourKey")]`:

```csharp
[CustomGVData("SoundSettings")]
public class SoundSettingsData
{
    public bool mute = false;
    [GVRange(0, 1)] public float masterVolume = 1f;
    [GVRange(0, 1)] public float musicVolume  = 1f;
}
```

Retrieve it via:

```csharp
var data = loader.GetValue<SoundSettingsData>("SoundSettings");
```

See `Examples/SoundSettings.cs` and `Examples/CameraViewPortSettings.cs` for complete implementations.

---

## Encryption

Encryption is configured per-loader in **Project Settings → GVLoadSystem**.

| Method | Description |
|---|---|
| None | Plain JSON (default). |
| Password | AES-256 with a project-defined password. |
| DeviceKey | Per-device AES-256 key. On Windows the key file is protected by DPAPI. |

Settings are baked into the build automatically by the included build processor.

---

## UI Integration

The package ships with ready-made `UIGVElement` appliers for:

- **Slider** → float/int value
- **Toggle** → bool value
- **InputField** → string value
- **Dropdown** → int index

Attach one of the `SettingsAppliers` prefabs from `Resources/Prefabs/` or add the component directly to your UI GameObject and link it to a `GVEntryReference`.

---

## Editor Tools

Access all editor tools from the **Tools → GVLoadSystem** menu:

| Tool | Purpose |
|---|---|
| GroupValues Editor | Inspect and edit GroupValues assets with a dedicated window. |
| Simple GroupValues | View and edit the SimpleGroupValues singleton. |
| Importer / Exporter | Import or export GroupValues data as CSV or JSON. |
| Size Window | Inspect the serialized size of each GroupValues asset. |
| Debug Overlay | In-game overlay showing live values for selected GroupValues assets. |

---

## Project Settings

**Edit → Project Settings → GVLoadSystem**

- Set encryption method and password/salt.
- Configure the default save subfolder.
- Enable automatic backups (writes a `.backup.json` alongside the main save file).
- Register GroupValues assets for the debug overlay.

---

## Assembly Definitions

| Assembly | Namespace |
|---|---|
| `LoadSystem.asmdef` | `GVLoadSystem.Core`, `GVLoadSystem.GVEditor`, `GVLoadSystem.Encryption` |
| `Utils.asmdef` | `GVUtils.Attributes` |

---

## Support

For questions, bug reports, or feature requests contact: **sirenbladestudios@gmail.com**
