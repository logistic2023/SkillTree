#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace JollyLlama.SkillTreeSystem
{
    public class SkillTreeEditorWindow : EditorWindow
    {
        // ── Layout ────────────────────────────────────────────────────────────────
        private const float LEFT_W_MIN  = 160f;
        private const float LEFT_W_MAX  = 600f;
        private const float LEFT_W_DEF  = 280f;
        private const float DIV_W       = 5f;
        private const float TOOLBAR_H   = 24f;
        private const float NODE_W      = 140f;
        private const float NODE_H      = 50f;
        private const float GRID_SIZE   = 20f;
        private const float ZOOM_MIN    = 0.25f;
        private const float ZOOM_MAX    = 2.5f;

        private float _leftW    = LEFT_W_DEF;
        private bool  _divDrag  = false;

        // ── Colors ────────────────────────────────────────────────────────────────
        private static readonly Color ColOffense = new Color(0.75f, 0.18f, 0.12f);
        private static readonly Color ColControl = new Color(0.12f, 0.38f, 0.78f);
        private static readonly Color ColEconomy = new Color(0.72f, 0.60f, 0.08f);
        private static readonly Color ColDefense = new Color(0.42f, 0.42f, 0.46f);
        private static readonly Color ColLine    = new Color(0.65f, 0.65f, 0.65f, 0.85f);
        private static readonly Color ColGrid    = new Color(0.26f, 0.26f, 0.26f, 0.45f);

        // ── State ─────────────────────────────────────────────────────────────────
        private SkillTreeSO  _tree;
        private SkillNodeSO  _selected;

        private Vector2    _offset   = new Vector2(320f, 80f);
        private float      _zoom     = 1f;
        private SkillNodeSO _dragging;
        private Vector2    _dragOffset;
        private bool       _panning;
        private Vector2    _panStart;

        private int     _leftTab;
        private Vector2 _leftScroll;
        private Vector2 _nodeListScroll;
        private Vector2 _effectsScroll;
        private Vector2 _prereqScroll;
        private Vector2 _costsScroll;

        // New-node defaults
        private string      _newId       = "";
        private string      _newName     = "";
        private SkillBranch _newBranch   = SkillBranch.Offense;
        private int         _newMaxRanks = 1;
        private string      _newFolder   = "Assets/SkillTree_System/Data/SkillTree/Nodes";

        private string _newTreeName   = "MainSkillTree";
        private string _newTreeFolder = "Assets/SkillTree_System/Data/SkillTree";

        // Default cost for quick-created nodes
        private ResourceDefinitionSO _defaultCostResource;
        private int                  _defaultCostAmount = 100;

        private SkillNodeSO _prereqCandidate;
        private int         _prereqCandidateRank = 1;

        private int          _effectTypeIndex;
        private static readonly string[] EffectTypeNames = { "Stat Multiplier", "Stat Flat Bonus" };

        private string      _bulkText   = "";
        private SkillBranch _bulkBranch = SkillBranch.Offense;

        // ── Quick-create popup ────────────────────────────────────────────────────
        private bool       _quickCreateOpen;
        private Vector2    _quickCreateCanvasPos;
        private string     _quickCreateName   = "";
        private string     _quickCreateId     = "";
        private bool       _quickCreateIdEdited;
        private SkillBranch _quickCreateBranch = SkillBranch.Offense;
        private const float QC_W = 260f;
        private const float QC_H = 148f;

        // ── Inline node inspector ─────────────────────────────────────────────────
        private bool    _inspectorOpen;
        private Vector2 _inspectorCanvasPos;
        private Vector2 _inspectorScroll;

        private const float INS_W_MIN  = 220f;
        private const float INS_H_MIN  = 200f;
        private const float INS_W_DEF  = 320f;
        private const float INS_H_DEF  = 580f;
        private const float RESIZE_HANDLE = 12f;

        private float   _insW = INS_W_DEF;
        private float   _insH = INS_H_DEF;
        private bool    _insResizing;
        private Vector2 _insResizeStartMouse;
        private Vector2 _insResizeStartSize;
        private Rect    _inspectorScreenRect;

        // ── Open ──────────────────────────────────────────────────────────────────
        [MenuItem("Window/Skill Tree Editor")]
        public static void Open() => GetWindow<SkillTreeEditorWindow>("Skill Tree Editor");

        // =========================================================================
        //  OnGUI
        // =========================================================================
        private void OnGUI()
        {
            DrawToolbar();

            float h        = position.height - TOOLBAR_H;
            float divStart = _leftW;

            Rect leftRect   = new Rect(0,               TOOLBAR_H, _leftW, h);
            Rect divRect    = new Rect(divStart,         TOOLBAR_H, DIV_W,  h);
            Rect canvasRect = new Rect(divStart + DIV_W, TOOLBAR_H,
                position.width - divStart - DIV_W, h);

            // ── Resize MUST be handled first, before DrawCanvas eats drag events ──
            HandleInspectorResizeEvents();

            DrawLeftPanel(leftRect);
            DrawCanvas(canvasRect);
            DrawDivider(divRect);

            if (_inspectorOpen && _selected != null)   DrawInlineInspector(canvasRect);
            if (_quickCreateOpen)                      DrawQuickCreatePopup(canvasRect);
        }

        /// <summary>
        /// Processes resize drag events for the inline inspector.
        /// Called at the very top of OnGUI so it runs before HandleCanvasInput.
        /// </summary>
        private void HandleInspectorResizeEvents()
        {
            if (!_inspectorOpen || _selected == null) return;

            Event ev = Event.current;

            if (ev.type == EventType.MouseDown && ev.button == 0)
            {
                Rect resizeHit = new Rect(
                    _inspectorScreenRect.xMax - RESIZE_HANDLE,
                    _inspectorScreenRect.yMax - RESIZE_HANDLE,
                    RESIZE_HANDLE, RESIZE_HANDLE);

                if (resizeHit.Contains(ev.mousePosition))
                {
                    _insResizing         = true;
                    _insResizeStartMouse = ev.mousePosition;
                    _insResizeStartSize  = new Vector2(_insW, _insH);
                    ev.Use();
                }
            }
            else if (ev.type == EventType.MouseDrag && _insResizing)
            {
                Vector2 delta = ev.mousePosition - _insResizeStartMouse;
                _insW = Mathf.Max(INS_W_MIN, _insResizeStartSize.x + delta.x);
                _insH = Mathf.Max(INS_H_MIN, _insResizeStartSize.y + delta.y);
                ev.Use();
                Repaint();
            }
            else if (ev.type == EventType.MouseUp && _insResizing)
            {
                _insResizing = false;
                ev.Use();
            }
        }

        // =========================================================================
        //  Shared cost editor  (used by both left panel and inline inspector)
        // =========================================================================

        /// <summary>
        /// Draws an editable list of ResourceCost entries for the selected node.
        /// Call inside a change-check block; marks node dirty if anything changes.
        /// </summary>
        private void DrawCostEditor(SkillNodeSO node, ref Vector2 scroll, float maxHeight = 150f)
        {
            SmallSection("COSTS PER RANK");

            node.costsPerRank ??= new List<ResourceCost>();

            scroll = GUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(maxHeight));

            for (int i = node.costsPerRank.Count - 1; i >= 0; i--)
            {
                var cost = node.costsPerRank[i];
                GUILayout.BeginHorizontal(EditorStyles.helpBox);

                // Resource SO picker
                var newRes = (ResourceDefinitionSO)EditorGUILayout.ObjectField(
                    cost.resource, typeof(ResourceDefinitionSO), false, GUILayout.Width(120));
                if (newRes != cost.resource) { cost.resource = newRes; Dirty(node); }

                // Suffix label (read-only reminder)
                string suffix = cost.resource != null ? cost.resource.shortSuffix : "?";
                GUILayout.Label(suffix, EditorStyles.miniLabel, GUILayout.Width(14));

                // Amount per rank
                int newAmt = EditorGUILayout.IntField(cost.amountPerRank, GUILayout.Width(55));
                if (newAmt != cost.amountPerRank)
                {
                    cost.amountPerRank = Mathf.Max(0, newAmt);
                    Dirty(node);
                }

                // Per-rank preview label  e.g. "r1:100  r2:200"
                if (node.maxRanks > 1 && cost.resource != null)
                {
                    string preview = string.Join("  ",
                        Enumerable.Range(1, Mathf.Min(node.maxRanks, 5))
                            .Select(r => $"r{r}:{cost.GetAmount(r)}"));
                    GUILayout.Label(preview, EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                }

                // Remove button
                GUI.color = new Color(1f, 0.35f, 0.35f);
                if (GUILayout.Button("✕", GUILayout.Width(20)))
                {
                    node.costsPerRank.RemoveAt(i);
                    Dirty(node);
                }
                GUI.color = Color.white;

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();

            // ── Add new cost row ─────────────────────────────────────────────────
            GUILayout.BeginHorizontal();
            _defaultCostResource = (ResourceDefinitionSO)EditorGUILayout.ObjectField(
                _defaultCostResource, typeof(ResourceDefinitionSO), false, GUILayout.Width(110));

            // "New…" opens the resource creator as a separate EditorWindow
            if (GUILayout.Button("New…", EditorStyles.miniButton, GUILayout.Width(38)))
                ResourceDefinitionCreatorWindow.Open(so => { _defaultCostResource = so; Repaint(); });

            _defaultCostAmount = Mathf.Max(0,
                EditorGUILayout.IntField(_defaultCostAmount, GUILayout.Width(50)));

            GUI.enabled = _defaultCostResource != null;
            if (GUILayout.Button("+ Add", GUILayout.Width(48)))
            {
                node.costsPerRank.Add(new ResourceCost
                {
                    resource      = _defaultCostResource,
                    amountPerRank = _defaultCostAmount
                });
                Dirty(node);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            // Cost preview for all ranks
            if (node.costsPerRank.Count > 0 && node.maxRanks > 1)
            {
                EditorGUILayout.LabelField("Preview:", EditorStyles.miniLabel);
                for (int r = 1; r <= node.maxRanks; r++)
                    EditorGUILayout.LabelField($"  Rank {r}: {node.GetCostString(r)}", EditorStyles.miniLabel);
            }
        }

        // =========================================================================
        //  Resource Definition creator popup
        // =========================================================================
        // =========================================================================
        //  Quick-create popup
        // =========================================================================
        private void DrawQuickCreatePopup(Rect canvasRect)
        {
            Vector2 screenPos = W2C(_quickCreateCanvasPos) + new Vector2(canvasRect.x, canvasRect.y);
            float px = Mathf.Clamp(screenPos.x, canvasRect.x, canvasRect.xMax - QC_W);
            float py = Mathf.Clamp(screenPos.y, canvasRect.y, canvasRect.yMax - QC_H);
            Rect popupRect = new Rect(px, py, QC_W, QC_H);

            EditorGUI.DrawRect(new Rect(popupRect.x - 1, popupRect.y - 1, popupRect.width + 2, popupRect.height + 2),
                new Color(0.6f, 0.6f, 0.6f, 1f));
            EditorGUI.DrawRect(popupRect, new Color(0.18f, 0.18f, 0.18f, 1f));

            GUILayout.BeginArea(popupRect);
            GUILayout.Space(6);
            EditorGUILayout.LabelField("  Create Node", EditorStyles.boldLabel);
            HLine();
            GUILayout.Space(4);

            GUI.SetNextControlName("QC_Name");
            string newName = EditorGUILayout.TextField("Name", _quickCreateName);
            if (newName != _quickCreateName)
            {
                _quickCreateName = newName;
                if (!_quickCreateIdEdited) _quickCreateId = Slugify(_quickCreateName);
            }

            GUI.SetNextControlName("QC_ID");
            string newId = EditorGUILayout.TextField("ID", _quickCreateId);
            if (newId != _quickCreateId) { _quickCreateId = newId; _quickCreateIdEdited = true; }

            _quickCreateBranch = (SkillBranch)EditorGUILayout.EnumPopup("Branch", _quickCreateBranch);

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            bool valid = !string.IsNullOrWhiteSpace(_quickCreateName) && !string.IsNullOrWhiteSpace(_quickCreateId);
            GUI.enabled = valid;
            if (GUILayout.Button("Create", GUILayout.Height(22))) { QuickCreateNode(); CloseQuickCreate(); }
            GUI.enabled = true;
            if (GUILayout.Button("Cancel", GUILayout.Height(22))) CloseQuickCreate();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            if (Event.current.type == EventType.Repaint)
                EditorGUI.FocusTextInControl("QC_Name");

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            { CloseQuickCreate(); Event.current.Use(); }

            if (valid && Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            { QuickCreateNode(); CloseQuickCreate(); Event.current.Use(); }

            if (Event.current.type == EventType.MouseDown && popupRect.Contains(Event.current.mousePosition))
                Event.current.Use();
        }

        private void QuickCreateNode()
        {
            string id = string.IsNullOrWhiteSpace(_quickCreateId) ? Slugify(_quickCreateName) : _quickCreateId.Trim();
            id = UniqueId(id);

            var node = CreateAsset<SkillNodeSO>(_newFolder, id + ".asset");
            if (node == null) return;

            node.nodeId       = id;
            node.displayName  = _quickCreateName.Trim();
            node.branch       = _quickCreateBranch;
            node.graphPosition = SnapToGrid(_quickCreateCanvasPos);

            // Seed default cost if one is configured
            if (_defaultCostResource != null)
                node.costsPerRank.Add(new ResourceCost
                {
                    resource      = _defaultCostResource,
                    amountPerRank = _defaultCostAmount
                });

            Dirty(node);

            _tree.allNodes ??= new List<SkillNodeSO>();
            _tree.allNodes.Add(node);
            Dirty(_tree);
            AssetDatabase.SaveAssets();

            _selected            = node;
            _leftTab             = 1;
            _inspectorCanvasPos  = node.graphPosition;
            _inspectorOpen       = true;
            Repaint();
        }

        private void CloseQuickCreate()
        {
            _quickCreateOpen    = false;
            _quickCreateName    = "";
            _quickCreateId      = "";
            _quickCreateIdEdited = false;
            GUI.FocusControl(null);
            Repaint();
        }

        // =========================================================================
        //  Inline node inspector  (resizable)
        // =========================================================================
        private void DrawInlineInspector(Rect canvasRect)
        {
            if (_selected == null) { _inspectorOpen = false; return; }

            Vector2 nodeScreen = W2C(_selected.graphPosition) + new Vector2(canvasRect.x, canvasRect.y);
            float px = nodeScreen.x + (NODE_W * _zoom * 0.5f) - _insW * 0.5f;
            float py = nodeScreen.y + NODE_H * _zoom + 8f;
            px = Mathf.Clamp(px, canvasRect.x + 2f, canvasRect.xMax - _insW - 2f);
            py = Mathf.Clamp(py, canvasRect.y + 2f, canvasRect.yMax - _insH - 2f);

            Rect ins = new Rect(px, py, _insW, _insH);
            _inspectorScreenRect = ins;

            // Border + background
            EditorGUI.DrawRect(new Rect(ins.x - 1, ins.y - 1, ins.width + 2, ins.height + 2),
                new Color(0.55f, 0.55f, 0.55f, 1f));
            EditorGUI.DrawRect(ins, new Color(0.15f, 0.15f, 0.15f, 0.97f));

            GUILayout.BeginArea(ins);

            // Title bar
            EditorGUI.DrawRect(new Rect(0, 0, _insW, 22f), new Color(0.1f, 0.1f, 0.1f, 1f));
            EditorGUI.DrawRect(new Rect(0, 0, 4f, 22f), BranchColor(_selected.branch));
            GUI.Label(new Rect(10, 2, _insW - 40, 18),
                _selected.displayName ?? _selected.nodeId,
                new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white } });
            if (GUI.Button(new Rect(_insW - 22, 2, 18, 18), "✕",
                    new GUIStyle(EditorStyles.miniButton) { normal = { textColor = new Color(1f, 0.4f, 0.4f) } }))
            {
                _inspectorOpen = false;
                GUILayout.EndArea();
                Repaint();
                return;
            }

            GUILayout.Space(26);
            _inspectorScroll = GUILayout.BeginScrollView(_inspectorScroll);
            EditorGUI.BeginChangeCheck();

            // ── Identity ──────────────────────────────────────────────────────────
            SmallSection("IDENTITY");
            _selected.nodeId      = Field("Node ID",      _selected.nodeId);
            _selected.displayName = Field("Display Name", _selected.displayName);
            _selected.branch      = (SkillBranch)EditorGUILayout.EnumPopup("Branch", _selected.branch);
            _selected.icon        = (Sprite)EditorGUILayout.ObjectField("Icon", _selected.icon, typeof(Sprite), false);
            EditorGUILayout.LabelField("Description", EditorStyles.miniLabel);
            _selected.description = EditorGUILayout.TextArea(_selected.description, GUILayout.MinHeight(36));

            // ── Ranks ─────────────────────────────────────────────────────────────
            GUILayout.Space(4); HLine();
            SmallSection("RANKS");
            _selected.maxRanks = EditorGUILayout.IntSlider("Max Ranks", _selected.maxRanks, 1, 10);

            // ── Costs ─────────────────────────────────────────────────────────────
            GUILayout.Space(4); HLine();
            DrawCostEditor(_selected, ref _costsScroll, 130f);

            // ── Prerequisites ─────────────────────────────────────────────────────
            GUILayout.Space(4); HLine();
            SmallSection("PREREQUISITES");
            if (_selected.prerequisites != null)
            {
                for (int i = _selected.prerequisites.Count - 1; i >= 0; i--)
                {
                    var entry = _selected.prerequisites[i];
                    GUILayout.BeginHorizontal(EditorStyles.helpBox);
                    if (entry?.node != null)
                    {
                        var dot = new GUIStyle(EditorStyles.label)
                            { normal = { textColor = BranchColor(entry.node.branch) } };
                        GUILayout.Label("●", dot, GUILayout.Width(12));
                        GUILayout.Label(entry.node.displayName, GUILayout.ExpandWidth(true));
                        EditorGUILayout.LabelField("≥", EditorStyles.miniLabel, GUILayout.Width(12));
                        int nr = EditorGUILayout.IntField(entry.requiredRank, GUILayout.Width(24));
                        if (nr != entry.requiredRank) { entry.requiredRank = Mathf.Max(1, nr); Dirty(_selected); }
                    }
                    else GUILayout.Label("(missing)", EditorStyles.miniLabel, GUILayout.ExpandWidth(true));

                    GUI.color = new Color(1f, 0.35f, 0.35f);
                    if (GUILayout.Button("✕", GUILayout.Width(18))) { _selected.prerequisites.RemoveAt(i); Dirty(_selected); }
                    GUI.color = Color.white;
                    GUILayout.EndHorizontal();
                }
            }

            if (_tree?.allNodes != null)
            {
                var candidates = _tree.allNodes.Where(n => n != null && n != _selected &&
                    (_selected.prerequisites == null ||
                     !_selected.prerequisites.Any(e => e?.node == n))).ToArray();
                if (candidates.Length > 0)
                {
                    EditorGUILayout.LabelField("Quick add (rank 1):", EditorStyles.miniLabel);
                    for (int qi = 0; qi < candidates.Length; qi += 2)
                    {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button(candidates[qi].displayName ?? candidates[qi].nodeId,
                                EditorStyles.miniButton, GUILayout.ExpandWidth(true)))
                        {
                            _selected.prerequisites ??= new List<PrerequisiteEntry>();
                            _selected.prerequisites.Add(new PrerequisiteEntry { node = candidates[qi], requiredRank = 1 });
                            Dirty(_selected);
                        }
                        if (qi + 1 < candidates.Length)
                            if (GUILayout.Button(candidates[qi + 1].displayName ?? candidates[qi + 1].nodeId,
                                    EditorStyles.miniButton, GUILayout.ExpandWidth(true)))
                            {
                                _selected.prerequisites ??= new List<PrerequisiteEntry>();
                                _selected.prerequisites.Add(new PrerequisiteEntry { node = candidates[qi + 1], requiredRank = 1 });
                                Dirty(_selected);
                            }
                        GUILayout.EndHorizontal();
                    }
                }
            }

            // ── Effects ───────────────────────────────────────────────────────────
            GUILayout.Space(4); HLine();
            SmallSection("EFFECTS");
            var effects = _selected.effects;
            if (effects != null)
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    var eff = effects[i];
                    if (eff == null) continue;
                    GUILayout.BeginHorizontal(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"[{i}] {eff.GetType().Name}", EditorStyles.boldLabel);
                    if (GUILayout.Button("▲", GUILayout.Width(20)) && i > 0) { (effects[i], effects[i - 1]) = (effects[i - 1], effects[i]); Dirty(_selected); }
                    if (GUILayout.Button("▼", GUILayout.Width(20)) && i < effects.Count - 1) { (effects[i], effects[i + 1]) = (effects[i + 1], effects[i]); Dirty(_selected); }
                    GUI.color = new Color(1f, 0.35f, 0.35f);
                    if (GUILayout.Button("✕", GUILayout.Width(20))) { effects.RemoveAt(i); Dirty(_selected); GUILayout.EndHorizontal(); break; }
                    GUI.color = Color.white;
                    GUILayout.EndHorizontal();
                    EditorGUI.indentLevel++;
                    DrawEffectFields(eff);
                    EditorGUI.indentLevel--;
                    EditorGUILayout.LabelField($"→ {eff.GetDescription()}", EditorStyles.miniLabel);
                    GUILayout.Space(2);
                }
            }

            GUILayout.BeginHorizontal();
            _effectTypeIndex = EditorGUILayout.Popup(_effectTypeIndex, EffectTypeNames);
            if (GUILayout.Button("+ Add", GUILayout.Width(55)))
            {
                _selected.effects ??= new List<SkillEffect>();
                SkillEffect newEff = _effectTypeIndex switch
                {
                    0 => new StatMultiplierEffect(),
                    1 => new StatFlatBonusEffect(),
                    _ => null
                };
                if (newEff != null) { _selected.effects.Add(newEff); Dirty(_selected); }
            }
            GUILayout.EndHorizontal();

            // ── Visibility ────────────────────────────────────────────────────────
            GUILayout.Space(4); HLine();
            SmallSection("VISIBILITY");
            _selected.useGlobalVisibilityRules = EditorGUILayout.Toggle("Use Global Rules", _selected.useGlobalVisibilityRules);
            if (!_selected.useGlobalVisibilityRules)
            {
                _selected.revealBoxRank  = EditorGUILayout.IntField("Reveal Box At",  _selected.revealBoxRank);
                _selected.revealInfoRank = EditorGUILayout.IntField("Reveal Info At", _selected.revealInfoRank);
                _selected.unlockRank     = EditorGUILayout.IntField("Unlock At",      _selected.unlockRank);
            }

            GUILayout.Space(4); HLine();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Ping Asset",   EditorStyles.miniButton)) EditorGUIUtility.PingObject(_selected);
            if (GUILayout.Button("Select Asset", EditorStyles.miniButton)) Selection.activeObject = _selected;
            GUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck()) { Dirty(_selected); AssetDatabase.SaveAssets(); Repaint(); }

            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // Draw the resize grip visual
            DrawResizeHandle(ins);

            // Eat clicks inside inspector body (resize drag handled in HandleInspectorResizeEvents at top of OnGUI)
            Event ev = Event.current;
            if (!_insResizing && ev.type == EventType.MouseDown && ins.Contains(ev.mousePosition))
                ev.Use();
        }

        private void DrawResizeHandle(Rect ins)
        {
            Rect handleRect = new Rect(ins.xMax - RESIZE_HANDLE, ins.yMax - RESIZE_HANDLE, RESIZE_HANDLE, RESIZE_HANDLE);
            Color gripCol   = _insResizing ? new Color(0.4f, 0.7f, 1f, 1f) : new Color(0.6f, 0.6f, 0.6f, 0.8f);
            EditorGUI.DrawRect(handleRect, new Color(0.1f, 0.1f, 0.1f, 0.6f));
            float d = 2.5f, sp = 3.5f;
            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3 - row; col++)
            {
                float dx = handleRect.xMax - 3f - col * sp;
                float dy = handleRect.yMax - 3f - row * sp;
                EditorGUI.DrawRect(new Rect(dx - d * 0.5f, dy - d * 0.5f, d, d), gripCol);
            }
            EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.ResizeUpLeft);
        }

        // =========================================================================
        //  Divider
        // =========================================================================
        private void DrawDivider(Rect r)
        {
            Color col = _divDrag ? new Color(0.35f, 0.65f, 1f, 0.9f) : new Color(0.08f, 0.08f, 0.08f, 1f);
            EditorGUI.DrawRect(r, col);
            EditorGUIUtility.AddCursorRect(r, MouseCursor.ResizeHorizontal);
            Event e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown when r.Contains(e.mousePosition) && e.button == 0:
                    _divDrag = true; e.Use(); break;
                case EventType.MouseDrag when _divDrag:
                    _leftW = Mathf.Clamp(e.mousePosition.x, LEFT_W_MIN, LEFT_W_MAX);
                    e.Use(); Repaint(); break;
                case EventType.MouseUp when _divDrag:
                    _divDrag = false; e.Use(); Repaint(); break;
            }
        }

        // =========================================================================
        //  Toolbar
        // =========================================================================
        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(TOOLBAR_H));
            EditorGUI.BeginChangeCheck();
            _tree = (SkillTreeSO)EditorGUILayout.ObjectField(_tree, typeof(SkillTreeSO), false, GUILayout.Width(200));
            if (EditorGUI.EndChangeCheck()) { _selected = null; _inspectorOpen = false; Repaint(); }

            GUILayout.Space(6);
            GUI.enabled = _tree != null;
            if (GUILayout.Button("Auto Layout", EditorStyles.toolbarButton, GUILayout.Width(85))) AutoLayout();
            if (GUILayout.Button("Center",      EditorStyles.toolbarButton, GUILayout.Width(55))) CenterView();
            if (GUILayout.Button("1×",          EditorStyles.toolbarButton, GUILayout.Width(28))) { _zoom = 1f; Repaint(); }
            GUI.enabled = true;

            GUILayout.Space(10);
            // Default cost resource picker in toolbar for quick setup
            EditorGUILayout.LabelField("Default Cost:", EditorStyles.miniLabel, GUILayout.Width(76));
            _defaultCostResource = (ResourceDefinitionSO)EditorGUILayout.ObjectField(
                _defaultCostResource, typeof(ResourceDefinitionSO), false, GUILayout.Width(110));
            _defaultCostAmount = Mathf.Max(0,
                EditorGUILayout.IntField(_defaultCostAmount, GUILayout.Width(45)));

            GUILayout.FlexibleSpace();
            GUILayout.Label($"Nodes: {_tree?.allNodes?.Count ?? 0}   zoom {_zoom:F2}×", EditorStyles.miniLabel);
            GUILayout.EndHorizontal();
        }

        // =========================================================================
        //  Left panel
        // =========================================================================
        private void DrawLeftPanel(Rect r)
        {
            GUILayout.BeginArea(r);
            _leftTab    = GUILayout.Toolbar(_leftTab, new[] { "Tree", "Node", "New Node" });
            GUILayout.Space(4);
            _leftScroll = GUILayout.BeginScrollView(_leftScroll);
            switch (_leftTab)
            {
                case 0: DrawTreeTab();    break;
                case 1: DrawNodeTab();    break;
                case 2: DrawNewNodeTab(); break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ── Tree tab ──────────────────────────────────────────────────────────────
        private void DrawTreeTab()
        {
            Section("CREATE SKILL TREE");
            _newTreeName   = Field("Name",        _newTreeName);
            _newTreeFolder = Field("Save Folder", _newTreeFolder);
            if (GUILayout.Button("Create SkillTreeSO"))
            {
                var t = CreateAsset<SkillTreeSO>(_newTreeFolder, _newTreeName + ".asset");
                if (t != null) { t.treeName = _newTreeName; Dirty(t); _tree = t; }
            }

            if (_tree == null) return;

            GUILayout.Space(8); HLine();
            Section("TREE SETTINGS");
            EditorGUI.BeginChangeCheck();
            _tree.treeName        = Field("Tree Name",   _tree.treeName);
            _tree.treeDescription = AreaField("Description", _tree.treeDescription);
            if (EditorGUI.EndChangeCheck()) Dirty(_tree);

            GUILayout.Space(8); HLine();
            Section($"NODES IN TREE  ({_tree.allNodes?.Count ?? 0})");
            _nodeListScroll = GUILayout.BeginScrollView(_nodeListScroll, GUILayout.MaxHeight(200));
            if (_tree.allNodes != null)
            {
                for (int i = 0; i < _tree.allNodes.Count; i++)
                {
                    var n = _tree.allNodes[i];
                    if (n == null) continue;
                    bool isSel = n == _selected;
                    GUILayout.BeginHorizontal();
                    var dot = new GUIStyle(EditorStyles.label) { normal = { textColor = BranchColor(n.branch) } };
                    GUILayout.Label("●", dot, GUILayout.Width(16));
                    var style = isSel ? new GUIStyle(EditorStyles.boldLabel) : EditorStyles.label;
                    if (GUILayout.Button(n.displayName ?? n.nodeId ?? n.name, style, GUILayout.ExpandWidth(true)))
                    {
                        _selected = n; _leftTab = 1;
                        EditorGUIUtility.PingObject(n); GUI.FocusControl(null); Repaint();
                    }
                    GUI.color = new Color(1f, 0.35f, 0.35f);
                    if (GUILayout.Button("✕", GUILayout.Width(20)))
                    {
                        if (EditorUtility.DisplayDialog("Remove",
                            $"Remove '{n.displayName}' from tree?", "Remove", "Cancel"))
                        {
                            _tree.allNodes.RemoveAt(i); Dirty(_tree);
                            if (_selected == n) { _selected = null; _inspectorOpen = false; }
                            Repaint(); break;
                        }
                    }
                    GUI.color = Color.white;
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
            GUILayout.Space(4);
            if (GUILayout.Button("Add Existing Node Asset"))
            {
                var path = EditorUtility.OpenFilePanel("Select SkillNodeSO", "Assets", "asset");
                if (!string.IsNullOrEmpty(path))
                {
                    path = "Assets" + path.Replace(Application.dataPath, "");
                    var node = AssetDatabase.LoadAssetAtPath<SkillNodeSO>(path);
                    if (node != null && !_tree.allNodes.Contains(node))
                    { _tree.allNodes.Add(node); Dirty(_tree); Repaint(); }
                }
            }
        }

        // ── Node tab ──────────────────────────────────────────────────────────────
        private void DrawNodeTab()
        {
            if (_tree == null)     { EditorGUILayout.HelpBox("Load a SkillTreeSO first.",    MessageType.Info); return; }
            if (_selected == null) { EditorGUILayout.HelpBox("Click a node on the canvas.", MessageType.Info); return; }

            EditorGUI.BeginChangeCheck();

            Section("IDENTITY");
            _selected.nodeId      = Field("Node ID",      _selected.nodeId);
            _selected.displayName = Field("Display Name", _selected.displayName);
            _selected.branch      = (SkillBranch)EditorGUILayout.EnumPopup("Branch", _selected.branch);
            _selected.icon        = (Sprite)EditorGUILayout.ObjectField("Icon", _selected.icon, typeof(Sprite), false);
            GUILayout.Space(4);
            EditorGUILayout.LabelField("Description", EditorStyles.miniLabel);
            _selected.description = EditorGUILayout.TextArea(_selected.description, GUILayout.MinHeight(48));

            GUILayout.Space(6); HLine();
            Section("RANKS");
            _selected.maxRanks = EditorGUILayout.IntSlider("Max Ranks", _selected.maxRanks, 1, 10);

            GUILayout.Space(6); HLine();
            DrawCostEditor(_selected, ref _costsScroll, 160f);

            GUILayout.Space(6); HLine();
            Section("PREREQUISITES");
            _prereqScroll = GUILayout.BeginScrollView(_prereqScroll, GUILayout.MaxHeight(110));
            if (_selected.prerequisites != null)
            {
                for (int i = _selected.prerequisites.Count - 1; i >= 0; i--)
                {
                    var entry = _selected.prerequisites[i];
                    GUILayout.BeginHorizontal(EditorStyles.helpBox);
                    if (entry?.node != null)
                    {
                        var dot = new GUIStyle(EditorStyles.label)
                            { normal = { textColor = BranchColor(entry.node.branch) } };
                        GUILayout.Label("●", dot, GUILayout.Width(14));
                        GUILayout.Label($"{entry.node.displayName}  [{entry.node.nodeId}]",
                            GUILayout.ExpandWidth(true));
                        EditorGUILayout.LabelField("Req rank:", EditorStyles.miniLabel, GUILayout.Width(60));
                        int nr = EditorGUILayout.IntField(entry.requiredRank, GUILayout.Width(30));
                        if (nr != entry.requiredRank) { entry.requiredRank = Mathf.Max(1, nr); Dirty(_selected); }
                    }
                    else GUILayout.Label("(missing reference)", EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                    GUI.color = new Color(1f, 0.35f, 0.35f);
                    if (GUILayout.Button("✕", GUILayout.Width(20)))
                    { _selected.prerequisites.RemoveAt(i); Dirty(_selected); }
                    GUI.color = Color.white;
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            _prereqCandidate = (SkillNodeSO)EditorGUILayout.ObjectField(
                _prereqCandidate, typeof(SkillNodeSO), false);
            EditorGUILayout.LabelField("Rank:", EditorStyles.miniLabel, GUILayout.Width(35));
            _prereqCandidateRank = Mathf.Max(1,
                EditorGUILayout.IntField(_prereqCandidateRank, GUILayout.Width(30)));
            bool alreadyAdded = _selected.prerequisites != null &&
                                _selected.prerequisites.Any(e => e?.node == _prereqCandidate);
            GUI.enabled = _prereqCandidate != null && _prereqCandidate != _selected && !alreadyAdded;
            if (GUILayout.Button("Add", GUILayout.Width(40)))
            {
                _selected.prerequisites ??= new List<PrerequisiteEntry>();
                _selected.prerequisites.Add(new PrerequisiteEntry
                    { node = _prereqCandidate, requiredRank = _prereqCandidateRank });
                Dirty(_selected); _prereqCandidate = null; _prereqCandidateRank = 1;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(6); HLine();
            Section("EFFECTS");
            _effectsScroll = GUILayout.BeginScrollView(_effectsScroll, GUILayout.MaxHeight(220));
            var effects = _selected.effects;
            if (effects != null)
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    var eff = effects[i];
                    if (eff == null) continue;
                    GUILayout.BeginHorizontal(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"[{i}] {eff.GetType().Name}", EditorStyles.boldLabel);
                    if (GUILayout.Button("▲", GUILayout.Width(22)) && i > 0)
                    { (effects[i], effects[i - 1]) = (effects[i - 1], effects[i]); Dirty(_selected); }
                    if (GUILayout.Button("▼", GUILayout.Width(22)) && i < effects.Count - 1)
                    { (effects[i], effects[i + 1]) = (effects[i + 1], effects[i]); Dirty(_selected); }
                    GUI.color = new Color(1f, 0.35f, 0.35f);
                    if (GUILayout.Button("✕", GUILayout.Width(22)))
                    { effects.RemoveAt(i); Dirty(_selected); GUILayout.EndHorizontal(); break; }
                    GUI.color = Color.white;
                    GUILayout.EndHorizontal();
                    EditorGUI.indentLevel++;
                    DrawEffectFields(eff);
                    EditorGUI.indentLevel--;
                    EditorGUILayout.LabelField($"→ {eff.GetDescription()}", EditorStyles.miniLabel);
                    GUILayout.Space(3);
                }
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            _effectTypeIndex = EditorGUILayout.Popup(_effectTypeIndex, EffectTypeNames);
            if (GUILayout.Button("+ Add Effect", GUILayout.Width(90)))
            {
                _selected.effects ??= new List<SkillEffect>();
                SkillEffect newEff = _effectTypeIndex switch
                {
                    0 => new StatMultiplierEffect(),
                    1 => new StatFlatBonusEffect(),
                    _ => null
                };
                if (newEff != null) { _selected.effects.Add(newEff); Dirty(_selected); }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6); HLine();
            Section("VISIBILITY");
            _selected.useGlobalVisibilityRules =
                EditorGUILayout.Toggle("Use Global Rules", _selected.useGlobalVisibilityRules);
            if (!_selected.useGlobalVisibilityRules)
            {
                _selected.revealBoxRank  = EditorGUILayout.IntField("Reveal Box At",  _selected.revealBoxRank);
                _selected.revealInfoRank = EditorGUILayout.IntField("Reveal Info At", _selected.revealInfoRank);
                _selected.unlockRank     = EditorGUILayout.IntField("Unlock At",      _selected.unlockRank);
            }

            GUILayout.Space(6); HLine();
            Section("GRAPH POSITION");
            _selected.graphPosition = EditorGUILayout.Vector2Field("Position", _selected.graphPosition);

            GUILayout.Space(6); HLine();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Ping Asset"))   EditorGUIUtility.PingObject(_selected);
            if (GUILayout.Button("Select Asset")) Selection.activeObject = _selected;
            GUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck()) { Dirty(_selected); AssetDatabase.SaveAssets(); }
        }

        private void DrawEffectFields(SkillEffect eff)
        {
            switch (eff)
            {
                case StatMultiplierEffect sme:
                    sme.statType   = (StatType)EditorGUILayout.EnumPopup("Stat", sme.statType);
                    sme.multiplier = EditorGUILayout.FloatField("Multiplier", sme.multiplier);
                    float pct = Mathf.Round((sme.multiplier - 1f) * 100f);
                    EditorGUILayout.LabelField($"  = {(pct >= 0 ? "+" : "")}{pct}%", EditorStyles.miniLabel);
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Presets:", EditorStyles.miniLabel, GUILayout.Width(50));
                    foreach (var p in new[] { 1.1f, 1.2f, 1.25f, 1.5f, 2.0f })
                        if (GUILayout.Button($"+{(int)((p - 1) * 100)}%", EditorStyles.miniButton))
                        { sme.multiplier = p; Dirty(_selected); }
                    GUILayout.EndHorizontal();
                    break;

                case StatFlatBonusEffect sfb:
                    sfb.statType = (StatType)EditorGUILayout.EnumPopup("Stat", sfb.statType);
                    sfb.bonus    = EditorGUILayout.FloatField("Bonus", sfb.bonus);
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Presets:", EditorStyles.miniLabel, GUILayout.Width(50));
                    foreach (var p in new[] { 1f, 2f, 5f, 10f })
                        if (GUILayout.Button($"+{p}", EditorStyles.miniButton))
                        { sfb.bonus = p; Dirty(_selected); }
                    GUILayout.EndHorizontal();
                    break;

                default:
                    EditorGUILayout.LabelField($"No editor for {eff.GetType().Name}", EditorStyles.miniLabel);
                    break;
            }
        }

        // ── New Node tab ──────────────────────────────────────────────────────────
        private void DrawNewNodeTab()
        {
            if (_tree == null) { EditorGUILayout.HelpBox("Load a SkillTreeSO first.", MessageType.Info); return; }

            Section("CREATE NEW NODE");
            _newId       = Field("Node ID *",      _newId);
            _newName     = Field("Display Name *", _newName);
            _newBranch   = (SkillBranch)EditorGUILayout.EnumPopup("Branch",    _newBranch);
            _newMaxRanks = EditorGUILayout.IntSlider("Max Ranks", _newMaxRanks, 1, 10);
            _newFolder   = Field("Save Folder", _newFolder);

            GUILayout.Space(4);
            EditorGUILayout.LabelField("Default Cost (added automatically):", EditorStyles.miniLabel);
            GUILayout.BeginHorizontal();
            _defaultCostResource = (ResourceDefinitionSO)EditorGUILayout.ObjectField(
                _defaultCostResource, typeof(ResourceDefinitionSO), false, GUILayout.Width(140));
            _defaultCostAmount = Mathf.Max(0,
                EditorGUILayout.IntField(_defaultCostAmount, GUILayout.Width(55)));
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            bool valid = !string.IsNullOrWhiteSpace(_newId) && !string.IsNullOrWhiteSpace(_newName);
            if (!valid) EditorGUILayout.HelpBox("Node ID and Display Name are required.", MessageType.Warning);
            GUI.enabled = valid;
            if (GUILayout.Button("Create & Add to Tree", GUILayout.Height(28))) CreateAndAddNode();
            GUI.enabled = true;

            GUILayout.Space(8); HLine();
            Section("BULK CREATE");
            EditorGUILayout.HelpBox("One 'ID|Name' per line. Pick branch then press Create All.", MessageType.Info);
            _bulkText   = EditorGUILayout.TextArea(_bulkText ?? "", GUILayout.MinHeight(80));
            _bulkBranch = (SkillBranch)EditorGUILayout.EnumPopup("Branch", _bulkBranch);
            if (GUILayout.Button("Create All Nodes")) BulkCreate();
        }

        private void BulkCreate()
        {
            if (string.IsNullOrWhiteSpace(_bulkText)) return;
            int created = 0;
            foreach (var rawLine in _bulkText.Split('\n'))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;
                var    parts = line.Split('|');
                string id    = parts[0].Trim();
                string name  = parts.Length > 1 ? parts[1].Trim() : id;
                if (string.IsNullOrEmpty(id)) continue;

                var node = CreateAsset<SkillNodeSO>(_newFolder, id + ".asset");
                if (node == null) continue;

                node.nodeId        = id;
                node.displayName   = name;
                node.branch        = _bulkBranch;
                node.graphPosition = AutoPosition(_bulkBranch);

                if (_defaultCostResource != null)
                    node.costsPerRank.Add(new ResourceCost
                    {
                        resource      = _defaultCostResource,
                        amountPerRank = _defaultCostAmount
                    });

                Dirty(node);
                _tree.allNodes ??= new List<SkillNodeSO>();
                _tree.allNodes.Add(node);
                created++;
            }
            Dirty(_tree); AssetDatabase.SaveAssets(); _bulkText = ""; CenterView(); Repaint();
            Debug.Log($"[SkillTreeEditor] Bulk created {created} nodes.");
        }

        // =========================================================================
        //  Canvas
        // =========================================================================
        private void DrawCanvas(Rect canvasRect)
        {
            if (_tree == null)
            {
                EditorGUI.LabelField(new Rect(canvasRect.x + 20, canvasRect.y + 20, 400, 24),
                    "Assign or create a SkillTreeSO in the toolbar / Tree tab.", EditorStyles.boldLabel);
                return;
            }

            GUI.Box(canvasRect, GUIContent.none);
            GUI.BeginClip(canvasRect);
            DrawGrid(canvasRect.size);
            DrawConnections();
            DrawNodes();
            GUI.EndClip();
            HandleCanvasInput(canvasRect);
            HandleZoom(canvasRect);
            if (_dragging != null || _panning) Repaint();
        }

        private void DrawGrid(Vector2 size)
        {
            float sp = GRID_SIZE * _zoom;
            Handles.color = ColGrid;
            for (float x = _offset.x % sp; x < size.x; x += sp)
                Handles.DrawLine(new Vector3(x, 0), new Vector3(x, size.y));
            for (float y = _offset.y % sp; y < size.y; y += sp)
                Handles.DrawLine(new Vector3(0, y), new Vector3(size.x, y));
            Handles.color = Color.white;
        }

        private void DrawConnections()
        {
            if (_tree?.allNodes == null) return;
            foreach (var node in _tree.allNodes)
            {
                if (node?.prerequisites == null) continue;
                Rect   toR   = NodeRect(node);
                var    toTop = new Vector2(toR.center.x, toR.yMin);
                foreach (var entry in node.prerequisites)
                {
                    if (entry?.node == null) continue;
                    Rect fromR   = NodeRect(entry.node);
                    var  fromBot = new Vector2(fromR.center.x, fromR.yMax);
                    float tan    = Mathf.Abs(toTop.y - fromBot.y) * 0.5f;
                    Color c      = _selected == node ? Color.yellow : ColLine;
                    Handles.DrawBezier(fromBot, toTop,
                        new Vector3(fromBot.x, fromBot.y + tan),
                        new Vector3(toTop.x,   toTop.y  - tan),
                        c, null, _selected == node ? 3f : 1.8f);
                    if (entry.requiredRank > 1)
                    {
                        var mid = (fromBot + toTop) * 0.5f;
                        var lr  = new Rect(mid.x - 14, mid.y - 8, 28, 16);
                        EditorGUI.DrawRect(lr, new Color(0, 0, 0, 0.6f));
                        GUI.Label(lr, $"≥{entry.requiredRank}",
                            new GUIStyle(EditorStyles.miniLabel)
                            {
                                normal    = { textColor = Color.yellow },
                                alignment = TextAnchor.MiddleCenter
                            });
                    }
                }
            }
        }

        private void DrawNodes()
        {
            if (_tree?.allNodes == null) return;
            foreach (var n in _tree.allNodes)
                if (n != null) DrawNode(n);
        }

        private void DrawNode(SkillNodeSO node)
        {
            Rect r   = NodeRect(node);
            bool sel = node == _selected;

            EditorGUI.DrawRect(new Rect(r.x + 3, r.y + 3, r.width, r.height), new Color(0, 0, 0, 0.45f));
            Color bg = BranchColor(node.branch);
            EditorGUI.DrawRect(r, sel ? Color.Lerp(bg, Color.white, 0.22f) : bg);
            if (sel) Handles.DrawSolidRectangleWithOutline(r, Color.clear, Color.white);

            if (node.icon != null)
                GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, 18 * _zoom, 18 * _zoom),
                    node.icon.texture, ScaleMode.ScaleToFit);

            GUI.Label(new Rect(r.x + 2, r.y + 4, r.width - 4, r.height - 16),
                node.displayName ?? node.name,
                new GUIStyle(EditorStyles.label)
                {
                    normal    = { textColor = Color.white },
                    fontStyle = sel ? FontStyle.Bold : FontStyle.Normal,
                    fontSize  = Mathf.Max(8, Mathf.RoundToInt(11 * _zoom)),
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap  = true
                });

            // Cost preview under node name (first resource only, to keep it compact)
            if (node.costsPerRank != null && node.costsPerRank.Count > 0)
            {
                var firstCost = node.costsPerRank[0];
                if (firstCost.resource != null)
                {
                    string costLabel = firstCost.Format(1);
                    if (node.costsPerRank.Count > 1) costLabel += "…";
                    GUI.Label(new Rect(r.x, r.yMax - 14 * _zoom, r.width, 14 * _zoom),
                        costLabel,
                        new GUIStyle(EditorStyles.label)
                        {
                            normal    = { textColor = new Color(1, 1, 0.6f, 0.9f) },
                            fontSize  = Mathf.Max(7, Mathf.RoundToInt(8 * _zoom)),
                            alignment = TextAnchor.LowerCenter
                        });
                }
            }

            // Effect count badge
            int effCount = node.effects?.Count ?? 0;
            if (effCount > 0)
            {
                var bR = new Rect(r.xMax - 18 * _zoom, r.y + 2, 16 * _zoom, 14 * _zoom);
                EditorGUI.DrawRect(bR, new Color(0, 0, 0, 0.5f));
                GUI.Label(bR, effCount.ToString(),
                    new GUIStyle(EditorStyles.label)
                    {
                        normal    = { textColor = Color.white },
                        fontSize  = Mathf.Max(7, Mathf.RoundToInt(9 * _zoom)),
                        alignment = TextAnchor.MiddleCenter
                    });
            }

            // Max ranks badge
            if (node.maxRanks > 1)
                GUI.Label(new Rect(r.x + 2, r.y + 2, 30, 14), $"×{node.maxRanks}",
                    new GUIStyle(EditorStyles.label)
                    {
                        normal    = { textColor = new Color(1, 1, 0.5f, 1) },
                        fontSize  = Mathf.Max(7, Mathf.RoundToInt(9 * _zoom)),
                        alignment = TextAnchor.UpperLeft
                    });
        }

        // ── Canvas input ──────────────────────────────────────────────────────────
        private void HandleCanvasInput(Rect canvasRect)
        {
            if (_divDrag || _insResizing) return;
            Event  e = Event.current;
            Vector2 m = e.mousePosition - new Vector2(canvasRect.x, canvasRect.y);
            if (!canvasRect.Contains(e.mousePosition)) return;
            if (_inspectorOpen && _selected != null && _inspectorScreenRect.Contains(e.mousePosition)) return;

            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0 && !_quickCreateOpen:
                    var hit = NodeAt(m);
                    if (hit != null)
                    {
                        _selected = hit; _dragging = hit;
                        _dragOffset = m - W2C(hit.graphPosition);
                        Selection.activeObject = hit; _leftTab = 1;
                        _inspectorOpen = true; _inspectorCanvasPos = hit.graphPosition;
                    }
                    else { _selected = null; _inspectorOpen = false; }
                    GUI.FocusControl(null); e.Use(); Repaint();
                    break;

                case EventType.MouseDown when e.button == 1 && !_quickCreateOpen:
                    var hitNode = NodeAt(m);
                    if (hitNode == null && _tree != null)
                    {
                        _quickCreateCanvasPos = C2W(m);
                        _quickCreateOpen = true; _quickCreateName = ""; _quickCreateId = "";
                        _quickCreateIdEdited = false; _quickCreateBranch = _newBranch;
                        e.Use(); Repaint();
                    }
                    else if (hitNode != null)
                    {
                        _selected = hitNode;
                        var menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Edit Node"), false, () =>
                            { _leftTab = 1; _inspectorOpen = false; Repaint(); });
                        menu.AddItem(new GUIContent("Ping Asset"), false, () =>
                            EditorGUIUtility.PingObject(hitNode));
                        menu.AddSeparator("");
                        menu.AddItem(new GUIContent("Remove from Tree"), false, () =>
                        {
                            if (EditorUtility.DisplayDialog("Remove",
                                $"Remove '{hitNode.displayName}' from tree? (Asset kept on disk)", "Remove", "Cancel"))
                            {
                                _tree.allNodes.Remove(hitNode); Dirty(_tree);
                                if (_selected == hitNode) { _selected = null; _inspectorOpen = false; }
                                Repaint();
                            }
                        });
                        menu.ShowAsContext(); e.Use();
                    }
                    break;

                case EventType.MouseDown when e.button == 2:
                    _panning = true; _panStart = m; e.Use(); break;

                case EventType.MouseDrag when _dragging != null && e.button == 0:
                    var wp = C2W(m - _dragOffset);
                    wp.x = Mathf.Round(wp.x / GRID_SIZE) * GRID_SIZE;
                    wp.y = Mathf.Round(wp.y / GRID_SIZE) * GRID_SIZE;
                    _dragging.graphPosition = wp;
                    if (_inspectorOpen) _inspectorCanvasPos = wp;
                    EditorUtility.SetDirty(_dragging); e.Use(); Repaint(); break;

                case EventType.MouseDrag when _panning:
                    _offset += m - _panStart; _panStart = m; e.Use(); Repaint(); break;

                case EventType.MouseUp:
                    if (_dragging != null) AssetDatabase.SaveAssets();
                    _dragging = null; _panning = false; break;

                case EventType.KeyDown when e.keyCode == KeyCode.Delete && _selected != null && !_quickCreateOpen:
                    if (EditorUtility.DisplayDialog("Remove",
                        $"Remove '{_selected.displayName}' from tree? (Asset kept on disk)", "Remove", "Cancel"))
                    {
                        _tree.allNodes.Remove(_selected); Dirty(_tree);
                        _selected = null; _inspectorOpen = false; Repaint();
                    }
                    e.Use(); break;

                case EventType.KeyDown when e.keyCode == KeyCode.F && _selected != null && !_quickCreateOpen:
                    var sr  = NodeRect(_selected);
                    var csz = new Vector2(position.width - _leftW - DIV_W, position.height - TOOLBAR_H);
                    _offset = csz * 0.5f - new Vector2(sr.center.x - _offset.x, sr.center.y - _offset.y);
                    e.Use(); Repaint(); break;
            }
        }

        private void HandleZoom(Rect canvasRect)
        {
            Event e = Event.current;
            if (e.type == EventType.ScrollWheel && canvasRect.Contains(e.mousePosition)
                && !_quickCreateOpen && !_insResizing)
            {
                _zoom = Mathf.Clamp(_zoom - e.delta.y * 0.04f, ZOOM_MIN, ZOOM_MAX);
                e.Use(); Repaint();
            }
        }

        // =========================================================================
        //  Layout helpers
        // =========================================================================
        private void AutoLayout()
        {
            if (_tree?.allNodes == null) return;
            var cols = new Dictionary<SkillBranch, float>();
            var rows = new Dictionary<SkillBranch, float>();
            int ci = 0;
            foreach (SkillBranch b in Enum.GetValues(typeof(SkillBranch)))
            { cols[b] = ci++ * (NODE_W + 80f) + 40f; rows[b] = 60f; }
            foreach (var n in _tree.allNodes)
            {
                if (n == null) continue;
                n.graphPosition = new Vector2(cols[n.branch], rows[n.branch]);
                rows[n.branch] += NODE_H + 55f;
                EditorUtility.SetDirty(n);
            }
            AssetDatabase.SaveAssets(); CenterView();
        }

        private void CenterView()
        {
            if (_tree?.allNodes == null || _tree.allNodes.Count == 0) return;
            var (min, max) = Bounds();
            var center = (min + max) * 0.5f;
            var pSize  = new Vector2(position.width - _leftW - DIV_W, position.height - TOOLBAR_H);
            _offset = pSize * 0.5f - center * _zoom; Repaint();
        }

        private (Vector2 min, Vector2 max) Bounds()
        {
            var mn = Vector2.one * float.MaxValue;
            var mx = Vector2.one * float.MinValue;
            foreach (var n in _tree.allNodes)
            {
                if (n == null) continue;
                mn = Vector2.Min(mn, n.graphPosition);
                mx = Vector2.Max(mx, n.graphPosition + new Vector2(NODE_W, NODE_H));
            }
            return (mn, mx);
        }

        private Vector2 AutoPosition(SkillBranch branch)
        {
            float colX = (int)branch * (NODE_W + 80f) + 40f;
            float maxY = 60f;
            if (_tree.allNodes != null)
                foreach (var n in _tree.allNodes)
                    if (n != null && n.branch == branch)
                        maxY = Mathf.Max(maxY, n.graphPosition.y + NODE_H + 55f);
            return new Vector2(colX, maxY);
        }

        private Rect NodeRect(SkillNodeSO n)
        {
            var c = W2C(n.graphPosition);
            return new Rect(c.x, c.y, NODE_W * _zoom, NODE_H * _zoom);
        }

        private SkillNodeSO NodeAt(Vector2 m)
        {
            if (_tree?.allNodes == null) return null;
            for (int i = _tree.allNodes.Count - 1; i >= 0; i--)
            {
                var n = _tree.allNodes[i];
                if (n != null && NodeRect(n).Contains(m)) return n;
            }
            return null;
        }

        private Vector2 W2C(Vector2 w) => w * _zoom + _offset;
        private Vector2 C2W(Vector2 c) => (c - _offset) / _zoom;
        private Vector2 SnapToGrid(Vector2 w) => new Vector2(
            Mathf.Round(w.x / GRID_SIZE) * GRID_SIZE,
            Mathf.Round(w.y / GRID_SIZE) * GRID_SIZE);

        private Color BranchColor(SkillBranch b) => b switch
        {
            SkillBranch.Offense => ColOffense,
            SkillBranch.Control => ColControl,
            SkillBranch.Economy => ColEconomy,
            SkillBranch.Defense => ColDefense,
            _ => new Color(0.3f, 0.3f, 0.3f)
        };

        // =========================================================================
        //  Asset helpers
        // =========================================================================
        private void CreateAndAddNode()
        {
            var node = CreateAsset<SkillNodeSO>(_newFolder, _newId + ".asset");
            if (node == null) return;

            node.nodeId        = _newId;
            node.displayName   = _newName;
            node.branch        = _newBranch;
            node.maxRanks      = _newMaxRanks;
            node.graphPosition = AutoPosition(_newBranch);

            if (_defaultCostResource != null)
                node.costsPerRank.Add(new ResourceCost
                {
                    resource      = _defaultCostResource,
                    amountPerRank = _defaultCostAmount
                });

            Dirty(node);
            _tree.allNodes ??= new List<SkillNodeSO>();
            _tree.allNodes.Add(node);
            Dirty(_tree);
            AssetDatabase.SaveAssets();
            _selected = node; _leftTab = 1; _newId = _newName = "";
            Repaint();
            Debug.Log($"[SkillTreeEditor] Created '{node.displayName}'");
        }

        private T CreateAsset<T>(string folder, string fileName) where T : ScriptableObject
        {
            if (!System.IO.Directory.Exists(folder))
            { System.IO.Directory.CreateDirectory(folder); AssetDatabase.Refresh(); }
            string path  = AssetDatabase.GenerateUniqueAssetPath(System.IO.Path.Combine(folder, fileName));
            var    asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(asset);
            return asset;
        }

        // ── GUI helpers ───────────────────────────────────────────────────────────
        private static string Field(string label, string value)
            => EditorGUILayout.TextField(label, value ?? "");

        private static string AreaField(string label, string value)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
            return EditorGUILayout.TextArea(value ?? "", GUILayout.MinHeight(36));
        }

        private static void Section(string t)
        { GUILayout.Space(2); EditorGUILayout.LabelField(t, EditorStyles.boldLabel); }

        private static void SmallSection(string t)
        { GUILayout.Space(2); EditorGUILayout.LabelField(t, EditorStyles.miniLabel); }

        private static void HLine()
            => EditorGUI.DrawRect(GUILayoutUtility.GetRect(1, 1), new Color(0.35f, 0.35f, 0.35f));

        private static void Dirty(UnityEngine.Object obj) => EditorUtility.SetDirty(obj);

        private static string Slugify(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Trim().ToLower().Replace(" ", "_").Replace("-", "_");
        }

        private string UniqueId(string baseId)
        {
            if (_tree?.allNodes == null) return baseId;
            var existing = new HashSet<string>(
                _tree.allNodes.Where(n => n != null && !string.IsNullOrEmpty(n.nodeId))
                              .Select(n => n.nodeId));
            if (!existing.Contains(baseId)) return baseId;
            for (int i = 2; i < 999; i++)
            { string c = $"{baseId}_{i}"; if (!existing.Contains(c)) return c; }
            return baseId + "_" + Guid.NewGuid().ToString("N")[..6];
        }
    }
}
#endif