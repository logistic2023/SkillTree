#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace JollyLlama.SkillTreeSystem
{
    /// <summary>
    /// One-time upgrade from the old hard-coded SkillBranch enum to SkillBranchSO assets.
    ///
    /// Old nodes/templates serialized the enum as an int under the key "branch"; that
    /// value is now read into a hidden legacyBranchIndex field (via FormerlySerializedAs).
    /// This tool creates (or reuses) one SkillBranchSO per old enum value, adds them to
    /// the tree's branches list in the old order, points every node/template at the
    /// right asset, and clears the legacy value.
    ///
    /// Safe to run more than once — it only touches objects that still have legacy data
    /// and no branch assigned, and reuses branch assets it finds by branchId.
    /// </summary>
    public static class SkillBranchMigration
    {
        // Must match the order of the old enum: Offense, Control, Economy, Defense.
        private static readonly (string id, string name, Color color)[] Legacy =
        {
            ("offense", "Offense", new Color(0.75f, 0.18f, 0.12f)),
            ("control", "Control", new Color(0.12f, 0.38f, 0.78f)),
            ("economy", "Economy", new Color(0.72f, 0.60f, 0.08f)),
            ("defense", "Defense", new Color(0.42f, 0.42f, 0.46f)),
        };

        private const string DefaultFolder = "Assets/SkillTree_System/Data/SkillTree/Branches";

        [MenuItem("Tools/Skill Tree/Migrate Legacy Branches (All Trees)")]
        public static void MigrateAll()
        {
            int trees = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:SkillTreeSO"))
            {
                var tree = AssetDatabase.LoadAssetAtPath<SkillTreeSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (tree != null && TreeNeedsMigration(tree)) { MigrateTree(tree, DefaultFolder, save: false); trees++; }
            }
            int templates = MigrateTemplates(DefaultFolder);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SkillBranchMigration] Migrated {trees} tree(s) and {templates} template(s).");
        }

        public static bool TreeNeedsMigration(SkillTreeSO tree)
            => tree?.allNodes != null && tree.allNodes.Any(NeedsMigration);

        private static bool NeedsMigration(SkillNodeSO n)
            => n != null && n.branch == null && n.LegacyBranchIndex >= 0;

        public static void MigrateTree(SkillTreeSO tree, string folder, bool save = true)
        {
            if (tree == null) return;
            folder = string.IsNullOrWhiteSpace(folder) ? DefaultFolder : folder;

            Undo.RecordObject(tree, "Migrate Legacy Branches");
            tree.branches ??= new List<SkillBranchSO>();

            // Recreate all four old tabs, in the old order, so the runtime panel looks the same.
            var map = new SkillBranchSO[Legacy.Length];
            for (int i = 0; i < Legacy.Length; i++)
            {
                map[i] = GetOrCreate(i, folder, tree);
                if (!tree.branches.Contains(map[i])) tree.branches.Add(map[i]);
            }
            EditorUtility.SetDirty(tree);

            int migrated = 0;
            foreach (var node in tree.allNodes.Where(NeedsMigration))
            {
                int idx = node.LegacyBranchIndex;
                if (idx >= map.Length)
                {
                    Debug.LogWarning($"[SkillBranchMigration] '{node.name}' has unknown legacy branch index {idx}; left unassigned.", node);
                    continue;
                }
                Undo.RecordObject(node, "Migrate Legacy Branches");
                node.branch = map[idx];
                node.ClearLegacyBranch();
                EditorUtility.SetDirty(node);
                migrated++;
            }

            if (save)
            {
                MigrateTemplates(folder);
                AssetDatabase.SaveAssets();
            }
            Debug.Log($"[SkillBranchMigration] '{tree.name}': migrated {migrated} node(s).", tree);
        }

        private static int MigrateTemplates(string folder)
        {
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:NodeTemplateSO"))
            {
                var tpl = AssetDatabase.LoadAssetAtPath<NodeTemplateSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (tpl == null || tpl.branch != null || tpl.LegacyBranchIndex < 0) continue;
                if (tpl.LegacyBranchIndex >= Legacy.Length) continue;

                Undo.RecordObject(tpl, "Migrate Legacy Branches");
                tpl.branch = GetOrCreate(tpl.LegacyBranchIndex, folder, null);
                tpl.ClearLegacyBranch();
                EditorUtility.SetDirty(tpl);
                count++;
            }
            return count;
        }

        /// <summary>Finds a branch with the legacy id in the tree, then the project; creates it otherwise.</summary>
        private static SkillBranchSO GetOrCreate(int legacyIndex, string folder, SkillTreeSO tree)
        {
            var (id, name, color) = Legacy[legacyIndex];

            var found = tree != null ? tree.branches.FirstOrDefault(b => b != null &&
                                                                         string.Equals(b.branchId, id, StringComparison.OrdinalIgnoreCase)) : null;
            if (found != null) return found;

            foreach (var guid in AssetDatabase.FindAssets("t:SkillBranchSO"))
            {
                var b = AssetDatabase.LoadAssetAtPath<SkillBranchSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (b != null && string.Equals(b.branchId, id, StringComparison.OrdinalIgnoreCase)) return b;
            }

            if (!AssetDatabase.IsValidFolder(folder))
            {
                System.IO.Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }

            var asset = ScriptableObject.CreateInstance<SkillBranchSO>();
            asset.branchId    = id;
            asset.displayName = name;
            asset.color       = color;
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/Branch_{name}.asset");
            AssetDatabase.CreateAsset(asset, path);
            Undo.RegisterCreatedObjectUndo(asset, "Create Branch");
            return asset;
        }
    }
}
#endif