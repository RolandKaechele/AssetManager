# AssetManager

A string-keyed asset registry for Unity with caching, preloading, and mod support.  
Maps logical string ids to `Resources/` paths (or `StreamingAssets/` file overrides for mods) and caches loaded instances.


## Features

- **String-keyed registry** — map logical ids to `Resources/` paths so callers never hardcode paths
- **Type-safe retrieval** — `Get<Sprite>("portrait_jan")`, `Get<AudioClip>("sfx_laser")`, etc.
- **Caching** — assets are loaded once and stored; repeated `Get<T>` calls return cached instances
- **Preloading** — `PreloadAll()` or `Preload(id)` to warm the cache during loading screens
- **Sprite / Texture2D mod override** — set `modOverridePath` to a PNG/JPG path relative to `StreamingAssets/`; the file is decoded at runtime and replaces the `Resources/` version
- **JSON / Modding** — load and merge registry entries from `StreamingAssets/asset_manifest/`; JSON entries override Inspector entries by id and can add new ones; cache is invalidated for updated entries
- **EventManager integration** — fires `AssetLoaded` as a named GameEvent on first cache (activated via `ASSETMANAGER_EM`)
- **Custom Inspector** — per-entry cache status with Preload All / Release All controls at runtime
- **Odin Inspector integration** — `SerializedMonoBehaviour` base for full Inspector serialization of complex types; runtime-display fields marked `[ReadOnly]` in Play Mode (activated via `ODIN_INSPECTOR`)


## Installation

### Option A — Unity Package Manager (Git URL)

1. Open **Window → Package Manager**
2. Click **+** → **Add package from git URL…**
3. Enter:

   ```
   https://github.com/RolandKaechele/AssetManager.git
   ```

### Option B — Clone into Assets

```bash
git clone https://github.com/RolandKaechele/AssetManager.git Assets/AssetManager
```

### Option C — npm / postinstall

```bash
cd Assets/AssetManager
npm install
```

`postinstall.js` creates the required `StreamingAssets/` folder under `Assets/` and optionally copies example JSON files.


## Scene Setup

1. Attach `AssetManager` to a persistent manager GameObject.
2. Register entries in the Inspector (id, type, resourcePath).
3. Optionally enable `preloadOnAwake` or call `PreloadAll()` during a loading screen.


## Quick Start

### 1. Inspector fields

| Field | Default | Description |
| ----- | ------- | ----------- |
| `entries` | *(empty)* | All asset registry entries |
| `preloadOnAwake` | `false` | Load all entries into cache on Awake |
| `loadManifestFromJson` | `false` | Merge from JSON manifest on Awake |
| `manifestJsonPath` | `"asset_manifest/"` | Folder relative to `StreamingAssets/` containing `.json` files to merge. Falls back to single-file mode if the value points to an existing file. |

### 2. Retrieve assets

```csharp
var am = FindFirstObjectByType<AssetManager.Runtime.AssetManager>();

// Load a sprite registered as "portrait_jan_tenner"
Sprite portrait = am.Get<Sprite>("portrait_jan_tenner");

// Load audio
AudioClip music = am.Get<AudioClip>("music_main_theme");

// Load prefab
GameObject panel = am.Get<GameObject>("ui_save_panel");
```

### 3. Preload during loading screen

```csharp
am.PreloadAll();

// Or preload specific groups
am.Preload("music_main_theme");
am.Preload("portrait_jan_tenner");
```

### 4. Release cache

```csharp
am.Release("portrait_jan_tenner");  // release one
am.ReleaseAll();                     // clear entire cache
```


## JSON / Modding

Enable `loadManifestFromJson` and place one or more `.json` files in `StreamingAssets/asset_manifest/`.
All `*.json` files in the folder are loaded and merged by `id` at startup.

**Example:** `StreamingAssets/asset_manifest/main.json`

```json
{
  "entries": [
    {
      "id": "portrait_jan_tenner",
      "type": 0,
      "resourcePath": "Characters/JanTenner/Portrait",
      "modOverridePath": ""
    },
    {
      "id": "music_main_theme",
      "type": 1,
      "resourcePath": "Audio/Music/MainTheme",
      "modOverridePath": ""
    }
  ]
}
```

**`type` values:** `0` = Sprite, `1` = AudioClip, `2` = Prefab, `3` = TextAsset, `4` = Material, `5` = Texture2D

### Mod file override (Sprite / Texture2D)

Set `modOverridePath` to a PNG or JPG path relative to `StreamingAssets/`:

```json
{
  "id": "portrait_jan_tenner",
  "type": 0,
  "resourcePath": "Characters/JanTenner/Portrait",
  "modOverridePath": "Mods/EnhancedPortraits/jan_tenner.png"
}
```

The file is decoded at runtime using `Texture2D.LoadImage`, creating a Sprite with a centered pivot at 100 PPU.  
File override is only supported for `Sprite` and `Texture2D` types.


## Runtime API

| Member | Description |
| ------ | ----------- |
| `Get<T>(id)` | Load and cache the asset as type `T`; returns null on failure |
| `Preload(id)` | Load asset into cache without returning it |
| `PreloadAll()` | Preload all registered entries |
| `Release(id)` | Remove a single entry from cache |
| `ReleaseAll()` | Clear the entire cache |
| `HasEntry(id)` | True if `id` is registered |
| `IsCached(id)` | True if `id` is currently in the cache |
| `GetEntry(id)` | Returns the `AssetEntry` for `id`, or null |
| `Entries` | `IReadOnlyList<AssetEntry>` (merged) |
| `OnAssetLoaded` | `event Action<string>` — fires on first cache of each asset |


## Optional Integrations

### EventManager (`ASSETMANAGER_EM`)

Requires `ASSETMANAGER_EM` define and [EventManager](https://github.com/RolandKaechele/EventManager).  
Fires `AssetLoaded` (value = asset id) when an asset is first loaded into the cache.


### Odin Inspector (`ODIN_INSPECTOR`)

Requires `ODIN_INSPECTOR` define (standard Odin Inspector scripting define). Inherits from `SerializedMonoBehaviour` for full Inspector serialization; runtime-display fields are marked `[ReadOnly]`.


## Editor Tools

Open via **JSON Editors → Asset Manager** in the Unity menu bar, or via the **Open JSON Editor** button in the AssetManager Inspector.

| Action | Result |
| ------ | ------ |
| **Load** | Reads all `*.json` from `StreamingAssets/asset_manifest/`; creates the folder if missing |
| **Edit** | Add / remove / reorder entries using the Inspector list |
| **Save** | Writes to `StreamingAssets/asset_manifest/asset_manifest.json` and calls `AssetDatabase.Refresh()` |

With **ODIN_INSPECTOR** active, the list uses Odin's enhanced drawer (drag-to-sort, collapsible entries).


## Dependencies

| Dependency | Required | Notes |
| ---------- | -------- | ----- |
| Unity 2022.3+ | ✓ | |
| EventManager | optional | Required when `ASSETMANAGER_EM` is defined |
| Odin Inspector | optional | Required when `ODIN_INSPECTOR` is defined |


## Repository

[https://github.com/RolandKaechele/AssetManager](https://github.com/RolandKaechele/AssetManager)


## License

MIT — see [LICENSE](LICENSE).
