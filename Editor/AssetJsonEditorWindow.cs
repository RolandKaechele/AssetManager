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
        private const string JsonFileName = "asset_manifest.json";

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
                Path.Combine("StreamingAssets", JsonFileName),
                EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(50))) Load();
            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(50))) Save();
            EditorGUILayout.EndHorizontal();
        }

        private void Load()
        {
            var path = Path.Combine(Application.streamingAssetsPath, JsonFileName);
            try
            {
                if (!File.Exists(path))
                {
                    File.WriteAllText(path, JsonUtility.ToJson(new AssetManifestEditorWrapper(), true));
                    AssetDatabase.Refresh();
                }

                var w = JsonUtility.FromJson<AssetManifestEditorWrapper>(File.ReadAllText(path));
                _bridge.entries = new List<AssetEntry>(
                    w.entries ?? Array.Empty<AssetEntry>());

                if (_bridgeEditor != null) { DestroyImmediate(_bridgeEditor); _bridgeEditor = null; }

                _status     = $"Loaded {_bridge.entries.Count} asset entries.";
                _statusError = false;
            }
            catch (Exception e)
            {
                _status     = $"Load error: {e.Message}";
                _statusError = true;
            }
        }

        private void Save()
        {
            try
            {
                var w    = new AssetManifestEditorWrapper { entries = _bridge.entries.ToArray() };
                var path = Path.Combine(Application.streamingAssetsPath, JsonFileName);
                File.WriteAllText(path, JsonUtility.ToJson(w, true));
                AssetDatabase.Refresh();
                _status     = $"Saved {_bridge.entries.Count} entries to {JsonFileName}.";
                _statusError = false;
            }
            catch (Exception e)
            {
                _status     = $"Save error: {e.Message}";
                _statusError = true;
            }
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
