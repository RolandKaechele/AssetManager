using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace AssetManager.Runtime
{
    // -------------------------------------------------------------------------
    // AssetEntryType
    // -------------------------------------------------------------------------

    /// <summary>The Unity asset type stored by an <see cref="AssetEntry"/>.</summary>
    public enum AssetEntryType { Sprite, AudioClip, Prefab, TextAsset, Material, Texture2D }

    // -------------------------------------------------------------------------
    // AssetEntry
    // -------------------------------------------------------------------------

    /// <summary>
    /// Registers a single asset under a logical string key.
    /// Serializable so it can be defined in the Inspector and loaded from a JSON manifest.
    /// </summary>
    [Serializable]
    public class AssetEntry
    {
        [Tooltip("Unique key used to retrieve this asset at runtime.")]
        public string id;

        public AssetEntryType type;

        [Tooltip("Path relative to a Resources/ folder, without extension (e.g. 'Characters/JanTenner/Portrait').")]
        public string resourcePath;

        [Tooltip("Optional: path relative to StreamingAssets/ for a mod file override. Supported only for Sprite and Texture2D.")]
        public string modOverridePath;
    }

    // -------------------------------------------------------------------------
    // JSON wrapper
    // -------------------------------------------------------------------------

    [Serializable]
    internal class AssetManifestJson
    {
        public AssetEntry[] entries;
    }

    // -------------------------------------------------------------------------
    // AssetManager
    // -------------------------------------------------------------------------

    /// <summary>
    /// <b>AssetManager</b> is a centralized, string-keyed asset registry with caching.
    ///
    /// <para><b>Responsibilities:</b>
    /// <list type="number">
    ///   <item>Map logical string ids to <c>Resources/</c> paths (or mod override paths).</item>
    ///   <item>Cache loaded assets to avoid repeated <c>Resources.Load</c> calls.</item>
    ///   <item>Support Sprite/Texture2D replacement from files in <c>StreamingAssets/</c> for modding.</item>
    ///   <item>Optionally merge the registry from a JSON manifest at startup.</item>
    /// </list>
    /// </para>
    ///
    /// <para><b>Modding / JSON:</b> Enable <c>loadManifestFromJson</c> and place an
    /// <c>asset_manifest.json</c> in <c>StreamingAssets/</c>.
    /// JSON entries are <b>merged by id</b>: JSON overrides Inspector entries with the same id and can add new ones.
    /// Cache is invalidated for updated entries.
    /// For Sprite/Texture2D entries, set <c>modOverridePath</c> to a PNG/JPG path relative to
    /// <c>StreamingAssets/</c> to load the asset directly from disk instead of <c>Resources/</c>.</para>
    ///
    /// <para><b>Optional integration defines:</b>
    /// <list type="bullet">
    ///   <item><c>ASSETMANAGER_EM</c>  — EventManager: fires <c>AssetLoaded</c> as a named GameEvent when an asset is first cached.</item>
    ///   <item><c>ASSETMANAGER_MLF</c> — MapLoaderFramework: registers with MapLoader for scene-correlated preload groups.</item>
    /// </list>
    /// </para>
    /// </summary>
    [AddComponentMenu("AssetManager/Asset Manager")]
    [DisallowMultipleComponent]
#if ODIN_INSPECTOR
    public class AssetManager : SerializedMonoBehaviour
#else
    public class AssetManager : MonoBehaviour
#endif
    {
        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------

        [Header("Registry")]
        [Tooltip("All asset entries for this game.")]
        [SerializeField] private AssetEntry[] entries = Array.Empty<AssetEntry>();

        [Tooltip("Preload all registered assets into the cache during Awake.")]
        [SerializeField] private bool preloadOnAwake = false;

        [Header("Modding / JSON")]
        [Tooltip("When enabled, merge registry entries from a JSON manifest in StreamingAssets/ at startup.")]
        [SerializeField] private bool loadManifestFromJson = false;

        [Tooltip("Path relative to StreamingAssets/ (e.g. 'asset_manifest.json' or 'Mods/asset_manifest.json').")]
        [SerializeField] private string manifestJsonPath = "asset_manifest.json";

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------

        /// <summary>Fired the first time an asset is loaded into the cache. Parameter: asset id.</summary>
        public event Action<string> OnAssetLoaded;

        // -------------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------------

        private readonly List<AssetEntry> _entries = new();
        private readonly Dictionary<string, AssetEntry> _index = new();
        private readonly Dictionary<string, UnityEngine.Object> _cache = new();

        /// <summary>Read-only registry (merged Inspector + JSON).</summary>
        public IReadOnlyList<AssetEntry> Entries => _entries;

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------

        private void Awake()
        {
            BuildIndex();
            if (loadManifestFromJson) LoadManifestJson();
            if (preloadOnAwake) PreloadAll();
        }

        private void BuildIndex()
        {
            _entries.Clear();
            _index.Clear();
            foreach (var e in entries)
            {
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                _entries.Add(e);
                _index[e.id] = e;
            }
        }

        private void LoadManifestJson()
        {
            string path = Path.Combine(Application.streamingAssetsPath, manifestJsonPath);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[AssetManager] Manifest JSON not found: {path}");
                return;
            }
            try
            {
                var wrapper = JsonUtility.FromJson<AssetManifestJson>(File.ReadAllText(path));
                if (wrapper?.entries == null) return;
                foreach (var e in wrapper.entries)
                {
                    if (e == null || string.IsNullOrEmpty(e.id)) continue;
                    _cache.Remove(e.id); // invalidate cache for updated entry
                    if (_index.ContainsKey(e.id))
                    {
                        int i = _entries.FindIndex(x => x.id == e.id);
                        if (i >= 0) _entries[i] = e;
                        _index[e.id] = e;
                    }
                    else
                    {
                        _entries.Add(e);
                        _index[e.id] = e;
                    }
                }
                Debug.Log($"[AssetManager] Asset manifest merged from {path}.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AssetManager] Failed to load manifest JSON: {ex.Message}");
            }
        }

        // -------------------------------------------------------------------------
        // Asset loading
        // -------------------------------------------------------------------------

        /// <summary>
        /// Retrieve the asset registered under <paramref name="id"/> as type <typeparamref name="T"/>.
        /// Returns the cached instance on subsequent calls. Returns null if not found or load fails.
        /// </summary>
        public T Get<T>(string id) where T : UnityEngine.Object
        {
            if (_cache.TryGetValue(id, out var cached) && cached is T typedCached)
                return typedCached;

            if (!_index.TryGetValue(id, out var entry))
            {
                Debug.LogWarning($"[AssetManager] No entry registered for id '{id}'.");
                return null;
            }

            // Mod override path: file load for Sprite/Texture2D
            if (!string.IsNullOrEmpty(entry.modOverridePath))
            {
                var modAsset = LoadFromFile<T>(entry.modOverridePath, entry.id);
                if (modAsset != null)
                {
                    _cache[id] = modAsset;
                    NotifyLoaded(id);
                    return modAsset;
                }
            }

            // Resources.Load fallback
            var asset = Resources.Load<T>(entry.resourcePath);
            if (asset == null)
            {
                Debug.LogWarning($"[AssetManager] Resources.Load<{typeof(T).Name}>('{entry.resourcePath}') failed for id '{id}'.");
                return null;
            }
            _cache[id] = asset;
            NotifyLoaded(id);
            return asset;
        }

        private T LoadFromFile<T>(string relativePath, string assetId) where T : UnityEngine.Object
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"[AssetManager] Mod file not found: {fullPath} (id '{assetId}').");
                return null;
            }
            bool wantsTexture = typeof(T) == typeof(Texture2D) || typeof(T) == typeof(Sprite);
            if (!wantsTexture)
            {
                Debug.LogWarning($"[AssetManager] File-based mod override only supported for Sprite/Texture2D (id '{assetId}').");
                return null;
            }
            try
            {
                byte[] bytes = File.ReadAllBytes(fullPath);
                var tex = new Texture2D(2, 2) { name = assetId };
                if (!tex.LoadImage(bytes))
                {
                    Debug.LogWarning($"[AssetManager] Failed to decode image: {fullPath}");
                    return null;
                }
                if (typeof(T) == typeof(Texture2D)) return tex as T;
                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                sprite.name = assetId;
                return sprite as T;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AssetManager] File read error for '{fullPath}': {ex.Message}");
                return null;
            }
        }

        private void NotifyLoaded(string id)
        {
            OnAssetLoaded?.Invoke(id);
#if ASSETMANAGER_EM
            FindFirstObjectByType<EventManager.Runtime.EventManager>()?.Fire("AssetLoaded", id);
#endif
        }

        // -------------------------------------------------------------------------
        // Preload / release
        // -------------------------------------------------------------------------

        /// <summary>Load the asset for <paramref name="id"/> into the cache now (without returning it).</summary>
        public void Preload(string id)
        {
            if (_cache.ContainsKey(id)) return;
            if (!_index.TryGetValue(id, out var entry))
            {
                Debug.LogWarning($"[AssetManager] Cannot preload unknown id '{id}'.");
                return;
            }
            var asset = Resources.Load(entry.resourcePath);
            if (asset != null) { _cache[id] = asset; NotifyLoaded(id); }
            else Debug.LogWarning($"[AssetManager] Preload failed for '{entry.resourcePath}' (id '{id}').");
        }

        /// <summary>Preload all registered entries into the cache.</summary>
        public void PreloadAll()
        {
            foreach (var e in _entries)
                if (e != null && !string.IsNullOrEmpty(e.id))
                    Preload(e.id);
        }

        /// <summary>Remove the cached asset for <paramref name="id"/> (does not unload from memory).</summary>
        public void Release(string id) => _cache.Remove(id);

        /// <summary>Clear the entire asset cache.</summary>
        public void ReleaseAll() => _cache.Clear();

        // -------------------------------------------------------------------------
        // Queries
        // -------------------------------------------------------------------------

        /// <summary>Returns true if an entry is registered for <paramref name="id"/>.</summary>
        public bool HasEntry(string id) => _index.ContainsKey(id);

        /// <summary>Returns true if the asset for <paramref name="id"/> is currently in the cache.</summary>
        public bool IsCached(string id) => _cache.ContainsKey(id);

        /// <summary>Returns the <see cref="AssetEntry"/> for <paramref name="id"/>, or null.</summary>
        public AssetEntry GetEntry(string id) => _index.TryGetValue(id, out var e) ? e : null;
    }
}
