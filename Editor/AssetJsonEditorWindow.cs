#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AssetManager.Runtime;
using UnityEditor;
using UnityEngine;

namespace AssetManager.Editor
{
    // ────────────────────────────────────────────────────────────────────────────
    // Asset Manifest JSON Editor Window
    // ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Editor window for creating and editing <c>asset_manifest.json</c> in StreamingAssets.
    /// Open via <b>JSON Editors → Asset Manager</b> or via the Manager Inspector button.
    /// </summary>
    public class AssetJsonEditorWindow : EditorWindow
    {
        private const string JsonFolderName = "asset_manifest";

        private AssetManifestEditorBridge _bridge;
        private UnityEditor.Editor        _bridgeEditor;
        private Vector2                   _scroll;
        private string                    _status;
        private bool                      _statusError;

        [MenuItem("JSON Editors/Asset Manager")]
        public static void ShowWindow() =>
            GetWindow<AssetJsonEditorWindow>("Asset Manifest JSON");

        private void OnEnable()
        {
            _bridge = CreateInstance<AssetManifestEditorBridge>();
            Load();
        }

        private void OnDisable()
        {
            if (_bridgeEditor != null) DestroyImmediate(_bridgeEditor);
            if (_bridge      != null) DestroyImmediate(_bridge);
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, _statusError ? MessageType.Error : MessageType.Info);

            if (_bridge == null) return;
            if (_bridgeEditor == null)
                _bridgeEditor = UnityEditor.Editor.CreateEditor(_bridge);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _bridgeEditor.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField(
                $"StreamingAssets/{JsonFolderName}/",
                EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(50))) Load();
            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(50))) Save();
            EditorGUILayout.EndHorizontal();
        }

        private void Load()
        {
            string folderPath = Path.Combine(Application.streamingAssetsPath, JsonFolderName);
            try
            {
                var list = new List<AssetEntry>();
                if (Directory.Exists(folderPath))
                {
                    foreach (var file in Directory.GetFiles(folderPath, "*.json", SearchOption.TopDirectoryOnly))
                    {
                        var w = JsonUtility.FromJson<AssetManifestEditorWrapper>(File.ReadAllText(file));
                        if (w?.entries != null) list.AddRange(w.entries);
                    }
                }
                else
                {
                    Directory.CreateDirectory(folderPath);
                    AssetDatabase.Refresh();
                }
                _bridge.entries = list;
                if (_bridgeEditor != null) { DestroyImmediate(_bridgeEditor); _bridgeEditor = null; }
                _status = $"Loaded {list.Count} asset entries from {JsonFolderName}/.";
                _statusError = false;
            }
            catch (Exception e) { _status = $"Load error: {e.Message}"; _statusError = true; }
        }

        private void Save()
        {
            try
            {
                string folderPath = Path.Combine(Application.streamingAssetsPath, JsonFolderName);
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
                int saved = 0;
                foreach (var entry in _bridge.entries)
                {
                    if (string.IsNullOrEmpty(entry.id)) continue;
                    var w = new AssetManifestEditorWrapper { entries = new[] { entry } };
                    File.WriteAllText(Path.Combine(folderPath, $"{entry.id}.json"), JsonUtility.ToJson(w, true));
                    saved++;
                }
                AssetDatabase.Refresh();
                _status = $"Saved {saved} asset entry file(s) to {JsonFolderName}/";
                _statusError = false;
            }
            catch (Exception e) { _status = $"Save error: {e.Message}"; _statusError = true; }
        }
    }

    // ── ScriptableObject bridge ──────────────────────────────────────────────
    internal class AssetManifestEditorBridge : ScriptableObject
    {
        public List<AssetEntry> entries = new List<AssetEntry>();
    }

    // ── Local wrapper mirrors the internal AssetManifestJson ─────────────────
    [Serializable]
    internal class AssetManifestEditorWrapper
    {
        public AssetEntry[] entries = Array.Empty<AssetEntry>();
    }
}
#endif
