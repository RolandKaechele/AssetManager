#if UNITY_EDITOR
using AssetManager.Runtime;
using UnityEditor;
using UnityEngine;

namespace AssetManager.Editor
{
    /// <summary>
    /// Custom Inspector for <see cref="AssetManager.Runtime.AssetManager"/>.
    /// Validates registry entries and shows cache status with preload/release controls at runtime.
    /// </summary>
    [CustomEditor(typeof(AssetManager.Runtime.AssetManager))]
    public class AssetManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Open JSON Editor")) AssetJsonEditorWindow.ShowWindow();

            EditorGUILayout.Space(6);

            // ── Validation ──────────────────────────────────────────────────────

            var entriesProp = serializedObject.FindProperty("entries");
            if (entriesProp != null)
            {
                bool hasMissingId   = false;
                bool hasMissingPath = false;
                for (int i = 0; i < entriesProp.arraySize; i++)
                {
                    var elem     = entriesProp.GetArrayElementAtIndex(i);
                    var idProp   = elem.FindPropertyRelative("id");
                    var pathProp = elem.FindPropertyRelative("resourcePath");
                    if (idProp   != null && string.IsNullOrEmpty(idProp.stringValue))   hasMissingId   = true;
                    if (pathProp != null && string.IsNullOrEmpty(pathProp.stringValue)) hasMissingPath = true;
                }
                if (hasMissingId)
                    EditorGUILayout.HelpBox("One or more entries are missing an ID.", MessageType.Warning);
                if (hasMissingPath)
                    EditorGUILayout.HelpBox("One or more entries are missing a Resource Path.", MessageType.Warning);
            }

            if (entriesProp != null && entriesProp.arraySize == 0)
                EditorGUILayout.HelpBox(
                    "No entries defined. Add entries in the Inspector or enable JSON manifest loading.",
                    MessageType.Info);

            // ── Runtime controls (Play Mode only) ───────────────────────────────

            if (!Application.isPlaying) return;

            var mgr = (AssetManager.Runtime.AssetManager)target;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Asset Registry", EditorStyles.boldLabel);

            var entries = mgr.Entries;
            if (entries.Count == 0)
            {
                EditorGUILayout.LabelField("  (no entries)");
            }
            else
            {
                foreach (var e in entries)
                {
                    if (e == null) continue;
                    bool cached = mgr.IsCached(e.id);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"  [{e.type}]  {e.id}");
                    EditorGUILayout.LabelField(cached ? "✓ Cached" : "— Not loaded", GUILayout.Width(100));
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Preload All")) mgr.PreloadAll();
            if (GUILayout.Button("Release All")) mgr.ReleaseAll();
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
