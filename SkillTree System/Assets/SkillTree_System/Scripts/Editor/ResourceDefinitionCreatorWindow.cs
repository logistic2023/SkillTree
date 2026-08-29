#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace JollyLlama.SkillTreeSystem
{
    /// <summary>
    /// Standalone utility window for creating ResourceDefinitionSO assets.
    /// Opens as a small floating window so it has its own OnGUI / event loop
    /// and zero interference with the Skill Tree editor window behind it.
    /// </summary>
    public class ResourceDefinitionCreatorWindow : EditorWindow
    {
        // ── Static open method ────────────────────────────────────────────────────

        /// <summary>
        /// Opens the window. The optional callback is invoked with the newly
        /// created SO so the caller can assign it to whatever field it needs.
        /// </summary>
        public static void Open(Action<ResourceDefinitionSO> onCreated = null)
        {
            var win = GetWindow<ResourceDefinitionCreatorWindow>(
                utility: true, title: "Create Resource", focus: true);

            win.minSize    = new Vector2(320f, 210f);
            win.maxSize    = new Vector2(320f, 210f);
            win._onCreated = onCreated;

            // Centre over the main editor window
            var main = EditorGUIUtility.GetMainWindowPosition();
            win.position = new Rect(
                main.x + (main.width  - 320f) * 0.5f,
                main.y + (main.height - 210f) * 0.5f,
                320f, 210f);

            win.Show();
        }

        // ── State ─────────────────────────────────────────────────────────────────

        private Action<ResourceDefinitionSO> _onCreated;

        private string _displayName = "";
        private string _resourceId  = "";
        private string _suffix      = "g";
        private Color  _color       = Color.white;
        private string _folder      = "Assets/SkillTree_System/Data/Resources";

        // Track whether the user has manually edited the ID field
        private bool _idEdited;

        // ── OnGUI ─────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            GUILayout.Space(8);

            // Display Name — auto-fills ID while user hasn't manually changed it
            EditorGUI.BeginChangeCheck();
            _displayName = EditorGUILayout.TextField("Display Name", _displayName);
            if (EditorGUI.EndChangeCheck() && !_idEdited)
                _resourceId = Slugify(_displayName);

            EditorGUI.BeginChangeCheck();
            _resourceId = EditorGUILayout.TextField("Resource ID", _resourceId);
            if (EditorGUI.EndChangeCheck()) _idEdited = true;

            _suffix = EditorGUILayout.TextField("Short Suffix",  _suffix);
            _color  = EditorGUILayout.ColorField("Display Color", _color);
            _folder = EditorGUILayout.TextField("Save Folder",   _folder);

            GUILayout.Space(8);
            DrawHLine();
            GUILayout.Space(6);

            bool valid = !string.IsNullOrWhiteSpace(_displayName) &&
                         !string.IsNullOrWhiteSpace(_resourceId);

            GUILayout.BeginHorizontal();

            GUI.enabled = valid;
            if (GUILayout.Button("Create", GUILayout.Height(24)))
                Commit();
            GUI.enabled = true;

            if (GUILayout.Button("Cancel", GUILayout.Height(24)))
                Close();

            GUILayout.EndHorizontal();

            // Keyboard shortcuts
            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.Escape)
                {
                    Event.current.Use();
                    Close();
                }
                else if (valid && (Event.current.keyCode == KeyCode.Return ||
                                   Event.current.keyCode == KeyCode.KeypadEnter))
                {
                    Event.current.Use();
                    Commit();
                }
            }
        }

        // ── Commit ────────────────────────────────────────────────────────────────

        private void Commit()
        {
            string id = _resourceId.Trim();

            if (!System.IO.Directory.Exists(_folder))
            {
                System.IO.Directory.CreateDirectory(_folder);
                AssetDatabase.Refresh();
            }

            string path = AssetDatabase.GenerateUniqueAssetPath(
                System.IO.Path.Combine(_folder, id + ".asset"));

            var so = ScriptableObject.CreateInstance<ResourceDefinitionSO>();
            AssetDatabase.CreateAsset(so, path);

            so.resourceId   = id;
            so.displayName  = _displayName.Trim();
            so.shortSuffix  = _suffix;
            so.displayColor = _color;

            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(so);

            _onCreated?.Invoke(so);
            Close();
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static void DrawHLine()
            => EditorGUI.DrawRect(GUILayoutUtility.GetRect(1, 1),
                new Color(0.35f, 0.35f, 0.35f));

        private static string Slugify(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Trim().ToLower()
                    .Replace(" ", "_")
                    .Replace("-", "_");
        }
    }
}
#endif