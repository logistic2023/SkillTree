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
        private const float NODE_W      = 72f;   // compact square nodes
        private const float NODE_H      = 72f;
        private const float GRID_SIZE   = 20f;
        private const float ZOOM_MIN      = 0.25f;
        private const float ZOOM_MAX      = 2.5f;
        private const float NODE_SPACING_X = 140f;  // generous horizontal spacing
        private const float NODE_SPACING_Y = 140f;  // generous vertical spacing
        private const float ARROW_SIZE     = 20f;

        private float _leftW    = LEFT_W_DEF;
        private bool  _divDrag  = false;

        // ── Colors ────────────────────────────────────────────────────────────────
        private static readonly Color ColNoBranch = new Color(0.3f, 0.3f, 0.3f);
        private static readonly Color ColLine    = new Color(0.65f, 0.65f, 0.65f, 0.85f);
        private static readonly Color ColGrid    = new Color(0.26f, 0.26f, 0.26f, 0.45f);

        // ── State ─────────────────────────────────────────────────────────────────
        private SkillTreeSO        _tree;
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
        private SkillBranchSO _newBranch;
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
        private SkillBranchSO _bulkBranch;
        private string        _branchFolder = "Assets/SkillTree_System/Data/SkillTree/Branches";

        // ── Arrow hover ───────────────────────────────────────────────────────────
        private SkillNodeSO _hoveredNode;

        // ── Selected edge (connection line) ───────────────────────────────────────
        private SkillNodeSO _selectedEdgeChild;  // the node that HAS the prerequisite
        private int         _selectedEdgeIndex;  // index into child.prerequisites
        private SkillNodeSO _hoveredEdgeChild;
        private int         _hoveredEdgeIndex = -1;

        // ── Wire drag (arrow → existing node = add prerequisite) ─────────────────
        private bool        _wireDragging;
        private SkillNodeSO _wireDragFrom;    // node the arrow belongs to
        private int         _wireDragArrowIdx; // which arrow was pressed (for click-spawn direction)
        private Vector2     _wireDragStart;   // canvas-clip start point
        private Vector2     _wireDragCurrent; // current mouse in canvas-clip space
        private SkillNodeSO _wireDragTarget;  // node under mouse during drag (highlight)

        // ── Double-click tracking ─────────────────────────────────────────────────
        private SkillNodeSO _lastClickedNode;
        private double      _lastClickTime;
        private const double DOUBLE_CLICK_SECS = 0.3;

        // ── In-place rename ───────────────────────────────────────────────────────
        private SkillNodeSO _renamingNode;   // node currently being renamed
        private string      _renameText;     // current text in the rename field
        private const string RENAME_CTRL = "NodeRenameField";

        // ── Multi-select ──────────────────────────────────────────────────────────
        private readonly HashSet<SkillNodeSO> _selection = new HashSet<SkillNodeSO>();
        // Marquee (rubber-band) drag
        private bool    _marqueeActive;
        private Vector2 _marqueeStart;   // canvas-clip space
        private Vector2 _marqueeEnd;

        // ── Arrow-triggered quick-create: pending parent to wire after creation ──
        private SkillNodeSO _pendingParent;   // set when arrow opens the quick-create popup

        // ── Clipboard (copy/paste) ────────────────────────────────────────────────
        private readonly List<SkillNodeSO> _clipboard     = new List<SkillNodeSO>();
        private Vector2                    _clipboardCentroid; // world-space centre of copied group
        private const float                PASTE_OFFSET = 40f; // world units to offset each successive paste

        // ── Search / filter ───────────────────────────────────────────────────────
        private string _searchQuery  = "";
        private bool   _searchActive => !string.IsNullOrWhiteSpace(_searchQuery);

        // ── Minimap ───────────────────────────────────────────────────────────────
        private const float MM_W        = 180f;
        private const float MM_H        = 120f;
        private const float MM_MARGIN   = 10f;
        private bool        _minimapDragging;
        private bool        _minimapVisible = true;

        // ── Simulate mode ─────────────────────────────────────────────────────────
        private bool                  _simMode;
        private SkillTreeRuntimeState _simState    = new SkillTreeRuntimeState();
        private Dictionary<string,int> _simBalances = new Dictionary<string,int>(); // resourceId → balance
        private Vector2               _simScroll;

        // ── Templates ─────────────────────────────────────────────────────────────
        private List<NodeTemplateSO> _templates        = new List<NodeTemplateSO>();
        private bool                 _templatesDirty   = true;  // reload from AssetDatabase
        private string               _templateFolder   = "Assets/SkillTree_System/Data/Templates";
        private string               _saveTemplateName = "";
        private Vector2              _templateScroll;
        private NodeTemplateSO       _stampTemplate;   // applied on next QuickCreateNode call

        // ── Recent trees ──────────────────────────────────────────────────────────
        private const string RECENT_PREFS_KEY = "SkillTreeEditor_RecentTrees";
        private const int    RECENT_MAX       = 8;
        private List<string> _recentGuids;   // asset GUIDs, lazy-loaded from EditorPrefs

        // ── Cycle detection ───────────────────────────────────────────────────────
        private HashSet<SkillNodeSO> _cyclicNodes  = new HashSet<SkillNodeSO>();
        private bool                 _cyclesDirty  = true;

        // ── Orphan detection ──────────────────────────────────────────────────────
        private HashSet<SkillNodeSO> _orphanNodes  = new HashSet<SkillNodeSO>();
        private bool                 _orphansDirty = true;
        private bool                 _showOrphans  = true;

        // ── Duplicate ID detection ────────────────────────────────────────────────
        private HashSet<SkillNodeSO> _duplicateIdNodes = new HashSet<SkillNodeSO>();
        private bool                 _duplicatesDirty  = true;

        // ── Space+drag panning ────────────────────────────────────────────────────
        private bool _spaceHeld;
        private bool _snapToGrid = true;  // toggled via toolbar button

        // ── Comment bubble hover ──────────────────────────────────────────────────
        private SkillNodeSO _commentHovered; // node whose bubble is being hovered    // true while Space is held  // toggle in toolbar

        // ── Regions ───────────────────────────────────────────────────────────────
        private NodeRegion _selectedRegion;
        private bool       _regionResizing;   // dragging the resize handle of a region
        private bool       _regionMoving;     // dragging the body of a region
        private Vector2    _regionDragOffset; // mouse offset from region origin at drag start
        private Vector2    _regionResizeStart;// region size at resize start
        private Vector2    _regionMouseStart; // mouse pos at resize start

        // ── Quick-create popup ────────────────────────────────────────────────────
        private bool       _quickCreateOpen;
        private Vector2    _quickCreateCanvasPos;
        private string     _quickCreateName   = "";
        private string     _quickCreateId     = "";
        private bool       _quickCreateIdEdited;
        private SkillBranchSO _quickCreateBranch;
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

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            _templatesDirty = true;
        }

        private void OnFocus() => _templatesDirty = true;

        private void OnLostFocus() => _spaceHeld = false;

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo()
        {
            _selectedEdgeChild = null; _selectedEdgeIndex = -1;
            _selection.Clear();
            InvalidateCycleCache(); InvalidateOrphanCache(); InvalidateDuplicateCache();
            Repaint();
        }

        // =========================================================================
        //  OnGUI
        // =========================================================================
        private void OnGUI()
        {
            // ── Ctrl+Z / Ctrl+Y / Ctrl+C / Ctrl+V ────────────────────────────────
            Event ev = Event.current;
            if (ev.type == EventType.KeyDown && ev.modifiers == EventModifiers.Control)
            {
                if (ev.keyCode == KeyCode.Z) { Undo.PerformUndo();  ev.Use(); Repaint(); }
                if (ev.keyCode == KeyCode.Y) { Undo.PerformRedo();  ev.Use(); Repaint(); }
                if (ev.keyCode == KeyCode.C && _tree != null) { CopySelection();  ev.Use(); }
                if (ev.keyCode == KeyCode.V && _clipboard.Count > 0) { PasteClipboard(); ev.Use(); }
            }
            // Escape clears search if active, otherwise handled by canvas
            if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape && _searchActive)
            { _searchQuery = ""; GUI.FocusControl(null); ev.Use(); Repaint(); }

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
            if (_tree != null)                         DrawMinimap(canvasRect);
            if (_simMode && _tree != null)             DrawSimPanel(canvasRect);
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
            float qcH = _pendingParent != null ? QC_H + 20f : QC_H;
            float px = Mathf.Clamp(screenPos.x, canvasRect.x, canvasRect.xMax - QC_W);
            float py = Mathf.Clamp(screenPos.y, canvasRect.y, canvasRect.yMax - qcH);
            Rect popupRect = new Rect(px, py, QC_W, qcH);

            // Border accent: cyan if triggered by arrow, grey otherwise
            Color borderCol = _pendingParent != null ? new Color(0.3f, 0.7f, 0.9f, 1f) : new Color(0.6f, 0.6f, 0.6f, 1f);
            EditorGUI.DrawRect(new Rect(popupRect.x - 1, popupRect.y - 1, popupRect.width + 2, popupRect.height + 2), borderCol);
            EditorGUI.DrawRect(popupRect, new Color(0.18f, 0.18f, 0.18f, 1f));

            GUILayout.BeginArea(popupRect);
            GUILayout.Space(6);
            EditorGUILayout.LabelField("  Create Node", EditorStyles.boldLabel);

            // Show parent hint when triggered by arrow
            if (_pendingParent != null)
            {
                string parentName = _pendingParent.displayName ?? _pendingParent.nodeId ?? "?";
                EditorGUILayout.LabelField($"  ↳ child of: {parentName}",
                    new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.4f, 0.85f, 1f) } });
            }

            // Show template hint when stamping
            if (_stampTemplate != null)
            {
                EditorGUILayout.LabelField($"  📋 template: {_stampTemplate.templateName}",
                    new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.6f, 1f, 0.6f) } });
            }

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

            _quickCreateBranch = BranchPopup("Branch", _quickCreateBranch, allowNone: false);

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

            Undo.RegisterCreatedObjectUndo(node, "Create Node");

            node.nodeId        = id;
            node.displayName   = _quickCreateName.Trim();
            node.branch        = _quickCreateBranch;
            node.graphPosition = SnapToGrid(_quickCreateCanvasPos);

            // ── Apply stamp template if one was set ───────────────────────────────
            if (_stampTemplate != null)
            {
                node.branch    = _stampTemplate.branch;
                node.icon      = _stampTemplate.icon;
                node.maxRanks  = _stampTemplate.maxRanks;
                node.costsPerRank = _stampTemplate.costsPerRank?
                    .Select(c => new ResourceCost { resource = c.resource, amountPerRank = c.amountPerRank })
                    .ToList() ?? new List<ResourceCost>();
                node.effects = _stampTemplate.effects?
                    .Where(e => e != null)
                    .Select(e => e switch
                    {
                        StatMultiplierEffect sme => (SkillEffect)new StatMultiplierEffect
                            { statId = sme.statId, displayName = sme.displayName, multiplier = sme.multiplier },
                        StatFlatBonusEffect sfb  => new StatFlatBonusEffect
                            { statId = sfb.statId, displayName = sfb.displayName, bonus = sfb.bonus },
                        _ => null
                    })
                    .Where(e => e != null).ToList() ?? new List<SkillEffect>();
                node.useGlobalVisibilityRules = _stampTemplate.useGlobalVisibilityRules;
                node.revealBoxRank            = _stampTemplate.revealBoxRank;
                node.revealInfoRank           = _stampTemplate.revealInfoRank;
                node.unlockRank               = _stampTemplate.unlockRank;
                _stampTemplate = null;
            }
            else if (_defaultCostResource != null)
            {
                node.costsPerRank.Add(new ResourceCost
                    { resource = _defaultCostResource, amountPerRank = _defaultCostAmount });
            }

            // Wire to parent if triggered by an arrow
            if (_pendingParent != null)
            {
                node.prerequisites ??= new List<PrerequisiteEntry>();
                node.prerequisites.Add(new PrerequisiteEntry { node = _pendingParent, requiredRank = 1 });
                Undo.RecordObject(_pendingParent, "Create Node");
                _pendingParent = null;
            }

            Undo.RecordObject(_tree, "Create Node");
            _tree.allNodes ??= new List<SkillNodeSO>();
            _tree.allNodes.Add(node);
            Dirty(_tree);
            AssetDatabase.SaveAssets();
            InvalidateCycleCache(); InvalidateOrphanCache(); InvalidateDuplicateCache();

            _selected            = node;
            _leftTab             = 1;
            _inspectorCanvasPos  = node.graphPosition;
            _inspectorOpen       = true;
            Repaint();
        }

        private void CloseQuickCreate()
        {
            _quickCreateOpen     = false;
            _quickCreateName     = "";
            _quickCreateId       = "";
            _quickCreateIdEdited = false;
            _pendingParent       = null;
            _stampTemplate       = null;
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

            // Record the node before any field change so Ctrl+Z restores field values
            if (Event.current.type == EventType.MouseDown || Event.current.type == EventType.KeyDown)
                Undo.RecordObject(_selected, "Edit Node");

            EditorGUI.BeginChangeCheck();

            // ── Identity ──────────────────────────────────────────────────────────
            SmallSection("IDENTITY");

            // ── Cycle warning ─────────────────────────────────────────────────────
            if (GetCyclicNodes().Contains(_selected))
                EditorGUILayout.HelpBox("⚠ This node is part of a circular dependency! Remove one of its prerequisites to fix it.", MessageType.Error);

            _selected.nodeId      = Field("Node ID",      _selected.nodeId);
            _selected.displayName = Field("Display Name", _selected.displayName);
            _selected.branch      = BranchPopup("Branch", _selected.branch);
            _selected.icon        = (Sprite)EditorGUILayout.ObjectField("Icon", _selected.icon, typeof(Sprite), false);
            EditorGUILayout.LabelField("Description", EditorStyles.miniLabel);
            _selected.description = EditorGUILayout.TextArea(_selected.description, GUILayout.MinHeight(36));

            // ── Editor comment (designer note, canvas bubble) ─────────────────────
            GUILayout.Space(2);
            EditorGUILayout.LabelField("📝 Designer Note", EditorStyles.miniLabel);
            string newComment = EditorGUILayout.TextArea(_selected.editorComment ?? "", GUILayout.MinHeight(28));
            if (newComment != (_selected.editorComment ?? ""))
            { _selected.editorComment = newComment; Dirty(_selected); }

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
                        if (nr != entry.requiredRank) SetPrerequisiteRank(_selected, entry, nr);
                    }
                    else GUILayout.Label("(missing)", EditorStyles.miniLabel, GUILayout.ExpandWidth(true));

                    GUI.color = new Color(1f, 0.35f, 0.35f);
                    if (GUILayout.Button("✕", GUILayout.Width(18)))
                        RemovePrerequisiteAt(_selected, i);
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
                            AddPrerequisite(_selected, candidates[qi]);
                        if (qi + 1 < candidates.Length)
                            if (GUILayout.Button(candidates[qi + 1].displayName ?? candidates[qi + 1].nodeId,
                                    EditorStyles.miniButton, GUILayout.ExpandWidth(true)))
                                AddPrerequisite(_selected, candidates[qi + 1]);
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

            // ── Cost calculator ───────────────────────────────────────────────────
            DrawCostCalculator(_selected);

            GUILayout.Space(4); HLine();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Ping Asset",   EditorStyles.miniButton)) EditorGUIUtility.PingObject(_selected);
            if (GUILayout.Button("Select Asset", EditorStyles.miniButton)) Selection.activeObject = _selected;
            GUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck()) { Dirty(_selected); AssetDatabase.SaveAssets(); InvalidateDuplicateCache(); Repaint(); }

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
            _tree = (SkillTreeSO)EditorGUILayout.ObjectField(_tree, typeof(SkillTreeSO), false, GUILayout.Width(180));
            if (EditorGUI.EndChangeCheck())
            {
                _selected = null; _inspectorOpen = false;
                InvalidateCycleCache(); InvalidateOrphanCache(); InvalidateDuplicateCache();
                if (_tree != null)
                {
                    PushRecentTree(_tree);
                    CenterView();
                }
                Repaint();
            }

            // Recent trees dropdown ▾
            if (GUILayout.Button("▾", EditorStyles.toolbarButton, GUILayout.Width(18)))
                ShowRecentMenu();

            GUILayout.Space(6);
            GUI.enabled = _tree != null;
            if (GUILayout.Button("Auto Layout", EditorStyles.toolbarButton, GUILayout.Width(85))) AutoLayout();
            if (GUILayout.Button("Center",      EditorStyles.toolbarButton, GUILayout.Width(55))) CenterView();
            GUI.enabled = _tree != null && _selection.Count >= 2;
            if (GUILayout.Button("Align ▾", EditorStyles.toolbarButton, GUILayout.Width(55))) ShowAlignMenu();
            GUI.enabled = _tree != null;
            if (GUILayout.Button("1×",          EditorStyles.toolbarButton, GUILayout.Width(28))) { _zoom = 1f; Repaint(); }
            _minimapVisible = GUILayout.Toggle(_minimapVisible, "Map",  EditorStyles.toolbarButton, GUILayout.Width(36));
            // Snap toggle — highlighted when ON
            GUI.backgroundColor = _snapToGrid ? new Color(0.6f, 0.85f, 1f) : Color.white;
            _snapToGrid = GUILayout.Toggle(_snapToGrid, "Snap", EditorStyles.toolbarButton, GUILayout.Width(38));
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("⬇ Export",    EditorStyles.toolbarButton, GUILayout.Width(68))) ShowExportMenu();

            // Simulate mode toggle — green tint when active
            GUI.backgroundColor = _simMode ? new Color(0.3f, 0.9f, 0.4f) : Color.white;
            bool newSim = GUILayout.Toggle(_simMode, "▶ Simulate", EditorStyles.toolbarButton, GUILayout.Width(80));
            GUI.backgroundColor = Color.white;
            if (newSim != _simMode)
            {
                _simMode = newSim;
                if (_simMode) InitSimBalances();
                else          _simState = new SkillTreeRuntimeState();
                Repaint();
            }
            GUI.enabled = true;

            GUILayout.Space(10);
            // ── Search bar ────────────────────────────────────────────────────────
            EditorGUILayout.LabelField("🔍", EditorStyles.miniLabel, GUILayout.Width(18));
            GUI.SetNextControlName("ToolbarSearch");
            string newQuery = EditorGUILayout.TextField(_searchQuery, EditorStyles.toolbarSearchField, GUILayout.Width(160));
            if (newQuery != _searchQuery) { _searchQuery = newQuery; Repaint(); }
            if (_searchActive)
            {
                int matchCount = _tree?.allNodes?.Count(n => n != null && NodeMatchesSearch(n)) ?? 0;
                GUILayout.Label($"{matchCount} found", EditorStyles.miniLabel, GUILayout.Width(55));
                if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(20)))
                { _searchQuery = ""; GUI.FocusControl(null); Repaint(); }
            }

            GUILayout.Space(10);
            // Default cost resource picker in toolbar for quick setup
            EditorGUILayout.LabelField("Default Cost:", EditorStyles.miniLabel, GUILayout.Width(76));
            _defaultCostResource = (ResourceDefinitionSO)EditorGUILayout.ObjectField(
                _defaultCostResource, typeof(ResourceDefinitionSO), false, GUILayout.Width(110));
            _defaultCostAmount = Mathf.Max(0,
                EditorGUILayout.IntField(_defaultCostAmount, GUILayout.Width(45)));

            GUILayout.FlexibleSpace();

            // ── Cycle warning ─────────────────────────────────────────────────────
            if (_tree != null && GetCyclicNodes().Count > 0)
            {
                var warnStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(1f, 0.3f, 0.3f) },
                    fontStyle = FontStyle.Bold,
                };
                GUILayout.Label($"⚠ {GetCyclicNodes().Count} cyclic node(s)!", warnStyle);
                GUILayout.Space(6);
            }

            // ── Duplicate ID warning ──────────────────────────────────────────────
            if (_tree != null)
            {
                var dups = GetDuplicateIdNodes();
                if (dups.Count > 0)
                {
                    var dupStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal    = { textColor = new Color(1f, 0.3f, 0.9f) },
                        fontStyle = FontStyle.Bold,
                    };
                    GUILayout.Label($"⚠ {dups.Count} duplicate ID(s)!", dupStyle);
                    GUILayout.Space(6);
                }
            }

            // ── Orphan warning ────────────────────────────────────────────────────
            if (_tree != null)
            {
                int orphanCount = GetOrphanNodes().Count;
                if (orphanCount > 0)
                {
                    var orphanStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal    = { textColor = new Color(1f, 0.65f, 0.1f) },
                        fontStyle = FontStyle.Bold,
                    };
                    GUILayout.Label($"◌ {orphanCount} orphan(s)", orphanStyle);
                    _showOrphans = GUILayout.Toggle(_showOrphans, "show",
                        EditorStyles.toolbarButton, GUILayout.Width(38));
                    GUILayout.Space(4);
                }
            }

            string clipInfo = _clipboard.Count > 0 ? $"  📋 {_clipboard.Count}" : "";
            GUILayout.Label($"Nodes: {_tree?.allNodes?.Count ?? 0}   zoom {_zoom:F2}×{clipInfo}", EditorStyles.miniLabel);
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
                if (t != null) { t.treeName = _newTreeName; Dirty(t); _tree = t; PushRecentTree(t); }
            }

            if (_tree == null) return;

            GUILayout.Space(8); HLine();
            Section("TREE SETTINGS");
            EditorGUI.BeginChangeCheck();
            _tree.treeName        = Field("Tree Name",   _tree.treeName);
            _tree.treeDescription = AreaField("Description", _tree.treeDescription);
            if (EditorGUI.EndChangeCheck()) Dirty(_tree);

            DrawBranchesSection();

            GUILayout.Space(8); HLine();
            Section($"NODES IN TREE  ({_tree.allNodes?.Count ?? 0})");
            _nodeListScroll = GUILayout.BeginScrollView(_nodeListScroll, GUILayout.MaxHeight(200));
            if (_tree.allNodes != null)
            {
                for (int i = 0; i < _tree.allNodes.Count; i++)
                {
                    var n = _tree.allNodes[i];
                    if (n == null) continue;
                    bool isSel    = n == _selected;
                    bool matches  = NodeMatchesSearch(n);
                    bool dimmed   = _searchActive && !matches;

                    // Highlight matching rows with a subtle background
                    if (_searchActive && matches)
                        EditorGUI.DrawRect(GUILayoutUtility.GetLastRect(), new Color(1f, 0.9f, 0.15f, 0.08f));

                    GUI.color = dimmed ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
                    GUILayout.BeginHorizontal();
                    var dot = new GUIStyle(EditorStyles.label) { normal = { textColor = BranchColor(n.branch) } };
                    GUILayout.Label("●", dot, GUILayout.Width(16));
                    var style = isSel
                        ? new GUIStyle(EditorStyles.boldLabel)
                        : (_searchActive && matches
                            ? new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(1f, 0.95f, 0.4f) } }
                            : EditorStyles.label);
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
                    { _tree.allNodes.Add(node); Dirty(_tree); InvalidateOrphanCache(); Repaint(); }
                }
            }

            // ── Validation ────────────────────────────────────────────────────────
            GUILayout.Space(8); HLine();
            Section("VALIDATION");

            var scan = ScanMissingRefs();
            int totalIssues = scan.nullPrereqs + scan.missingNodes;

            if (totalIssues == 0)
            {
                EditorGUILayout.LabelField("✓ No missing references found.", 
                    new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.4f, 0.9f, 0.4f) } });
            }
            else
            {
                // Show breakdown
                if (scan.nullPrereqs > 0)
                    EditorGUILayout.LabelField($"  • {scan.nullPrereqs} null prerequisite slot(s)",
                        new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.5f, 0.2f) } });
                if (scan.missingNodes > 0)
                    EditorGUILayout.LabelField($"  • {scan.missingNodes} destroyed/missing node reference(s)",
                        new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.4f, 0.4f) } });
                if (scan.nullInTreeList > 0)
                    EditorGUILayout.LabelField($"  • {scan.nullInTreeList} null slot(s) in allNodes list",
                        new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.5f, 0.2f) } });

                GUILayout.Space(4);
                GUI.color = new Color(1f, 0.5f, 0.3f);
                if (GUILayout.Button($"Fix All ({totalIssues + scan.nullInTreeList} issues)", GUILayout.Height(26)))
                    CleanMissingRefs();
                GUI.color = Color.white;
            }

            // Affected node list (scrollable)
            if (scan.affectedNodes.Count > 0)
            {
                EditorGUILayout.LabelField("Affected nodes:", EditorStyles.miniLabel);
                foreach (var n in scan.affectedNodes)
                {
                    GUILayout.BeginHorizontal();
                    var dot = new GUIStyle(EditorStyles.label)
                        { normal = { textColor = BranchColor(n.branch) } };
                    GUILayout.Label("●", dot, GUILayout.Width(14));
                    if (GUILayout.Button(n.displayName ?? n.nodeId ?? n.name,
                        EditorStyles.miniButton, GUILayout.ExpandWidth(true)))
                    {
                        _selected = n; _leftTab = 1; EditorGUIUtility.PingObject(n); Repaint();
                    }
                    GUILayout.EndHorizontal();
                }
            }

            // ── Regions ───────────────────────────────────────────────────────────
            GUILayout.Space(8); HLine();
            Section($"REGIONS  ({_tree.regions?.Count ?? 0})");
            _tree.regions ??= new List<NodeRegion>();

            for (int ri = 0; ri < _tree.regions.Count; ri++)
            {
                var reg = _tree.regions[ri];
                if (reg == null) continue;
                bool isSel = reg == _selectedRegion;

                GUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.BeginHorizontal();

                // Colour swatch
                EditorGUI.BeginChangeCheck();
                Color newCol = EditorGUILayout.ColorField(GUIContent.none, reg.color, false, true, false,
                    GUILayout.Width(36), GUILayout.Height(18));
                if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(_tree, "Edit Region"); reg.color = newCol; Dirty(_tree); }

                // Label field
                EditorGUI.BeginChangeCheck();
                string newLabel = EditorGUILayout.TextField(reg.label, GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(_tree, "Rename Region"); reg.label = newLabel; Dirty(_tree); }

                // Select button
                GUI.color = isSel ? new Color(0.4f, 0.8f, 1f) : Color.white;
                if (GUILayout.Button(isSel ? "●" : "○", EditorStyles.miniButton, GUILayout.Width(22)))
                { _selectedRegion = isSel ? null : reg; Repaint(); }
                GUI.color = Color.white;

                // Delete button
                GUI.color = new Color(1f, 0.35f, 0.35f);
                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22)))
                {
                    Undo.RecordObject(_tree, "Delete Region");
                    _tree.regions.RemoveAt(ri);
                    if (_selectedRegion == reg) _selectedRegion = null;
                    Dirty(_tree); InvalidateOrphanCache(); Repaint();
                    GUILayout.EndHorizontal(); GUILayout.EndVertical(); break;
                }
                GUI.color = Color.white;
                GUILayout.EndHorizontal();

                // Size fields (shown when selected)
                if (isSel)
                {
                    EditorGUI.BeginChangeCheck();
                    var newPos  = EditorGUILayout.Vector2Field("Position", reg.position);
                    var newSize = EditorGUILayout.Vector2Field("Size",     reg.size);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_tree, "Edit Region");
                        reg.position = newPos; reg.size = newSize; Dirty(_tree);
                    }
                }
                GUILayout.EndVertical();
            }

            GUILayout.Space(4);
            if (GUILayout.Button("+ Add Region"))
            {
                Undo.RecordObject(_tree, "Add Region");
                // Place new region at canvas centre
                var canvasSz = new Vector2(position.width - _leftW - DIV_W, position.height - TOOLBAR_H);
                Vector2 centre = C2W(canvasSz * 0.5f);
                var reg = new NodeRegion
                {
                    label    = "New Region",
                    position = SnapToGrid(centre - new Vector2(150f, 100f)),
                    size     = new Vector2(300f, 200f),
                    color    = new Color(
                        UnityEngine.Random.Range(0.2f, 0.7f),
                        UnityEngine.Random.Range(0.2f, 0.7f),
                        UnityEngine.Random.Range(0.2f, 0.7f), 0.18f),
                };
                _tree.regions.Add(reg);
                _selectedRegion = reg;
                Dirty(_tree); InvalidateOrphanCache(); Repaint();
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
            _selected.branch      = BranchPopup("Branch", _selected.branch);
            _selected.icon        = (Sprite)EditorGUILayout.ObjectField("Icon", _selected.icon, typeof(Sprite), false);
            GUILayout.Space(4);
            EditorGUILayout.LabelField("Description", EditorStyles.miniLabel);
            _selected.description = EditorGUILayout.TextArea(_selected.description, GUILayout.MinHeight(48));

            GUILayout.Space(2);
            EditorGUILayout.LabelField("📝 Designer Note (canvas bubble)", EditorStyles.miniLabel);
            string noteTab = EditorGUILayout.TextArea(_selected.editorComment ?? "", GUILayout.MinHeight(32));
            if (noteTab != (_selected.editorComment ?? ""))
            { _selected.editorComment = noteTab; Dirty(_selected); }

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
                        if (nr != entry.requiredRank) SetPrerequisiteRank(_selected, entry, nr);
                    }
                    else GUILayout.Label("(missing reference)", EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                    GUI.color = new Color(1f, 0.35f, 0.35f);
                    if (GUILayout.Button("✕", GUILayout.Width(20)))
                        RemovePrerequisiteAt(_selected, i);
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
                AddPrerequisite(_selected, _prereqCandidate, _prereqCandidateRank);
                _prereqCandidate = null; _prereqCandidateRank = 1;
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

            // ── Cost calculator ───────────────────────────────────────────────────
            DrawCostCalculator(_selected);

            GUILayout.Space(6); HLine();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Ping Asset"))   EditorGUIUtility.PingObject(_selected);
            if (GUILayout.Button("Select Asset")) Selection.activeObject = _selected;
            GUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck()) { Dirty(_selected); AssetDatabase.SaveAssets(); }
        }

        /// <summary>
        /// Free-text field for an open stat id, since the skill tree package no
        /// longer assumes a closed enum of valid stats (see IStatRegistry). The "▾"
        /// button offers ids already used elsewhere in this tree as a typo safety
        /// net — purely a convenience, not a constraint; any string is valid.
        ///
        /// GenericMenu's click callback fires asynchronously (on a later OnGUI pass),
        /// so a picked id can't flow back through this method's return value the way
        /// the plain text field edit can — onPicked lets the caller apply it directly
        /// to the live effect object whenever the click actually happens.
        /// </summary>
        private string DrawStatIdField(string label, string value, Action<string> onPicked)
        {
            GUILayout.BeginHorizontal();
            string newVal = EditorGUILayout.TextField(label, value);
            if (GUILayout.Button("▾", GUILayout.Width(20)))
            {
                var known = CollectKnownStatIds();
                if (known.Count == 0)
                {
                    ShowNotification(new GUIContent("No stat ids used yet in this tree"));
                }
                else
                {
                    var menu = new GenericMenu();
                    foreach (var id in known)
                        menu.AddItem(new GUIContent(id), id == value, () => onPicked(id));
                    menu.ShowAsContext();
                }
            }
            GUILayout.EndHorizontal();
            return newVal;
        }

        /// <summary>Scans every effect on every node in the current tree for stat ids
        /// already in use, so the picker in DrawStatIdField has something to offer.</summary>
        private List<string> CollectKnownStatIds()
        {
            var ids = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_tree?.allNodes == null) return new List<string>();

            foreach (var node in _tree.allNodes)
            {
                if (node?.effects == null) continue;
                foreach (var eff in node.effects)
                {
                    string id = eff switch
                    {
                        StatMultiplierEffect sme => sme.statId,
                        StatFlatBonusEffect  sfb => sfb.statId,
                        _ => null
                    };
                    if (!string.IsNullOrEmpty(id)) ids.Add(id);
                }
            }
            return new List<string>(ids);
        }

        private void DrawEffectFields(SkillEffect eff)
        {
            switch (eff)
            {
                case StatMultiplierEffect sme:
                    sme.statId      = DrawStatIdField("Stat", sme.statId,
                        picked => { sme.statId = picked; Dirty(_selected); Repaint(); });
                    sme.displayName = EditorGUILayout.TextField("Display Name", sme.displayName);
                    sme.multiplier  = EditorGUILayout.FloatField("Multiplier", sme.multiplier);
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
                    sfb.statId      = DrawStatIdField("Stat", sfb.statId,
                        picked => { sfb.statId = picked; Dirty(_selected); Repaint(); });
                    sfb.displayName = EditorGUILayout.TextField("Display Name", sfb.displayName);
                    sfb.bonus       = EditorGUILayout.FloatField("Bonus", sfb.bonus);
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
            _newBranch   = BranchPopup("Branch", _newBranch, allowNone: false);
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
            _bulkBranch = BranchPopup("Branch", _bulkBranch, allowNone: false);
            if (GUILayout.Button("Create All Nodes")) BulkCreate();

            // ── Templates ─────────────────────────────────────────────────────────
            GUILayout.Space(8); HLine();
            Section("TEMPLATES");

            // Save current selected node as template
            if (_selected != null)
            {
                EditorGUILayout.LabelField($"Save '{_selected.displayName}' as template:", EditorStyles.miniLabel);
                GUILayout.BeginHorizontal();
                _saveTemplateName = EditorGUILayout.TextField(_saveTemplateName, GUILayout.ExpandWidth(true));
                bool canSave = !string.IsNullOrWhiteSpace(_saveTemplateName);
                GUI.enabled = canSave;
                if (GUILayout.Button("Save", EditorStyles.miniButton, GUILayout.Width(44)))
                {
                    SaveAsTemplate(_selected, _saveTemplateName);
                    _saveTemplateName = "";
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                GUILayout.Space(6);
            }
            else
            {
                EditorGUILayout.LabelField("Select a node to save it as a template.", EditorStyles.miniLabel);
                GUILayout.Space(4);
            }

            // Template list
            var templates = GetTemplates();
            if (templates.Count == 0)
            {
                EditorGUILayout.LabelField("No templates saved yet.", EditorStyles.miniLabel);
            }
            else
            {
                _templateScroll = GUILayout.BeginScrollView(_templateScroll, GUILayout.MaxHeight(220));
                foreach (var tpl in templates)
                {
                    if (tpl == null) continue;
                    GUILayout.BeginVertical(EditorStyles.helpBox);

                    // Header row: color dot + name + stamp button
                    GUILayout.BeginHorizontal();
                    var dot = new GUIStyle(EditorStyles.label)
                        { normal = { textColor = BranchColor(tpl.branch) } };
                    GUILayout.Label("●", dot, GUILayout.Width(14));
                    GUILayout.Label(tpl.templateName, EditorStyles.boldLabel, GUILayout.ExpandWidth(true));

                    // Stamp button
                    GUI.color = new Color(0.4f, 0.85f, 0.5f);
                    if (GUILayout.Button("Stamp", EditorStyles.miniButton, GUILayout.Width(46)))
                        StampTemplate(tpl);
                    GUI.color = Color.white;

                    // Ping / delete
                    if (GUILayout.Button("…", EditorStyles.miniButton, GUILayout.Width(22)))
                    {
                        var menu = new GenericMenu();
                        var captured = tpl;
                        menu.AddItem(new GUIContent("Ping Asset"), false,
                            () => EditorGUIUtility.PingObject(captured));
                        menu.AddItem(new GUIContent("Select Asset"), false,
                            () => Selection.activeObject = captured);
                        menu.AddSeparator("");
                        menu.AddItem(new GUIContent("Delete Template"), false, () =>
                        {
                            if (EditorUtility.DisplayDialog("Delete Template",
                                $"Delete template '{captured.templateName}'? This cannot be undone.", "Delete", "Cancel"))
                            {
                                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(captured));
                                _templatesDirty = true; Repaint();
                            }
                        });
                        menu.ShowAsContext();
                    }
                    GUILayout.EndHorizontal();

                    // Summary row
                    string summary = $"{BranchName(tpl.branch)}  ×{tpl.maxRanks} ranks";
                    if (tpl.effects?.Count > 0) summary += $"  {tpl.effects.Count} fx";
                    if (tpl.costsPerRank?.Count > 0) summary += $"  {tpl.costsPerRank.Count} cost(s)";
                    EditorGUILayout.LabelField(summary, EditorStyles.miniLabel);

                    GUILayout.EndVertical();
                }
                GUILayout.EndScrollView();
            }

            GUILayout.Space(4);
            _templateFolder = Field("Template Folder", _templateFolder);
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
            DrawRegions();
            DrawConnections();
            DrawNodes();
            GUI.EndClip();
            HandleRegionInput(canvasRect);
            HandleCanvasInput(canvasRect);
            HandleZoom(canvasRect);

            // ── Keyboard nav hint (bottom-left, subtle) ───────────────────────────
            if (_selected != null && _tree?.allNodes?.Count > 1)
            {
                string hint = "↑ parent  ↓ child  ←→ siblings  Tab cycle  F focus";
                var hintStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(1f, 1f, 1f, 0.22f) },
                    fontSize = 9,
                };
                float hw = 320f;
                GUI.Label(new Rect(canvasRect.x + 8, canvasRect.yMax - 18, hw, 16), hint, hintStyle);
            }

            if (_dragging != null || _panning || _marqueeActive || _wireDragging || _regionMoving || _regionResizing) Repaint();
        }

        // =========================================================================
        //  Minimap
        // =========================================================================
        private void DrawMinimap(Rect canvasRect)
        {
            if (!_minimapVisible) return;
            if (_tree?.allNodes == null || _tree.allNodes.Count == 0) return;

            // ── Minimap rect (bottom-right of canvas) ─────────────────────────────
            Rect mm = new Rect(
                canvasRect.xMax - MM_W - MM_MARGIN,
                canvasRect.yMax - MM_H - MM_MARGIN,
                MM_W, MM_H);

            // ── World bounds of all nodes ─────────────────────────────────────────
            Vector2 wMin = Vector2.one * float.MaxValue;
            Vector2 wMax = Vector2.one * float.MinValue;
            foreach (var n in _tree.allNodes)
            {
                if (n == null) continue;
                wMin = Vector2.Min(wMin, n.graphPosition);
                wMax = Vector2.Max(wMax, n.graphPosition + new Vector2(NODE_W, NODE_H));
            }
            // Pad the bounds slightly
            float pad = 40f;
            wMin -= Vector2.one * pad;
            wMax += Vector2.one * pad;
            Vector2 wSize = wMax - wMin;
            if (wSize.x < 1f || wSize.y < 1f) return;

            // ── Scale factors: world → minimap ────────────────────────────────────
            float scaleX = MM_W  / wSize.x;
            float scaleY = MM_H / wSize.y;
            float scale  = Mathf.Min(scaleX, scaleY);
            // Centre the content inside the minimap panel
            float contentW = wSize.x * scale;
            float contentH = wSize.y * scale;
            float offX = mm.x + (MM_W  - contentW) * 0.5f;
            float offY = mm.y + (MM_H - contentH) * 0.5f;

            Vector2 WorldToMM(Vector2 w) =>
                new Vector2(offX + (w.x - wMin.x) * scale,
                            offY + (w.y - wMin.y) * scale);

            // ── Background + border ───────────────────────────────────────────────
            EditorGUI.DrawRect(new Rect(mm.x - 1, mm.y - 1, mm.width + 2, mm.height + 2),
                new Color(0.5f, 0.5f, 0.5f, 0.9f));
            EditorGUI.DrawRect(mm, new Color(0.1f, 0.1f, 0.12f, 0.92f));

            // ── Connection lines ──────────────────────────────────────────────────
            Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            foreach (var node in _tree.allNodes)
            {
                if (node?.prerequisites == null) continue;
                Vector2 toMM = WorldToMM(node.graphPosition + new Vector2(NODE_W, NODE_H) * 0.5f);
                foreach (var p in node.prerequisites)
                {
                    if (p?.node == null) continue;
                    Vector2 fromMM = WorldToMM(p.node.graphPosition + new Vector2(NODE_W, NODE_H) * 0.5f);
                    Handles.DrawLine(fromMM, toMM);
                }
            }
            Handles.color = Color.white;

            // ── Node dots ─────────────────────────────────────────────────────────
            float dotW = Mathf.Max(3f, NODE_W  * scale);
            float dotH = Mathf.Max(3f, NODE_H * scale);
            foreach (var n in _tree.allNodes)
            {
                if (n == null) continue;
                Vector2 mmPos = WorldToMM(n.graphPosition);
                bool isSel    = n == _selected || _selection.Contains(n);
                bool isCyclic = _cyclicNodes.Contains(n);
                bool isOrphan = _showOrphans && _orphanNodes.Contains(n);
                Color dotCol  = isCyclic ? new Color(1f,  0.2f, 0.2f, 1f)
                              : isOrphan ? new Color(1f,  0.6f, 0.1f, 1f)
                              : isSel    ? new Color(0.9f, 0.95f, 1f,  1f)
                              : BranchColor(n.branch);
                EditorGUI.DrawRect(new Rect(mmPos.x, mmPos.y, dotW, dotH), dotCol);
            }

            // ── Viewport indicator ────────────────────────────────────────────────
            // The visible canvas in world space
            Vector2 vpWorldMin = C2W(Vector2.zero);
            Vector2 vpWorldMax = C2W(new Vector2(canvasRect.width, canvasRect.height));
            Vector2 vpMmMin    = WorldToMM(vpWorldMin);
            Vector2 vpMmMax    = WorldToMM(vpWorldMax);
            Rect vpRect = new Rect(vpMmMin.x, vpMmMin.y,
                vpMmMax.x - vpMmMin.x, vpMmMax.y - vpMmMin.y);

            // Clamp to minimap bounds for display
            EditorGUI.DrawRect(new Rect(vpRect.x,          vpRect.y,          vpRect.width, 1f),
                new Color(1f, 1f, 1f, 0.7f));
            EditorGUI.DrawRect(new Rect(vpRect.x,          vpRect.yMax - 1f,  vpRect.width, 1f),
                new Color(1f, 1f, 1f, 0.7f));
            EditorGUI.DrawRect(new Rect(vpRect.x,          vpRect.y,          1f, vpRect.height),
                new Color(1f, 1f, 1f, 0.7f));
            EditorGUI.DrawRect(new Rect(vpRect.xMax - 1f,  vpRect.y,          1f, vpRect.height),
                new Color(1f, 1f, 1f, 0.7f));

            // ── "Map" label ───────────────────────────────────────────────────────
            GUI.Label(new Rect(mm.x + 4, mm.y + 2, 40, 14), "MAP",
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal   = { textColor = new Color(1f, 1f, 1f, 0.45f) },
                    fontSize = 8,
                    fontStyle = FontStyle.Bold,
                });

            // ── Click / drag to pan ───────────────────────────────────────────────
            Event e = Event.current;
            if (mm.Contains(e.mousePosition))
            {
                EditorGUIUtility.AddCursorRect(mm, MouseCursor.Pan);

                if (e.type == EventType.MouseDown && e.button == 0)
                { _minimapDragging = true; e.Use(); }
            }

            if (_minimapDragging)
            {
                if (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
                {
                    // Convert click position to world and centre the viewport there
                    float nx = (e.mousePosition.x - offX) / scale + wMin.x;
                    float ny = (e.mousePosition.y - offY) / scale + wMin.y;
                    Vector2 worldClick = new Vector2(nx, ny);
                    Vector2 canvasSz   = new Vector2(canvasRect.width, canvasRect.height);
                    _offset = canvasSz * 0.5f - worldClick * _zoom;
                    e.Use(); Repaint();
                }
                if (e.type == EventType.MouseUp)
                { _minimapDragging = false; e.Use(); }
            }
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

        // =========================================================================
        //  Cycle detection
        // =========================================================================
        /// <summary>
        /// Runs DFS on the prerequisite graph and returns all nodes that are part
        /// of at least one cycle. Result is cached; call InvalidateCycleCache()
        /// after any prerequisite change.
        /// </summary>
        private HashSet<SkillNodeSO> GetCyclicNodes()
        {
            if (!_cyclesDirty) return _cyclicNodes;
            _cyclesDirty = false;
            _cyclicNodes.Clear();

            if (_tree?.allNodes == null) return _cyclicNodes;

            // Standard DFS cycle detection with three-colour marking.
            // `path` mirrors the current chain of GRAY ancestors so that when a back-edge
            // is found we can mark every node in the cycle, not just the edge's target.
            var WHITE = 0; var GRAY = 1; var BLACK = 2;
            var color = new Dictionary<SkillNodeSO, int>();
            foreach (var n in _tree.allNodes)
                if (n != null) color[n] = WHITE;

            var stack = new Stack<(SkillNodeSO node, bool leaving)>();
            var path  = new List<SkillNodeSO>();

            void Visit(SkillNodeSO start)
            {
                stack.Push((start, false));
                while (stack.Count > 0)
                {
                    var (node, leaving) = stack.Pop();
                    if (node == null || !color.ContainsKey(node)) continue;

                    if (leaving)
                    {
                        color[node] = BLACK;
                        if (path.Count > 0 && path[path.Count - 1] == node)
                            path.RemoveAt(path.Count - 1);
                        continue;
                    }

                    // Already fully processed, or already open further up this same
                    // path (duplicate queued entry) — nothing new to do here.
                    if (color[node] == BLACK || color[node] == GRAY) continue;

                    color[node] = GRAY;
                    path.Add(node);
                    stack.Push((node, true)); // push leaving marker

                    if (node.prerequisites == null) continue;
                    foreach (var p in node.prerequisites)
                    {
                        if (p?.node == null) continue;
                        if (!color.TryGetValue(p.node, out int c)) continue;

                        if (c == GRAY)
                        {
                            // Back-edge to an ancestor still on the current path —
                            // mark the whole cycle, from that ancestor to here.
                            int idx = path.LastIndexOf(p.node);
                            if (idx >= 0)
                                for (int i = idx; i < path.Count; i++)
                                    _cyclicNodes.Add(path[i]);
                            else
                                _cyclicNodes.Add(p.node); // fallback, shouldn't normally hit
                        }
                        else if (c == WHITE)
                        {
                            stack.Push((p.node, false));
                        }
                    }
                }
            }

            foreach (var n in _tree.allNodes)
                if (n != null && color.TryGetValue(n, out int c) && c == WHITE)
                    Visit(n);

            return _cyclicNodes;
        }

        private void InvalidateCycleCache() => _cyclesDirty = true;

        // =========================================================================
        //  Orphan detection
        // =========================================================================
        /// <summary>
        /// Returns nodes that have NO prerequisites AND are not a prerequisite
        /// of any other node — i.e. completely disconnected from the tree.
        /// Root nodes (no prerequisites but others depend on them) are NOT orphans.
        /// Cached; call InvalidateOrphanCache() after any connection change.
        /// </summary>
        private HashSet<SkillNodeSO> GetOrphanNodes()
        {
            if (!_orphansDirty) return _orphanNodes;
            _orphansDirty = false;
            _orphanNodes.Clear();

            if (_tree?.allNodes == null) return _orphanNodes;

            // Build set of nodes that ARE referenced as prerequisites by someone
            var referenced = new HashSet<SkillNodeSO>();
            foreach (var n in _tree.allNodes)
            {
                if (n?.prerequisites == null) continue;
                foreach (var p in n.prerequisites)
                    if (p?.node != null) referenced.Add(p.node);
            }

            // Orphan = has no prerequisites AND nobody references it
            foreach (var n in _tree.allNodes)
            {
                if (n == null) continue;
                bool hasPrereqs   = n.prerequisites != null && n.prerequisites.Any(p => p?.node != null);
                bool isReferenced = referenced.Contains(n);
                if (!hasPrereqs && !isReferenced)
                    _orphanNodes.Add(n);
            }

            return _orphanNodes;
        }

        private void InvalidateOrphanCache() => _orphansDirty = true;

        // =========================================================================
        //  Duplicate ID detection
        // =========================================================================
        /// <summary>
        /// Returns all nodes whose nodeId is shared by at least one other node in the tree.
        /// Duplicate IDs cause silent save/load corruption at runtime.
        /// Cached; call InvalidateDuplicateCache() after any nodeId or node list change.
        /// </summary>
        private HashSet<SkillNodeSO> GetDuplicateIdNodes()
        {
            if (!_duplicatesDirty) return _duplicateIdNodes;
            _duplicatesDirty = false;
            _duplicateIdNodes.Clear();

            if (_tree?.allNodes == null) return _duplicateIdNodes;

            // Count occurrences of each nodeId
            var counts = new Dictionary<string, List<SkillNodeSO>>(System.StringComparer.Ordinal);
            foreach (var n in _tree.allNodes)
            {
                if (n == null || string.IsNullOrEmpty(n.nodeId)) continue;
                if (!counts.TryGetValue(n.nodeId, out var list))
                    counts[n.nodeId] = list = new List<SkillNodeSO>();
                list.Add(n);
            }

            // Any id with 2+ nodes → all of them are duplicates
            foreach (var kvp in counts)
                if (kvp.Value.Count > 1)
                    foreach (var n in kvp.Value)
                        _duplicateIdNodes.Add(n);

            return _duplicateIdNodes;
        }

        private void InvalidateDuplicateCache() => _duplicatesDirty = true;

        // =========================================================================
        //  Cost calculator
        // =========================================================================
        /// <summary>
        /// Walks the full prerequisite chain of <paramref name="target"/> (BFS, cycle-safe)
        /// and sums the total resource cost to unlock every node in the chain at max rank.
        /// Returns a dictionary of resourceId → total amount, plus the ordered node chain.
        /// </summary>
        private Dictionary<string, int> ComputeChainCost(
            SkillNodeSO target,
            out List<SkillNodeSO> chain)
        {
            chain = new List<SkillNodeSO>();
            var totals  = new Dictionary<string, int>();
            var visited = new HashSet<SkillNodeSO>();
            var queue   = new Queue<SkillNodeSO>();

            queue.Enqueue(target);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (node == null || !visited.Add(node)) continue;
                chain.Add(node);

                // Accumulate cost at max rank for this node
                if (node.costsPerRank != null)
                    foreach (var cost in node.costsPerRank)
                    {
                        if (cost?.resource == null) continue;
                        string id = cost.resource.resourceId;
                        int    amt = cost.GetAmount(node.maxRanks);
                        totals[id] = (totals.TryGetValue(id, out int cur) ? cur : 0) + amt;
                    }

                // Queue prerequisites
                if (node.prerequisites != null)
                    foreach (var p in node.prerequisites)
                        if (p?.node != null && !visited.Contains(p.node))
                            queue.Enqueue(p.node);
            }

            // Sort chain: prerequisites first, target last
            chain.Reverse();
            return totals;
        }

        /// <summary>
        /// Returns the ResourceDefinitionSO for a given resourceId by scanning the tree's nodes.
        /// Used to get the shortSuffix for display.
        /// </summary>
        private ResourceDefinitionSO FindResourceDef(string resourceId)
        {
            if (_tree?.allNodes == null) return null;
            foreach (var n in _tree.allNodes)
            {
                if (n?.costsPerRank == null) continue;
                foreach (var c in n.costsPerRank)
                    if (c?.resource?.resourceId == resourceId)
                        return c.resource;
            }
            return null;
        }

        /// <summary>Draws the cost calculator UI for a given node inside an inspector panel.</summary>
        private void DrawCostCalculator(SkillNodeSO node)
        {
            GUILayout.Space(4); HLine();
            SmallSection("COST TO UNLOCK (full chain)");

            var totals = ComputeChainCost(node, out var chain);

            if (totals.Count == 0)
            {
                EditorGUILayout.LabelField("Free — no costs in chain.", EditorStyles.miniLabel);
            }
            else
            {
                // Total cost per resource
                foreach (var kvp in totals)
                {
                    var res    = FindResourceDef(kvp.Key);
                    string lbl = res != null ? $"{res.shortSuffix}" : kvp.Key;
                    EditorGUILayout.LabelField($"  {lbl}",
                        kvp.Value.ToString(),
                        new GUIStyle(EditorStyles.miniLabel)
                        {
                            normal = { textColor = new Color(1f, 0.88f, 0.4f) },
                            fontStyle = FontStyle.Bold,
                        });
                }
            }

            // Node chain breakdown (collapsible)
            GUILayout.Space(2);
            EditorGUILayout.LabelField($"Chain: {chain.Count} node(s)", EditorStyles.miniLabel);
            foreach (var n in chain)
            {
                string nodeCost = "free";
                if (n.costsPerRank != null && n.costsPerRank.Count > 0)
                    nodeCost = string.Join(" + ", n.costsPerRank
                        .Where(c => c?.resource != null)
                        .Select(c =>
                        {
                            int amt = c.GetAmount(n.maxRanks);
                            return $"{amt}{c.resource.shortSuffix}";
                        }));

                string label = n == node ? $"→ {n.displayName ?? n.nodeId}" : $"  {n.displayName ?? n.nodeId}";
                Color  col   = n == node
                    ? new Color(1f, 0.95f, 0.6f)
                    : new Color(0.72f, 0.72f, 0.72f);

                EditorGUILayout.LabelField(label, nodeCost,
                    new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = col } });
            }
        }
        private struct ScanResult
        {
            public int nullPrereqs;       // prerequisite entries where .node is null
            public int missingNodes;      // entries where the SO was deleted (Unity fake-null)
            public int nullInTreeList;    // null slots directly in _tree.allNodes
            public List<SkillNodeSO> affectedNodes;
        }

        private ScanResult ScanMissingRefs()
        {
            var result = new ScanResult
            {
                affectedNodes = new List<SkillNodeSO>()
            };

            if (_tree?.allNodes == null) return result;

            // Null slots in the tree list itself
            result.nullInTreeList = _tree.allNodes.Count(n => n == null);

            foreach (var node in _tree.allNodes)
            {
                if (node == null) continue;
                if (node.prerequisites == null) continue;

                bool affected = false;
                foreach (var p in node.prerequisites)
                {
                    if (p == null)                { result.nullPrereqs++;  affected = true; }
                    else if (p.node == null)      { result.nullPrereqs++;  affected = true; }
                    else if (!p.node)             { result.missingNodes++; affected = true; } // Unity fake-null (destroyed SO)
                }
                if (affected && !result.affectedNodes.Contains(node))
                    result.affectedNodes.Add(node);
            }

            return result;
        }

        private void CleanMissingRefs()
        {
            if (_tree?.allNodes == null) return;

            int fixed_ = 0;

            // Remove null entries from the tree list
            int before = _tree.allNodes.Count;
            Undo.RecordObject(_tree, "Clean Missing References");
            _tree.allNodes.RemoveAll(n => n == null);
            fixed_ += before - _tree.allNodes.Count;

            // Remove bad prerequisite entries from each node
            foreach (var node in _tree.allNodes)
            {
                if (node?.prerequisites == null) continue;
                int nodeBefore = node.prerequisites.Count;
                Undo.RecordObject(node, "Clean Missing References");
                node.prerequisites.RemoveAll(p => p == null || p.node == null || !p.node);
                int removed = nodeBefore - node.prerequisites.Count;
                if (removed > 0) { fixed_ += removed; Dirty(node); }
            }

            Dirty(_tree);
            AssetDatabase.SaveAssets();
            InvalidateCycleCache(); InvalidateOrphanCache(); InvalidateDuplicateCache();
            Repaint();

            Debug.Log($"[SkillTreeEditor] Cleaned {fixed_} missing reference(s).");
            EditorUtility.DisplayDialog("Scan Complete",
                $"Removed {fixed_} missing or null reference(s).\nAll affected nodes have been saved.", "OK");
        }

        // =========================================================================
        //  Regions
        // =========================================================================
        private const float REGION_HANDLE    = 14f;  // resize grip size
        private const float REGION_HEADER_H  = 22f;  // label bar height

        private void DrawRegions()
        {
            if (_tree?.regions == null) return;
            foreach (var reg in _tree.regions)
            {
                if (reg == null) continue;

                Rect wr = RegionRect(reg); // canvas-clip space
                bool isSel = reg == _selectedRegion;

                // ── Filled body ─────────────────────────────────────────────────
                Color fill = new Color(reg.color.r, reg.color.g, reg.color.b,
                    isSel ? reg.color.a * 1.6f : reg.color.a);
                EditorGUI.DrawRect(wr, fill);

                // ── Border ───────────────────────────────────────────────────────
                Color border = new Color(reg.color.r, reg.color.g, reg.color.b,
                    isSel ? 0.9f : 0.45f);
                float bw = isSel ? 2f : 1f;
                EditorGUI.DrawRect(new Rect(wr.x,          wr.y,          wr.width, bw),   border);
                EditorGUI.DrawRect(new Rect(wr.x,          wr.yMax - bw,  wr.width, bw),   border);
                EditorGUI.DrawRect(new Rect(wr.x,          wr.y,          bw, wr.height),  border);
                EditorGUI.DrawRect(new Rect(wr.xMax - bw,  wr.y,          bw, wr.height),  border);

                // ── Label bar ────────────────────────────────────────────────────
                float hh   = REGION_HEADER_H * Mathf.Clamp(_zoom, 0.5f, 1.5f);
                Rect  hdr  = new Rect(wr.x, wr.y, wr.width, hh);
                Color hdrC = new Color(reg.color.r * 0.6f, reg.color.g * 0.6f, reg.color.b * 0.6f,
                    isSel ? 0.85f : 0.6f);
                EditorGUI.DrawRect(hdr, hdrC);

                GUI.Label(new Rect(hdr.x + 6, hdr.y + 2, hdr.width - 12, hdr.height - 4),
                    reg.label,
                    new GUIStyle(EditorStyles.boldLabel)
                    {
                        normal   = { textColor = Color.white },
                        fontSize = Mathf.Max(9, Mathf.RoundToInt(11 * _zoom)),
                    });

                // ── Resize grip (bottom-right) ───────────────────────────────────
                if (isSel)
                {
                    float gs = REGION_HANDLE * Mathf.Clamp(_zoom, 0.6f, 1.5f);
                    Rect grip = new Rect(wr.xMax - gs, wr.yMax - gs, gs, gs);
                    EditorGUI.DrawRect(grip, new Color(1f, 1f, 1f, 0.25f));
                    // Dotted corner lines
                    Handles.color = new Color(1f, 1f, 1f, 0.7f);
                    Handles.DrawLine(new Vector3(grip.x + 3, grip.yMax - 2), new Vector3(grip.xMax - 2, grip.yMax - 2));
                    Handles.DrawLine(new Vector3(grip.xMax - 2, grip.y + 3), new Vector3(grip.xMax - 2, grip.yMax - 2));
                    Handles.color = Color.white;
                    EditorGUIUtility.AddCursorRect(grip, MouseCursor.ResizeUpLeft);
                }
            }
        }

        private void HandleRegionInput(Rect canvasRect)
        {
            if (_tree?.regions == null) return;
            Event  e = Event.current;
            Vector2 m = e.mousePosition - new Vector2(canvasRect.x, canvasRect.y); // canvas-clip

            // ── Resize drag update ────────────────────────────────────────────────
            if (_regionResizing && _selectedRegion != null)
            {
                if (e.type == EventType.MouseDrag)
                {
                    Vector2 delta = e.mousePosition - _regionMouseStart;
                    Vector2 newSz = _regionResizeStart + delta / _zoom;
                    Undo.RecordObject(_tree, "Resize Region");
                    _selectedRegion.size = new Vector2(
                        Mathf.Max(80f, newSz.x),
                        Mathf.Max(50f, newSz.y));
                    Dirty(_tree); e.Use(); Repaint();
                }
                if (e.type == EventType.MouseUp) { _regionResizing = false; e.Use(); }
                return;
            }

            // ── Move drag update ──────────────────────────────────────────────────
            if (_regionMoving && _selectedRegion != null)
            {
                if (e.type == EventType.MouseDrag)
                {
                    Undo.RecordObject(_tree, "Move Region");
                    _selectedRegion.position = SnapToGrid(C2W(m) - _regionDragOffset);
                    Dirty(_tree); e.Use(); Repaint();
                }
                if (e.type == EventType.MouseUp) { _regionMoving = false; e.Use(); }
                return;
            }

            if (!canvasRect.Contains(e.mousePosition)) return;

            // ── Delete selected region ────────────────────────────────────────────
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete
                && _selectedRegion != null && NodeAt(m) == null && !_quickCreateOpen)
            {
                Undo.RecordObject(_tree, "Delete Region");
                _tree.regions.Remove(_selectedRegion);
                _selectedRegion = null;
                Dirty(_tree); e.Use(); Repaint(); return;
            }

            if (e.type != EventType.MouseDown || e.button != 0) return;
            if (NodeAt(m) != null) return; // nodes take priority

            // ── Hit-test resize grip then header then body ────────────────────────
            for (int ri = _tree.regions.Count - 1; ri >= 0; ri--)
            {
                var reg = _tree.regions[ri];
                if (reg == null) continue;
                Rect wr = RegionRect(reg);

                float gs = REGION_HANDLE * Mathf.Clamp(_zoom, 0.6f, 1.5f);
                Rect  grip = new Rect(wr.xMax - gs, wr.yMax - gs, gs, gs);

                if (reg == _selectedRegion && grip.Contains(m))
                {
                    _regionResizing    = true;
                    _regionMouseStart  = e.mousePosition;
                    _regionResizeStart = reg.size;
                    e.Use(); return;
                }

                if (wr.Contains(m))
                {
                    _selectedRegion    = reg;
                    _regionMoving      = true;
                    _regionDragOffset  = C2W(m) - reg.position;
                    e.Use(); Repaint(); return;
                }
            }

            // Clicked empty canvas → deselect region
            _selectedRegion = null;
            Repaint();
        }

        private Rect RegionRect(NodeRegion reg)
        {
            Vector2 tl = W2C(reg.position);
            return new Rect(tl.x, tl.y, reg.size.x * _zoom, reg.size.y * _zoom);
        }

        // ── Centralized prerequisite mutation ────────────────────────────────────
        // Every UI path that adds/removes/edits a prerequisite routes through one of
        // these three methods instead of touching node.prerequisites directly. That
        // way the cycle/orphan caches can never go stale from a new edit path
        // forgetting to invalidate them — the exact bug this replaced.

        private void AddPrerequisite(SkillNodeSO node, SkillNodeSO prereqNode, int requiredRank = 1,
            string undoLabel = "Add Prerequisite")
        {
            if (node == null || prereqNode == null) return;
            Undo.RecordObject(node, undoLabel);
            node.prerequisites ??= new List<PrerequisiteEntry>();
            node.prerequisites.Add(new PrerequisiteEntry { node = prereqNode, requiredRank = Mathf.Max(1, requiredRank) });
            Dirty(node);
            InvalidateCycleCache(); InvalidateOrphanCache();
        }

        private void RemovePrerequisiteAt(SkillNodeSO node, int index, string undoLabel = "Remove Prerequisite")
        {
            if (node?.prerequisites == null || index < 0 || index >= node.prerequisites.Count) return;
            Undo.RecordObject(node, undoLabel);
            node.prerequisites.RemoveAt(index);
            Dirty(node);
            InvalidateCycleCache(); InvalidateOrphanCache();
        }

        private void SetPrerequisiteRank(SkillNodeSO node, PrerequisiteEntry entry, int newRank,
            string undoLabel = "Edit Prerequisite Rank")
        {
            if (node == null || entry == null) return;
            int clamped = Mathf.Max(1, newRank);
            if (clamped == entry.requiredRank) return;
            Undo.RecordObject(node, undoLabel);
            entry.requiredRank = clamped;
            Dirty(node);
            InvalidateCycleCache(); // rank changes never alter graph shape, only orphan-irrelevant
        }

        /// <summary>
        /// Computes the endpoint of a prerequisite edge on the canvas, including the
        /// sideways nudge applied when the reverse edge also exists (a direct 2-node
        /// cycle) so the two curves don't sit exactly on top of each other.
        /// Shared by DrawConnections (visuals) and EdgeAt (hit-testing) so clicks/hovers
        /// always line up with what's actually drawn.
        /// </summary>
        private static void GetEdgePoints(SkillNodeSO child, PrerequisiteEntry entry,
            Rect toR, Rect fromR, out Vector2 fromPt, out Vector2 toPt, out Vector2 dirN)
        {
            Vector2 fromC = fromR.center;
            Vector2 toC   = toR.center;
            Vector2 delta = toC - fromC;
            dirN = delta.normalized;

            bool reverseExists = entry.node.prerequisites != null &&
                                  entry.node.prerequisites.Any(e => e?.node == child);
            Vector2 perp = new Vector2(-dirN.y, dirN.x);
            float   side = reverseExists
                ? (child.GetInstanceID() < entry.node.GetInstanceID() ? 1f : -1f)
                : 0f;
            Vector2 sideOffset = perp * (side * 7f);

            fromPt = fromC + ClampToEdge(dirN, fromR) + sideOffset;
            toPt   = toC   - ClampToEdge(dirN, toR)   + sideOffset;
        }

        private void DrawConnections()
        {
            if (_tree?.allNodes == null) return;
            for (int ni = 0; ni < _tree.allNodes.Count; ni++)
            {
                var node = _tree.allNodes[ni];
                if (node?.prerequisites == null) continue;
                Rect toR = NodeRect(node);

                for (int pi = 0; pi < node.prerequisites.Count; pi++)
                {
                    var entry = node.prerequisites[pi];
                    if (entry?.node == null) continue;
                    Rect fromR = NodeRect(entry.node);

                    GetEdgePoints(node, entry, toR, fromR, out Vector2 fromPt, out Vector2 toPt, out Vector2 dirN);

                    bool isSelEdge  = _selectedEdgeChild == node && _selectedEdgeIndex == pi;
                    bool isHovEdge  = _hoveredEdgeChild  == node && _hoveredEdgeIndex  == pi;
                    bool nodeHl     = _selected == node || _selected == entry.node;
                    bool isCycEdge  = GetCyclicNodes().Contains(node) && GetCyclicNodes().Contains(entry.node);

                    // Cyclic edges get a distinct hue (magenta, not red) AND a dashed
                    // stroke — a shape difference, not just a color difference, so it
                    // reads clearly even for colorblind users and can't be mistaken
                    // for the red "selected edge" state at a glance.
                    Color lineCol = isCycEdge  ? new Color(0.95f, 0.15f, 0.85f, 1f)
                                  : isSelEdge  ? new Color(1f,    0.35f, 0.35f, 1f)
                                  : isHovEdge  ? new Color(1f,    0.75f, 0.3f,  1f)
                                  : nodeHl     ? new Color(0.85f, 0.92f, 1f,    1f)
                                  :              ColLine;
                    float lineW = isCycEdge ? 3f : (isSelEdge || isHovEdge) ? 3f : (nodeHl ? 2.5f : 1.5f);

                    float dist    = (toPt - fromPt).magnitude;
                    float tan     = Mathf.Clamp(dist * 0.35f, 20f, 80f);
                    Vector2 tangent = dirN * tan;
                    Vector2 ctrl1   = fromPt + tangent;
                    Vector2 ctrl2   = toPt   - tangent;

                    if (isCycEdge)
                        DrawDashedBezier(fromPt, ctrl1, ctrl2, toPt, lineCol, lineW);
                    else
                        Handles.DrawBezier(fromPt, toPt, ctrl1, ctrl2, lineCol, null, lineW);

                    // Direction arrow at the curve's midpoint — points from the
                    // prerequisite (fromPt) toward the node that depends on it (toPt),
                    // so you can read "which node depends on which" at a glance.
                    Vector2 midPt  = 0.125f * fromPt + 0.375f * ctrl1 + 0.375f * ctrl2 + 0.125f * toPt;
                    Vector2 midDir = (ctrl2 + toPt) - (fromPt + ctrl1);
                    midDir = midDir.sqrMagnitude > 0.0001f ? midDir.normalized : dirN;
                    DrawArrowhead(midPt, midDir, lineCol);

                    // Delete hint on selected edge
                    if (isSelEdge)
                    {
                        var mid = (fromPt + toPt) * 0.5f;
                        var lr  = new Rect(mid.x - 30, mid.y - 10, 60, 18);
                        EditorGUI.DrawRect(lr, new Color(0.6f, 0.1f, 0.1f, 0.9f));
                        GUI.Label(lr, "Del to remove",
                            new GUIStyle(EditorStyles.miniLabel)
                            {
                                normal    = { textColor = Color.white },
                                alignment = TextAnchor.MiddleCenter,
                                fontSize  = 9,
                            });
                    }

                    if (entry.requiredRank > 1)
                    {
                        var mid = (fromPt + toPt) * 0.5f;
                        var lr  = new Rect(mid.x - 14, mid.y - 8, 28, 16);
                        EditorGUI.DrawRect(lr, new Color(0, 0, 0, 0.65f));
                        GUI.Label(lr, $"≥{entry.requiredRank}",
                            new GUIStyle(EditorStyles.miniLabel)
                            {
                                normal    = { textColor = new Color(1f, 0.9f, 0.3f) },
                                alignment = TextAnchor.MiddleCenter
                            });
                    }
                }
            }
        }

        /// <summary>
        /// Draws a cubic bezier as a dashed stroke by sampling it into short segments
        /// and skipping every other chunk — used for cyclic-dependency edges so they
        /// read as unmistakably different from a solid line, not just a different
        /// color (which alone can be too easy to confuse with the red "selected"
        /// state, especially for colorblind users).
        /// </summary>
        private static void DrawDashedBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3,
            Color color, float width, int segments = 48, int dashOn = 3, int dashOff = 2)
        {
            Vector2 Eval(float t)
            {
                float u = 1f - t;
                return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
            }

            Color prevColor = Handles.color;
            Handles.color = color;

            int cycle = dashOn + dashOff;
            for (int i = 0; i < segments; i++)
            {
                if (i % cycle >= dashOn) continue; // in the "gap" part of the dash pattern
                float t0 = (float)i       / segments;
                float t1 = (float)(i + 1) / segments;
                Handles.DrawAAPolyLine(width, Eval(t0), Eval(t1));
            }

            Handles.color = prevColor;
        }

        /// <summary>
        /// Draws a small filled triangle pointing in `dir`, tip centred at `tip`.
        /// Used to mark the direction of dependency along a connection line.
        /// </summary>
        private static void DrawArrowhead(Vector2 tip, Vector2 dir, Color color)
        {
            const float len = 8f;
            const float wid = 4.5f;

            Vector2 back = -dir;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            Vector2 baseCenter = tip + back * len;
            Vector2 p1 = baseCenter + perp * wid;
            Vector2 p2 = baseCenter - perp * wid;

            Color prevColor = Handles.color;
            Handles.color = color;
            Handles.DrawAAConvexPolygon(tip, p1, p2);
            Handles.color = prevColor;
        }

        // Returns offset from rect center to the edge in the given direction
        private static Vector2 ClampToEdge(Vector2 dir, Rect r)
        {
            float hw = r.width  * 0.5f;
            float hh = r.height * 0.5f;
            if (Mathf.Abs(dir.x) < 0.001f) return new Vector2(0, Mathf.Sign(dir.y) * hh);
            if (Mathf.Abs(dir.y) < 0.001f) return new Vector2(Mathf.Sign(dir.x) * hw, 0);
            float tx = hw / Mathf.Abs(dir.x);
            float ty = hh / Mathf.Abs(dir.y);
            float t  = Mathf.Min(tx, ty);
            return dir * t;
        }

        /// <summary>Approximate min distance from point p to the bezier used for a connection.</summary>
        private static float PointToBezierDist(Vector2 p, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            float minDist = float.MaxValue;
            const int steps = 20;
            for (int i = 0; i <= steps; i++)
            {
                float  t  = i / (float)steps;
                float  u  = 1f - t;
                Vector2 b = u*u*u*p0 + 3*u*u*t*p1 + 3*u*t*t*p2 + t*t*t*p3;
                float d = Vector2.Distance(p, b);
                if (d < minDist) minDist = d;
            }
            return minDist;
        }

        /// <summary>
        /// Returns the child node and prerequisite index of the connection line
        /// closest to point <paramref name="m"/> (canvas-clip space), or null if none within threshold.
        /// </summary>
        private (SkillNodeSO child, int prereqIdx) EdgeAt(Vector2 m, float threshold = 8f)
        {
            if (_tree?.allNodes == null) return (null, -1);
            float best = threshold;
            SkillNodeSO bestChild = null; int bestIdx = -1;

            foreach (var node in _tree.allNodes)
            {
                if (node?.prerequisites == null) continue;
                Rect toR = NodeRect(node);
                for (int pi = 0; pi < node.prerequisites.Count; pi++)
                {
                    var entry = node.prerequisites[pi];
                    if (entry?.node == null) continue;
                    Rect fromR = NodeRect(entry.node);

                    GetEdgePoints(node, entry, toR, fromR, out Vector2 fromPt, out Vector2 toPt, out Vector2 dirN);
                    float   tan    = Mathf.Clamp((toPt - fromPt).magnitude * 0.35f, 20f, 80f);
                    Vector2 tangent = dirN * tan;

                    float d = PointToBezierDist(m, fromPt, fromPt + tangent, toPt - tangent, toPt);
                    if (d < best) { best = d; bestChild = node; bestIdx = pi; }
                }
            }
            return (bestChild, bestIdx);
        }

        private void DrawNodes()
        {
            if (_tree?.allNodes == null) return;

            foreach (var n in _tree.allNodes)
                if (n != null) DrawNode(n);

            // Arrow overlays on selected / hovered node
            if (_tree?.allNodes != null)
                foreach (var n in _tree.allNodes)
                    if (n != null && (n == _selected || n == _hoveredNode))
                        DrawNodeArrows(n);

            // In-place rename field — drawn last so it's always on top
            if (_renamingNode != null) DrawRenameField();

            // Cycle badges drawn LAST so they always appear above arrows
            DrawCycleBadges();

            // Comment bubbles — topmost so never hidden
            DrawCommentBubbles();

            // Marquee rectangle
            if (_marqueeActive)
            {
                var mr = MarqueeRect();
                Color fill   = new Color(0.3f, 0.6f, 1f, 0.08f);
                Color border = new Color(0.4f, 0.7f, 1f, 0.85f);
                EditorGUI.DrawRect(mr, fill);
                // 1-px border
                EditorGUI.DrawRect(new Rect(mr.x,          mr.y,          mr.width, 1),    border);
                EditorGUI.DrawRect(new Rect(mr.x,          mr.yMax - 1,   mr.width, 1),    border);
                EditorGUI.DrawRect(new Rect(mr.x,          mr.y,          1, mr.height),   border);
                EditorGUI.DrawRect(new Rect(mr.xMax - 1,   mr.y,          1, mr.height),   border);
            }

            // Live wire drag
            if (_wireDragging && _wireDragFrom != null)
            {
                Rect fr  = NodeRect(_wireDragFrom);
                Vector2 from = fr.center;
                Vector2 to   = _wireDragCurrent;

                if (_wireDragTarget != null)
                    to = NodeRect(_wireDragTarget).center;

                Color wireCol = _wireDragTarget != null
                    ? new Color(0.4f, 1f, 0.5f, 1f)
                    : new Color(0.4f, 0.85f, 1f, 0.9f);

                Vector2 delta   = to - from;
                float   tan     = Mathf.Clamp(delta.magnitude * 0.4f, 20f, 100f);
                Vector2 tangent = delta.normalized * tan;
                Handles.DrawBezier(from, to, from + tangent, to - tangent, wireCol, null, 2.5f);

                // Dot at tip
                Handles.color = wireCol;
                Handles.DrawSolidDisc(to, Vector3.forward, 5f);
                Handles.color = Color.white;
            }
        }

        /// <summary>Returns true if the node's name, ID, branch or effect types contain the search query.</summary>
        private bool NodeMatchesSearch(SkillNodeSO node)
        {
            if (!_searchActive) return true;
            string q = _searchQuery.ToLowerInvariant();
            if ((node.displayName ?? "").ToLowerInvariant().Contains(q)) return true;
            if ((node.nodeId     ?? "").ToLowerInvariant().Contains(q)) return true;
            if (node.branch != null &&
                (node.branch.DisplayName.ToLowerInvariant().Contains(q) ||
                 (node.branch.branchId ?? "").ToLowerInvariant().Contains(q))) return true;
            if ((node.description ?? "").ToLowerInvariant().Contains(q)) return true;
            if (node.effects != null && node.effects.Any(e => e?.GetType().Name.ToLowerInvariant().Contains(q) == true)) return true;
            return false;
        }

        /// <summary>
        /// Draws cycle warning badges on top of everything else — after nodes AND arrows —
        /// so they're never obscured. Badge sits inside the top-right corner of the node.
        /// </summary>
        private void DrawCycleBadges()
        {
            var cyclic     = GetCyclicNodes();
            var orphans    = _showOrphans ? GetOrphanNodes() : null;
            var duplicates = GetDuplicateIdNodes();
            if (cyclic.Count == 0 && (orphans == null || orphans.Count == 0) && duplicates.Count == 0) return;
            if (_tree?.allNodes == null) return;

            var badgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal    = { textColor = Color.white },
                fontSize  = Mathf.Max(8, Mathf.RoundToInt(9 * _zoom)),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };

            foreach (var node in _tree.allNodes)
            {
                if (node == null) continue;
                Rect  r  = NodeRect(node);
                float bh = Mathf.Max(16f, 18f * _zoom);

                if (cyclic.Contains(node))
                {
                    var bR = new Rect(r.x, r.y, r.width, bh);
                    EditorGUI.DrawRect(bR, new Color(0.75f, 0.08f, 0.08f, 0.97f));
                    GUI.Label(bR, "⚠  CYCLE", badgeStyle);
                }
                else if (duplicates.Contains(node))
                {
                    var bR = new Rect(r.x, r.y, r.width, bh);
                    EditorGUI.DrawRect(bR, new Color(0.65f, 0.05f, 0.65f, 0.97f));
                    GUI.Label(bR, $"⚠  DUP ID: {node.nodeId}", badgeStyle);
                }
                else if (orphans != null && orphans.Contains(node))
                {
                    var bR = new Rect(r.x, r.y, r.width, bh);
                    EditorGUI.DrawRect(bR, new Color(0.55f, 0.35f, 0.02f, 0.95f));
                    GUI.Label(bR, "◌  ORPHAN", badgeStyle);
                }
            }
        }

        // =========================================================================
        //  In-place rename
        // =========================================================================
        private void DrawRenameField()
        {
            if (_renamingNode == null) return;

            Rect lr = NodeLabelRect(_renamingNode);

            // Background pill
            EditorGUI.DrawRect(new Rect(lr.x - 2, lr.y - 2, lr.width + 4, lr.height + 4),
                new Color(0.15f, 0.35f, 0.55f, 1f));
            EditorGUI.DrawRect(new Rect(lr.x - 1, lr.y - 1, lr.width + 2, lr.height + 2),
                new Color(0.4f, 0.75f, 1f, 1f));
            EditorGUI.DrawRect(lr, new Color(0.1f, 0.1f, 0.12f, 1f));

            // Focus the text field every frame while renaming
            GUI.SetNextControlName(RENAME_CTRL);
            var fieldStyle = new GUIStyle(EditorStyles.textField)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = Mathf.Max(8, Mathf.RoundToInt(9 * _zoom)),
            };
            string newText = GUI.TextField(lr, _renameText, fieldStyle);
            if (newText != _renameText) _renameText = newText;

            if (Event.current.type == EventType.Repaint)
                EditorGUI.FocusTextInControl(RENAME_CTRL);

            Event ev = Event.current;
            // Commit on Enter or Tab
            if (ev.type == EventType.KeyDown &&
                (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter || ev.keyCode == KeyCode.Tab))
            {
                CommitRename(); ev.Use();
            }
            // Cancel on Escape
            if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape)
            {
                _renamingNode = null; _renameText = ""; GUI.FocusControl(null); ev.Use(); Repaint();
            }
        }

        private void CommitRename()
        {
            if (_renamingNode == null) return;
            string newName = _renameText.Trim();
            if (!string.IsNullOrEmpty(newName) && newName != _renamingNode.displayName)
            {
                Undo.RecordObject(_renamingNode, "Rename Node");
                _renamingNode.displayName = newName;
                Dirty(_renamingNode);
                AssetDatabase.SaveAssets();
                InvalidateDuplicateCache();
            }
            _renamingNode = null; _renameText = "";
            GUI.FocusControl(null); Repaint();
        }

        private void StartRename(SkillNodeSO node)
        {
            _renamingNode = node;
            _renameText   = node.displayName ?? node.nodeId ?? "";
            Repaint();
        }

        // =========================================================================
        //  Comment bubbles
        // =========================================================================
        private void DrawCommentBubbles()
        {
            if (_tree?.allNodes == null) return;
            Event ev = Event.current;
            _commentHovered = null;

            foreach (var node in _tree.allNodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.editorComment)) continue;

                Rect r = NodeRect(node);

                // ── Icon position: top-right corner of node, above the ranks badge ──
                float iconSz = Mathf.Max(14f, 16f * _zoom);
                Rect  iconR  = new Rect(r.xMax - iconSz * 0.5f, r.yMin - iconSz * 0.6f, iconSz, iconSz);

                // Draw speech bubble icon (💬 as text label)
                bool hovered = iconR.Contains(ev.mousePosition);
                if (hovered) _commentHovered = node;

                Color bgCol = hovered ? new Color(0.9f, 0.85f, 0.2f, 1f) : new Color(0.25f, 0.55f, 0.9f, 0.92f);
                EditorGUI.DrawRect(new Rect(iconR.x - 1, iconR.y - 1, iconR.width + 2, iconR.height + 2),
                    new Color(0f, 0f, 0f, 0.5f));
                EditorGUI.DrawRect(iconR, bgCol);
                GUI.Label(iconR, "💬",
                    new GUIStyle(EditorStyles.label)
                    {
                        fontSize  = Mathf.Max(7, Mathf.RoundToInt(9 * _zoom)),
                        alignment = TextAnchor.MiddleCenter,
                    });

                EditorGUIUtility.AddCursorRect(iconR, MouseCursor.Text);

                // ── Tooltip bubble shown on hover ─────────────────────────────────
                if (hovered)
                {
                    string text    = node.editorComment.Trim();
                    float  bubbleW = Mathf.Clamp(text.Length * 6.5f * _zoom, 120f, 260f);
                    var    content = new GUIContent(text);
                    var    style   = new GUIStyle(EditorStyles.helpBox)
                    {
                        fontSize  = Mathf.Max(9, Mathf.RoundToInt(10 * _zoom)),
                        wordWrap  = true,
                        richText  = false,
                        alignment = TextAnchor.UpperLeft,
                        padding   = new RectOffset(6, 6, 4, 4),
                    };
                    style.normal.textColor = new Color(0.95f, 0.95f, 0.85f);

                    float bubbleH = style.CalcHeight(content, bubbleW);
                    bubbleH = Mathf.Clamp(bubbleH, 28f, 160f);

                    // Position above the icon, clamped so it doesn't go off-canvas
                    float bx = iconR.center.x - bubbleW * 0.5f;
                    float by = iconR.yMin - bubbleH - 6f;

                    EditorGUI.DrawRect(new Rect(bx - 1, by - 1, bubbleW + 2, bubbleH + 2),
                        new Color(0.9f, 0.85f, 0.2f, 0.8f));
                    EditorGUI.DrawRect(new Rect(bx, by, bubbleW, bubbleH),
                        new Color(0.12f, 0.12f, 0.14f, 0.97f));
                    GUI.Label(new Rect(bx, by, bubbleW, bubbleH), content, style);

                    Repaint();
                }
            }
        }

        private void DrawNode(SkillNodeSO node)
        {
            Rect r   = NodeRect(node);
            bool sel = node == _selected || _selection.Contains(node);
            bool hov = node == _hoveredNode && !sel;
            bool wireTarget = _wireDragging && node == _wireDragTarget && node != _wireDragFrom;

            // ── Search state ─────────────────────────────────────────────────────
            bool matches  = NodeMatchesSearch(node);
            bool dimmed   = _searchActive && !matches;
            float alpha   = dimmed ? 0.2f : 1f;

            // ── Cycle state ───────────────────────────────────────────────────────
            bool isCyclic = GetCyclicNodes().Contains(node);

            // ── Orphan state ──────────────────────────────────────────────────────
            bool isOrphan = _showOrphans && GetOrphanNodes().Contains(node);

            // ── Duplicate ID state ────────────────────────────────────────────────
            bool isDuplicate = GetDuplicateIdNodes().Contains(node);

            // ── Missing ref state ─────────────────────────────────────────────────
            bool hasMissingRef = node.prerequisites != null &&
                node.prerequisites.Any(p => p == null || p.node == null || !p.node);

            Color branchCol = BranchColor(node.branch);

            // ── Drop shadow ──────────────────────────────────────────────────────
            GUI.color = new Color(1f, 1f, 1f, alpha);
            EditorGUI.DrawRect(new Rect(r.x + 4, r.y + 4, r.width, r.height),
                new Color(0, 0, 0, 0.4f * alpha));

            // ── Search match ring ─────────────────────────────────────────────────
            if (_searchActive && matches)
            {
                float pulse = Mathf.Sin((float)EditorApplication.timeSinceStartup * 3f) * 0.15f + 0.85f;
                Color matchRing = new Color(1f, 0.92f, 0.2f, pulse);
                EditorGUI.DrawRect(new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8), matchRing);
                Repaint();
            }

            // ── Cycle warning ring ────────────────────────────────────────────────
            if (isCyclic)
            {
                float pulse = Mathf.Sin((float)EditorApplication.timeSinceStartup * 5f) * 0.3f + 0.7f;
                EditorGUI.DrawRect(new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8),
                    new Color(1f, 0.15f, 0.15f, pulse));
                Repaint();
            }

            // ── Orphan amber ring ─────────────────────────────────────────────────
            if (isOrphan)
            {
                float pulse = Mathf.Sin((float)EditorApplication.timeSinceStartup * 2.5f) * 0.2f + 0.6f;
                EditorGUI.DrawRect(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6),
                    new Color(1f, 0.6f, 0.05f, pulse));
                Repaint();
            }

            // ── Duplicate ID magenta ring ─────────────────────────────────────────
            if (isDuplicate)
            {
                float pulse = Mathf.Sin((float)EditorApplication.timeSinceStartup * 4f) * 0.25f + 0.75f;
                EditorGUI.DrawRect(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6),
                    new Color(1f, 0.1f, 0.9f, pulse));
                Repaint();
            }

            // ── Outer ring (selection / hover) ───────────────────────────────────
            float ringW = sel ? 3f : (hov ? 2f : (wireTarget ? 3f : 1.5f));
            Color ringCol = wireTarget ? new Color(0.3f, 1f, 0.4f, 1f)
                          : sel  ? new Color(0.9f, 0.95f, 1f, 1f)
                          : hov  ? new Color(0.7f, 0.85f, 1f, 0.9f)
                          :        new Color(branchCol.r * 0.6f, branchCol.g * 0.6f, branchCol.b * 0.6f, 0.8f);
            EditorGUI.DrawRect(new Rect(r.x - ringW, r.y - ringW, r.width + ringW * 2, r.height + ringW * 2), ringCol);

            // ── Background: dark circle body ─────────────────────────────────────
            Color bgBase = sel
                ? new Color(branchCol.r * 0.5f + 0.1f, branchCol.g * 0.5f + 0.1f, branchCol.b * 0.5f + 0.1f)
                : new Color(0.14f, 0.14f, 0.16f);
            EditorGUI.DrawRect(r, bgBase);

            // ── Branch colour accent strip (bottom) ──────────────────────────────
            float stripH = Mathf.Max(3f, 5f * _zoom);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - stripH, r.width, stripH),
                sel ? Color.Lerp(branchCol, Color.white, 0.3f) : branchCol);

            // ── Icon (large, centered) ───────────────────────────────────────────
            if (node.icon != null)
            {
                float iconSz  = r.width * 0.55f;
                float iconTop = r.y + (r.height - stripH - iconSz) * 0.5f;
                GUI.DrawTexture(new Rect(r.x + (r.width - iconSz) * 0.5f, iconTop, iconSz, iconSz),
                    node.icon.texture, ScaleMode.ScaleToFit);
            }
            else
            {
                // No icon: show abbreviated name in center
                string abbr  = node.displayName ?? node.nodeId ?? "?";
                string label = abbr.Length > 2 ? abbr[..2].ToUpper() : abbr.ToUpper();
                GUI.Label(new Rect(r.x, r.y, r.width, r.height - stripH),
                    label,
                    new GUIStyle(EditorStyles.boldLabel)
                    {
                        normal    = { textColor = sel ? Color.white : new Color(0.8f, 0.8f, 0.8f) },
                        fontSize  = Mathf.Max(10, Mathf.RoundToInt(16 * _zoom)),
                        alignment = TextAnchor.MiddleCenter,
                    });
            }

            // ── Node name below the node box ─────────────────────────────────────
            float labelW = Mathf.Max(r.width * 2f, 90f * _zoom);
            Rect labelRect = new Rect(
                r.center.x - labelW * 0.5f,
                r.yMax + 3f * _zoom,
                labelW, 18f * _zoom);
            // Don't draw label while renaming — DrawRenameField() handles it
            if (node != _renamingNode)
                GUI.Label(labelRect,
                    node.displayName ?? node.nodeId ?? node.name,
                    new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal    = { textColor = sel ? Color.white : new Color(0.82f, 0.82f, 0.82f) },
                        fontStyle = sel ? FontStyle.Bold : FontStyle.Normal,
                        fontSize  = Mathf.Max(7, Mathf.RoundToInt(9 * _zoom)),
                        alignment = TextAnchor.UpperCenter,
                        wordWrap  = false,
                    });

            // ── Cost badge (bottom-left of box) ──────────────────────────────────
            if (node.costsPerRank != null && node.costsPerRank.Count > 0)
            {
                var firstCost = node.costsPerRank[0];
                if (firstCost.resource != null)
                {
                    string costLabel = firstCost.Format(1);
                    Rect costRect = new Rect(r.x - 1, r.yMax + labelRect.height + 2f * _zoom,
                        labelW, 14f * _zoom);
                    costRect.x = r.center.x - labelW * 0.5f;
                    GUI.Label(costRect, costLabel,
                        new GUIStyle(EditorStyles.miniLabel)
                        {
                            normal    = { textColor = new Color(1f, 0.88f, 0.4f, 0.9f) },
                            fontSize  = Mathf.Max(6, Mathf.RoundToInt(8 * _zoom)),
                            alignment = TextAnchor.UpperCenter,
                        });
                }
            }

            // ── Ranks badge (top-right corner) ───────────────────────────────────
            if (node.maxRanks > 1)
            {
                var bR = new Rect(r.xMax - 16 * _zoom, r.y, 16 * _zoom, 14 * _zoom);
                EditorGUI.DrawRect(bR, new Color(0.08f, 0.08f, 0.08f, 0.85f));
                GUI.Label(bR, $"×{node.maxRanks}",
                    new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal    = { textColor = new Color(1, 1, 0.5f, 1) },
                        fontSize  = Mathf.Max(6, Mathf.RoundToInt(8 * _zoom)),
                        alignment = TextAnchor.MiddleCenter,
                    });
            }

            // ── Effect count badge (top-left corner) ─────────────────────────────
            int effCount = node.effects?.Count ?? 0;
            if (effCount > 0)
            {
                var bR = new Rect(r.x, r.y, 16 * _zoom, 14 * _zoom);
                EditorGUI.DrawRect(bR, new Color(0.08f, 0.08f, 0.08f, 0.85f));
                GUI.Label(bR, effCount.ToString(),
                    new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal    = { textColor = new Color(0.6f, 0.9f, 1f, 1f) },
                        fontSize  = Mathf.Max(6, Mathf.RoundToInt(8 * _zoom)),
                        alignment = TextAnchor.MiddleCenter,
                    });
            }

            GUI.color = Color.white; // reset alpha tint

            // ── Simulate mode overlay ─────────────────────────────────────────────
            if (_simMode)
            {
                var simState  = SimNodeState(node);
                int simRank   = _simState.GetRank(node.nodeId);
                Color overlay;
                string label;

                switch (simState)
                {
                    case NodeVisibilityState.Unlocked:
                        overlay = new Color(0.1f, 0.7f, 0.2f, 0.55f);
                        label   = simRank >= node.maxRanks ? $"✓ MAX" : $"✓ {simRank}/{node.maxRanks}";
                        break;
                    case NodeVisibilityState.Unlockable:
                        overlay = new Color(0.8f, 0.75f, 0.0f, 0.45f);
                        label   = "● Unlock";
                        break;
                    case NodeVisibilityState.Visible:
                        overlay = new Color(0.15f, 0.15f, 0.15f, 0.65f);
                        label   = "🔒";
                        break;
                    case NodeVisibilityState.Mystery:
                        overlay = new Color(0.25f, 0.1f, 0.4f, 0.75f);
                        label   = "?";
                        break;
                    default: // Hidden
                        overlay = new Color(0f, 0f, 0f, 0.82f);
                        label   = "";
                        break;
                }

                EditorGUI.DrawRect(r, overlay);
                if (!string.IsNullOrEmpty(label))
                    GUI.Label(r, label,
                        new GUIStyle(EditorStyles.boldLabel)
                        {
                            normal    = { textColor = Color.white },
                            fontSize  = Mathf.Max(9, Mathf.RoundToInt(11 * _zoom)),
                            alignment = TextAnchor.MiddleCenter,
                        });
            }

            // ── Missing ref magenta dot (bottom-left corner) ──────────────────────
            if (hasMissingRef)
            {
                float ds = Mathf.Max(8f, 10f * _zoom);
                var dotR = new Rect(r.x + 2, r.yMax - ds - 2, ds, ds);
                EditorGUI.DrawRect(dotR, new Color(0.9f, 0.1f, 0.8f, 0.95f));
                GUI.Label(dotR, "!",
                    new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal    = { textColor = Color.white },
                        fontSize  = Mathf.Max(6, Mathf.RoundToInt(7 * _zoom)),
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold,
                    });
            }
        }

        // ── Directional arrow overlays ────────────────────────────────────────────
        private static readonly (string label, Vector2 dir)[] ArrowDefs =
        {
            ("▲", Vector2.up),
            ("▼", Vector2.down),
            ("◀", Vector2.left),
            ("▶", Vector2.right),
            ("↖", new Vector2(-1,  1)),   // up-left
            ("↗", new Vector2( 1,  1)),   // up-right
            ("↙", new Vector2(-1, -1)),   // down-left
            ("↘", new Vector2( 1, -1)),   // down-right
        };

        /// <summary>
        /// Returns the 4 arrow button rects in canvas-clip space for a given node.
        /// Index order matches ArrowDefs: 0=Up, 1=Down, 2=Left, 3=Right.
        /// </summary>
        private Rect[] GetArrowRects(SkillNodeSO node)
        {
            Rect r   = NodeRect(node);
            float az = ARROW_SIZE * Mathf.Clamp(_zoom, 0.6f, 1.5f);
            float gap = 6f;
            // Diagonals sit at the actual corners of the node, offset outward by gap
            // so they're clearly separated from both cardinal arrows and the node itself
            float cornerOff = gap;
            return new Rect[]
            {
                // Cardinals — centred on each edge
                new Rect(r.center.x - az * 0.5f,  r.yMin - az - gap,        az, az),  // ▲ up
                new Rect(r.center.x - az * 0.5f,  r.yMax + gap,             az, az),  // ▼ down
                new Rect(r.xMin - az - gap,        r.center.y - az * 0.5f,  az, az),  // ◀ left
                new Rect(r.xMax + gap,             r.center.y - az * 0.5f,  az, az),  // ▶ right
                // Diagonals — at the four corners, pushed out so they don't overlap cardinals
                new Rect(r.xMin - az - cornerOff,  r.yMin - az - cornerOff, az, az),  // ↖ up-left
                new Rect(r.xMax + cornerOff,       r.yMin - az - cornerOff, az, az),  // ↗ up-right
                new Rect(r.xMin - az - cornerOff,  r.yMax + cornerOff,      az, az),  // ↙ down-left
                new Rect(r.xMax + cornerOff,       r.yMax + cornerOff,      az, az),  // ↘ down-right
            };
        }

        /// <summary>
        /// Pure visual draw of the 4 directional arrows. All click handling is done
        /// in HandleCanvasInput BEFORE the node-drag switch so clicks are never stolen.
        /// </summary>
        private void DrawNodeArrows(SkillNodeSO node)
        {
            var rects = GetArrowRects(node);
            Vector2 mp = Event.current.mousePosition;

            for (int i = 0; i < rects.Length; i++)
            {
                bool isDiagonal = i >= 4;
                bool hovered    = rects[i].Contains(mp);

                // Diagonals are slightly smaller and use a warm amber tint
                Rect drawRect = isDiagonal
                    ? new Rect(rects[i].x + 2, rects[i].y + 2, rects[i].width - 4, rects[i].height - 4)
                    : rects[i];

                Color bg = hovered
                    ? (isDiagonal ? new Color(0.7f, 0.5f, 0.1f, 0.95f) : new Color(0.2f, 0.6f, 0.75f, 0.95f))
                    : (isDiagonal ? new Color(0.18f, 0.14f, 0.08f, 0.88f) : new Color(0.12f, 0.12f, 0.12f, 0.88f));
                Color fg = hovered
                    ? new Color(1f, 1f, 0.85f, 1f)
                    : (isDiagonal ? new Color(0.75f, 0.65f, 0.45f, 1f) : new Color(0.85f, 0.85f, 0.85f, 1f));

                int fontSize = isDiagonal
                    ? Mathf.Max(7, Mathf.RoundToInt(10 * _zoom))
                    : Mathf.Max(9, Mathf.RoundToInt(12 * _zoom));

                var arrowStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize  = fontSize,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    normal    = { textColor = fg },
                };

                EditorGUI.DrawRect(drawRect, new Color(0f, 0f, 0f, 0.5f));
                EditorGUI.DrawRect(new Rect(drawRect.x + 1, drawRect.y + 1, drawRect.width - 2, drawRect.height - 2), bg);
                GUI.Label(drawRect, ArrowDefs[i].label, arrowStyle);
                EditorGUIUtility.AddCursorRect(drawRect, MouseCursor.ArrowPlus);

                if (hovered) Repaint();
            }
        }

        /// <summary>
        /// Called when the user clicks a directional arrow on a node.
        /// Opens the quick-create popup at the target position; after the user names
        /// the node and confirms, QuickCreateNode() wires _pendingParent automatically.
        /// </summary>
        private void SpawnConnectedNode(SkillNodeSO from, Vector2 dir)
        {
            // Normalize so diagonal directions travel the same distance per axis
            Vector2 absDir = new Vector2(Mathf.Abs(dir.x), Mathf.Abs(dir.y));
            Vector2 worldOffset = new Vector2(
                Mathf.Sign(dir.x) * (absDir.x > 0 ? NODE_SPACING_X : 0),
                Mathf.Sign(dir.y) * (absDir.y > 0 ? -NODE_SPACING_Y : 0)   // Y flipped (graphPos down = screen down)
            );
            Vector2 targetPos = SnapToGrid(from.graphPosition + worldOffset);

            // Store the parent so QuickCreateNode can wire it after naming
            _pendingParent = from;

            // Open the quick-create popup at the target canvas position
            _quickCreateCanvasPos  = targetPos;
            _quickCreateOpen       = true;
            _quickCreateName       = "";
            _quickCreateId         = "";
            _quickCreateIdEdited   = false;
            _quickCreateBranch     = from.branch;

            Repaint();
        }

        // ── Canvas input ──────────────────────────────────────────────────────────
        private void HandleCanvasInput(Rect canvasRect)
        {
            if (_divDrag || _insResizing) return;
            Event  e = Event.current;
            Vector2 m = e.mousePosition - new Vector2(canvasRect.x, canvasRect.y);
            if (!canvasRect.Contains(e.mousePosition)) return;

            // ── Block canvas events when inspector or quick-create popup is under mouse ──
            bool overInspector   = _inspectorOpen  && _selected != null && _inspectorScreenRect.Contains(e.mousePosition);
            bool overQuickCreate = _quickCreateOpen && e.type != EventType.KeyDown;
            if (overInspector || overQuickCreate) return;

            // ── Track hovered node and hovered edge ───────────────────────────────
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            {
                var prev = _hoveredNode;
                _hoveredNode = NodeAt(m);
                if (_hoveredNode != prev) Repaint();

                // Edge hover (only when not over a node)
                if (_hoveredNode == null)
                {
                    var (ec, ei) = EdgeAt(m);
                    if (ec != _hoveredEdgeChild || ei != _hoveredEdgeIndex)
                    {
                        _hoveredEdgeChild = ec; _hoveredEdgeIndex = ei;
                        EditorGUIUtility.AddCursorRect(new Rect(m.x - 8, m.y - 8, 16, 16), MouseCursor.Link);
                        Repaint();
                    }
                }
                else if (_hoveredEdgeChild != null)
                {
                    _hoveredEdgeChild = null; _hoveredEdgeIndex = -1; Repaint();
                }
            }

            // ── Space key: pan cursor + track held state ──────────────────────────
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Space && !_quickCreateOpen)
            {
                _spaceHeld = true;
                EditorGUIUtility.AddCursorRect(canvasRect, MouseCursor.Pan);
                e.Use(); Repaint(); return;
            }
            if (e.type == EventType.KeyUp && e.keyCode == KeyCode.Space)
            {
                _spaceHeld = false;
                Repaint(); return;
            }

            // Show pan cursor while space is held
            if (_spaceHeld)
                EditorGUIUtility.AddCursorRect(canvasRect, MouseCursor.Pan);

            // Space + left-mouse drag → pan (takes priority over node drag)
            if (_spaceHeld && e.type == EventType.MouseDown && e.button == 0)
            {
                _panning = true; _panStart = m; _dragging = null; e.Use(); return;
            }
            if (_wireDragging)
            {
                _wireDragCurrent = m;
                var overNode = NodeAt(m);
                _wireDragTarget = (overNode != null && overNode != _wireDragFrom) ? overNode : null;

                if (e.type == EventType.MouseDrag)  { e.Use(); Repaint(); }

                if (e.type == EventType.MouseUp && e.button == 0)
                {
                    if (_wireDragTarget != null)
                    {
                        // Drop on existing node → add _wireDragFrom as prerequisite of target
                        _wireDragTarget.prerequisites ??= new List<PrerequisiteEntry>();
                        bool already = _wireDragTarget.prerequisites.Any(p => p?.node == _wireDragFrom);
                        if (!already && _wireDragTarget != _wireDragFrom)
                        {
                            Undo.RecordObject(_wireDragTarget, "Add Connection");
                            _wireDragTarget.prerequisites.Add(
                                new PrerequisiteEntry { node = _wireDragFrom, requiredRank = 1 });
                            Dirty(_wireDragTarget);
                            AssetDatabase.SaveAssets();
                            InvalidateCycleCache(); InvalidateOrphanCache(); InvalidateDuplicateCache();
                        }
                    }
                    else
                    {
                        // Drop on empty canvas → spawn at drop position or use directional offset
                        float dragDist = Vector2.Distance(m, _wireDragStart);
                        _pendingParent = _wireDragFrom;
                        if (dragDist < 8f)
                        {
                            // Tiny movement = treated as a click → use arrow direction for placement
                            SpawnConnectedNode(_wireDragFrom, ArrowDefs[_wireDragArrowIdx].dir);
                        }
                        else
                        {
                            // Real drag to empty space → create at drop position
                            _quickCreateCanvasPos  = C2W(m);
                            _quickCreateOpen       = true;
                            _quickCreateName       = "";
                            _quickCreateId         = "";
                            _quickCreateIdEdited   = false;
                            _quickCreateBranch     = _wireDragFrom.branch;
                        }
                    }

                    _wireDragging   = false;
                    _wireDragFrom   = null;
                    _wireDragTarget = null;
                    e.Use(); Repaint(); return;
                }

                // Cancel on right-click or Escape
                if ((e.type == EventType.MouseDown && e.button == 1) ||
                    (e.type == EventType.KeyDown   && e.keyCode == KeyCode.Escape))
                {
                    _wireDragging = false; _wireDragFrom = null; _wireDragTarget = null;
                    e.Use(); Repaint(); return;
                }

                return; // swallow all other events while wire is live
            }

            // ── PRIORITY: Arrow MouseDown → start wire drag ───────────────────────
            if (e.type == EventType.MouseDown && e.button == 0 && !_quickCreateOpen && _selection.Count == 0)
            {
                if (_tree?.allNodes != null)
                {
                    foreach (var candidate in _tree.allNodes)
                    {
                        if (candidate == null) continue;
                        if (candidate != _selected && candidate != _hoveredNode) continue;
                        var arrowRects = GetArrowRects(candidate);
                        for (int ai = 0; ai < arrowRects.Length; ai++)
                        {
                            if (arrowRects[ai].Contains(m))
                            {
                                // Start wire drag — spawn vs connect decided on MouseUp
                                _wireDragging    = true;
                                _wireDragFrom    = candidate;
                                _wireDragArrowIdx = ai;
                                _wireDragStart   = m;
                                _wireDragCurrent = m;
                                _wireDragTarget  = null;
                                e.Use(); return;
                            }
                        }
                    }
                }
            }

            bool shift = e.shift;

            // Commit any in-progress rename when clicking elsewhere
            if (e.type == EventType.MouseDown && _renamingNode != null)
            {
                if (!NodeLabelRect(_renamingNode).Contains(m))
                    CommitRename();
            }

            // ── Simulate mode: intercept node clicks ──────────────────────────────
            if (_simMode && e.type == EventType.MouseDown && e.button == 0 && !_quickCreateOpen)
            {
                var simHit = NodeAt(m);
                if (simHit != null)
                {
                    if (e.alt) SimTryRefund(simHit);
                    else       SimTryUnlock(simHit);
                    e.Use(); Repaint(); return;
                }
            }

            switch (e.type)
            {
                // ── LEFT CLICK ────────────────────────────────────────────────────
                case EventType.MouseDown when e.button == 0 && !_quickCreateOpen:
                {
                    var hit = NodeAt(m);
                    if (hit != null)
                    {
                        // ── Double-click → open inspector ─────────────────────────
                        bool isDouble = (hit == _lastClickedNode) &&
                                        (EditorApplication.timeSinceStartup - _lastClickTime) < DOUBLE_CLICK_SECS;
                        _lastClickedNode = hit;
                        _lastClickTime   = EditorApplication.timeSinceStartup;

                        if (isDouble)
                        {
                            _dragging = null;
                            _selection.Clear();
                            _selected           = hit;
                            _inspectorOpen      = true;
                            _inspectorCanvasPos = hit.graphPosition;
                            Selection.activeObject = hit;
                            _leftTab = 1;
                            GUI.FocusControl(null); e.Use(); Repaint();
                            break;
                        }

                        if (shift)
                        {
                            // Shift-click: toggle in multi-select, never opens inspector
                            if (_selection.Contains(hit))
                                _selection.Remove(hit);
                            else
                            {
                                if (_selected != null) _selection.Add(_selected);
                                _selection.Add(hit);
                            }
                            _selected      = hit;
                            _inspectorOpen = false;
                        }
                        else
                        {
                            // Single click: select + prepare drag, keep inspector closed
                            bool inGroup = _selection.Contains(hit);
                            if (!inGroup)
                            {
                                _selection.Clear();
                                _selected = hit;
                                // Close inspector — user must double-click to open it
                                // (unless it was already open for this same node)
                                if (_inspectorOpen && _selected == hit)
                                    _inspectorCanvasPos = hit.graphPosition; // just reposition
                                else
                                    _inspectorOpen = false;
                                Selection.activeObject = hit;
                            }
                            else
                            {
                                _selected = hit; // keep group, switch primary
                            }
                        }

                        _dragging   = hit;
                        _dragOffset = m - W2C(hit.graphPosition);
                        _marqueeActive = false;
                        _selectedEdgeChild = null; _selectedEdgeIndex = -1;

                        // Record undo for all nodes that will move (group or single)
                        if (_selection.Count > 0)
                        {
                            var undoObjs = _selection.Where(n => n != null).Cast<UnityEngine.Object>().ToArray();
                            Undo.RecordObjects(undoObjs, "Move Nodes");
                        }
                        else
                        {
                            Undo.RecordObject(hit, "Move Node");
                        }
                    }
                    else
                    {
                        // Clicked empty canvas — check for edge click first
                        var (ec, ei) = EdgeAt(m);
                        if (ec != null)
                        {
                            // Selected an edge
                            _selectedEdgeChild = ec;
                            _selectedEdgeIndex = ei;
                            _selection.Clear();
                            _selected      = null;
                            _inspectorOpen = false;
                            _marqueeActive = false;
                            _lastClickedNode = null;
                            GUI.FocusControl(null); e.Use(); Repaint();
                            break;
                        }

                        // True empty canvas click
                        _selectedEdgeChild = null; _selectedEdgeIndex = -1;
                        if (!shift)
                        {
                            _selection.Clear();
                            _selected      = null;
                            _inspectorOpen = false;
                        }
                        // Start marquee
                        _marqueeActive = true;
                        _marqueeStart  = m;
                        _marqueeEnd    = m;
                        _lastClickedNode = null;
                    }
                    GUI.FocusControl(null); e.Use(); Repaint();
                    break;
                }

                // ── RIGHT CLICK ───────────────────────────────────────────────────
                case EventType.MouseDown when e.button == 1 && !_quickCreateOpen:
                {
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
                        // If right-clicking inside multi-select, act on the group
                        bool inGroup = _selection.Contains(hitNode) || _selection.Count == 0;
                        _selected = hitNode;
                        if (!_selection.Contains(hitNode)) _selection.Clear();

                        var menu = new GenericMenu();
                        int selCount = _selection.Count > 0 ? _selection.Count : 1;
                        string countLabel = selCount > 1 ? $" ({selCount} nodes)" : "";

                        menu.AddItem(new GUIContent("Rename"), false, () =>
                            { _selected = hitNode; StartRename(hitNode); });
                        menu.AddSeparator("");
                        menu.AddItem(new GUIContent("Edit Node"), false, () =>
                            { _leftTab = 1; _inspectorOpen = _selection.Count == 0; Repaint(); });
                        menu.AddItem(new GUIContent("Ping Asset"), false, () =>
                            EditorGUIUtility.PingObject(hitNode));
                        menu.AddSeparator("");
                        menu.AddItem(new GUIContent($"Remove from Tree{countLabel}"), false, () =>
                        {
                            var toRemove = _selection.Count > 0
                                ? new List<SkillNodeSO>(_selection)
                                : new List<SkillNodeSO> { hitNode };
                            string msg = toRemove.Count == 1
                                ? $"Remove '{toRemove[0].displayName}' from tree? (Asset kept on disk)"
                                : $"Remove {toRemove.Count} nodes from tree? (Assets kept on disk)";
                            if (EditorUtility.DisplayDialog("Remove", msg, "Remove", "Cancel"))
                            {
                                Undo.RecordObject(_tree, toRemove.Count == 1 ? "Remove Node" : "Remove Nodes");
                                foreach (var n in toRemove)
                                {
                                    _tree.allNodes.Remove(n);
                                    if (_selected == n) { _selected = null; _inspectorOpen = false; }
                                }
                                _selection.Clear();
                                Dirty(_tree); InvalidateOrphanCache(); Repaint();
                            }
                        });
                        menu.ShowAsContext(); e.Use();
                    }
                    break;
                }

                case EventType.MouseDown when e.button == 2:
                    _panning = true; _panStart = m; e.Use(); break;

                // ── DRAG ──────────────────────────────────────────────────────────
                case EventType.MouseDrag when e.button == 0:
                {
                    if (_marqueeActive)
                    {
                        _marqueeEnd = m;
                        // Live-update selection to nodes inside marquee
                        if (!shift) _selection.Clear();
                        var mr = MarqueeRect();
                        if (_tree?.allNodes != null)
                            foreach (var n in _tree.allNodes)
                                if (n != null && mr.Overlaps(NodeRect(n)))
                                    _selection.Add(n);
                        e.Use(); Repaint();
                    }
                    else if (_dragging != null)
                    {
                        var wp = C2W(m - _dragOffset);
                        if (_snapToGrid)
                        {
                            wp.x = Mathf.Round(wp.x / GRID_SIZE) * GRID_SIZE;
                            wp.y = Mathf.Round(wp.y / GRID_SIZE) * GRID_SIZE;
                        }
                        Vector2 delta = wp - _dragging.graphPosition;

                        if (_selection.Count > 0)
                        {
                            // Move entire group
                            foreach (var n in _selection)
                            {
                                if (n == null) continue;
                                n.graphPosition += delta;
                                EditorUtility.SetDirty(n);
                            }
                        }
                        _dragging.graphPosition = wp;
                        if (_inspectorOpen) _inspectorCanvasPos = wp;
                        EditorUtility.SetDirty(_dragging);
                        e.Use(); Repaint();
                    }
                    else if (_panning)
                    {
                        _offset += m - _panStart; _panStart = m; e.Use(); Repaint();
                    }
                    break;
                }

                case EventType.MouseDrag when _panning:
                    _offset += m - _panStart; _panStart = m; e.Use(); Repaint(); break;

                // ── MOUSE UP ──────────────────────────────────────────────────────
                case EventType.MouseUp:
                    if (_dragging != null) AssetDatabase.SaveAssets();
                    _dragging = null; _panning = false;
                    if (_marqueeActive)
                    {
                        _marqueeActive = false;
                        // If only one node selected via marquee, make it primary
                        if (_selection.Count == 1)
                        {
                            _selected = _selection.First();
                            _selection.Clear();
                            _inspectorOpen      = true;
                            _inspectorCanvasPos = _selected.graphPosition;
                        }
                        Repaint();
                    }
                    break;

                // ── DELETE ────────────────────────────────────────────────────────
                case EventType.KeyDown when e.keyCode == KeyCode.Delete && !_quickCreateOpen:
                {
                    // Edge selected → remove that prerequisite
                    if (_selectedEdgeChild != null && _selectedEdgeIndex >= 0
                        && _selectedEdgeIndex < (_selectedEdgeChild.prerequisites?.Count ?? 0))
                    {
                        Undo.RecordObject(_selectedEdgeChild, "Remove Connection");
                        _selectedEdgeChild.prerequisites.RemoveAt(_selectedEdgeIndex);
                        Dirty(_selectedEdgeChild);
                        AssetDatabase.SaveAssets();
                        InvalidateCycleCache(); InvalidateOrphanCache(); InvalidateDuplicateCache();
                        _selectedEdgeChild = null; _selectedEdgeIndex = -1;
                        Repaint(); e.Use(); break;
                    }

                    // Node(s) selected → remove from tree
                    var toRemove = _selection.Count > 0
                        ? new List<SkillNodeSO>(_selection)
                        : (_selected != null ? new List<SkillNodeSO> { _selected } : null);

                    if (toRemove != null && toRemove.Count > 0)
                    {
                        string msg = toRemove.Count == 1
                            ? $"Remove '{toRemove[0].displayName}' from tree? (Asset kept on disk)"
                            : $"Remove {toRemove.Count} nodes from tree? (Assets kept on disk)";
                        if (EditorUtility.DisplayDialog("Remove", msg, "Remove", "Cancel"))
                        {
                            Undo.RecordObject(_tree, toRemove.Count == 1 ? "Remove Node" : "Remove Nodes");
                            foreach (var n in toRemove)
                            {
                                _tree.allNodes.Remove(n);
                                if (_selected == n) { _selected = null; _inspectorOpen = false; }
                            }
                            _selection.Clear();
                            Dirty(_tree); InvalidateOrphanCache(); Repaint();
                        }
                    }
                    e.Use(); break;
                }

                case EventType.KeyDown when e.keyCode == KeyCode.Escape && !_quickCreateOpen:
                    _selection.Clear(); _selected = null; _inspectorOpen = false;
                    _selectedEdgeChild = null; _selectedEdgeIndex = -1;
                    Repaint(); e.Use(); break;

                case EventType.KeyDown when e.keyCode == KeyCode.F && !_quickCreateOpen:
                {
                    // Collect nodes to fit: multi-select group, single selected, or all
                    List<SkillNodeSO> toFit;
                    if (_selection.Count > 0)
                        toFit = _selection.Where(n => n != null).ToList();
                    else if (_selected != null)
                        toFit = new List<SkillNodeSO> { _selected };
                    else
                        toFit = _tree?.allNodes?.Where(n => n != null).ToList() ?? new List<SkillNodeSO>();

                    if (toFit.Count > 0)
                    {
                        // Compute bounding box in world space
                        Vector2 mn = Vector2.one * float.MaxValue;
                        Vector2 mx = Vector2.one * float.MinValue;
                        foreach (var n in toFit)
                        {
                            mn = Vector2.Min(mn, n.graphPosition);
                            mx = Vector2.Max(mx, n.graphPosition + new Vector2(NODE_W, NODE_H));
                        }

                        var canvasSz = new Vector2(position.width - _leftW - DIV_W, position.height - TOOLBAR_H);
                        float margin = 60f;

                        // Compute zoom to fit bounding box with margin
                        float fitZoomX = (canvasSz.x - margin * 2) / (mx.x - mn.x + NODE_W);
                        float fitZoomY = (canvasSz.y - margin * 2) / (mx.y - mn.y + NODE_H);
                        float fitZoom  = Mathf.Clamp(Mathf.Min(fitZoomX, fitZoomY), ZOOM_MIN, ZOOM_MAX);

                        // Only zoom out to fit; don't zoom in past 1× for single nodes
                        if (toFit.Count == 1) fitZoom = Mathf.Min(fitZoom, 1f);
                        _zoom = fitZoom;

                        // Centre the bounding box
                        Vector2 worldCentre = (mn + mx) * 0.5f;
                        _offset = canvasSz * 0.5f - worldCentre * _zoom;
                        e.Use(); Repaint();
                    }
                    break;
                }

                // ── KEYBOARD NAVIGATION ───────────────────────────────────────────
                case EventType.KeyDown when !_quickCreateOpen && _renamingNode == null &&
                    (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.DownArrow ||
                     e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.RightArrow ||
                     e.keyCode == KeyCode.Tab):
                {
                    if (_tree?.allNodes == null || _tree.allNodes.Count == 0) break;

                    var nodes = _tree.allNodes.Where(n => n != null).ToList();
                    var current = _selected;

                    SkillNodeSO next = null;

                    if (e.keyCode == KeyCode.Tab)
                    {
                        // Tab / Shift+Tab — cycle through all nodes in tree order
                        int idx = current != null ? nodes.IndexOf(current) : -1;
                        if (e.shift)
                            next = nodes[(idx - 1 + nodes.Count) % nodes.Count];
                        else
                            next = nodes[(idx + 1) % nodes.Count];
                    }
                    else if (e.keyCode == KeyCode.UpArrow)
                    {
                        // ↑ — go to first prerequisite (parent)
                        if (current?.prerequisites != null)
                        {
                            var validPrereqs = current.prerequisites
                                .Where(p => p?.node != null)
                                .Select(p => p.node)
                                .ToList();
                            next = validPrereqs.FirstOrDefault();
                        }
                    }
                    else if (e.keyCode == KeyCode.DownArrow)
                    {
                        // ↓ — go to first child (node that has current as prerequisite)
                        next = nodes.FirstOrDefault(n =>
                            n != current &&
                            n.prerequisites != null &&
                            n.prerequisites.Any(p => p?.node == current));
                    }
                    else if (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.RightArrow)
                    {
                        // ←/→ — cycle through siblings (nodes sharing at least one parent)
                        // If no parent, cycle through root nodes (no prerequisites)
                        List<SkillNodeSO> siblings;

                        if (current?.prerequisites != null && current.prerequisites.Any(p => p?.node != null))
                        {
                            // Siblings = nodes that share at least one prerequisite with current
                            var parentIds = new HashSet<SkillNodeSO>(
                                current.prerequisites.Where(p => p?.node != null).Select(p => p.node));
                            siblings = nodes.Where(n => n != null && n != current &&
                                n.prerequisites != null &&
                                n.prerequisites.Any(p => p?.node != null && parentIds.Contains(p.node)))
                                .ToList();
                        }
                        else
                        {
                            // Root nodes (no prerequisites)
                            siblings = nodes.Where(n =>
                                n.prerequisites == null || !n.prerequisites.Any(p => p?.node != null))
                                .ToList();
                        }

                        if (siblings.Count > 0)
                        {
                            int idx = current != null ? siblings.IndexOf(current) : -1;
                            if (e.keyCode == KeyCode.RightArrow)
                                next = siblings[(idx + 1) % siblings.Count];
                            else
                                next = siblings[(idx - 1 + siblings.Count) % siblings.Count];
                        }
                    }

                    if (next != null && next != current)
                    {
                        _selection.Clear();
                        _selected = next;

                        // Pan canvas to keep newly selected node in view
                        var canvasSz = new Vector2(position.width - _leftW - DIV_W, position.height - TOOLBAR_H);
                        var nr = NodeRect(next);
                        // Only pan if node is outside the canvas or close to the edge
                        float margin = 80f;
                        Rect safeArea = new Rect(margin, margin, canvasSz.x - margin * 2, canvasSz.y - margin * 2);
                        if (!safeArea.Contains(nr.center))
                            _offset = canvasSz * 0.5f - new Vector2(nr.center.x - _offset.x, nr.center.y - _offset.y);

                        // Update inspector position if open
                        if (_inspectorOpen) _inspectorCanvasPos = next.graphPosition;

                        e.Use(); Repaint();
                    }
                    else if (e.keyCode == KeyCode.Tab)
                    {
                        // Tab with no selection starts at first node
                        if (next == null && nodes.Count > 0)
                        {
                            _selected = nodes[0];
                            e.Use(); Repaint();
                        }
                    }

                    break;
                }
            }
        }

        private Rect MarqueeRect()
        {
            float x = Mathf.Min(_marqueeStart.x, _marqueeEnd.x);
            float y = Mathf.Min(_marqueeStart.y, _marqueeEnd.y);
            float w = Mathf.Abs(_marqueeEnd.x - _marqueeStart.x);
            float h = Mathf.Abs(_marqueeEnd.y - _marqueeStart.y);
            return new Rect(x, y, w, h);
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
            // One column per branch in tree order; unassigned/unlisted branches go last.
            var rows = new Dictionary<int, float>();
            foreach (var n in _tree.allNodes)
            {
                if (n == null) continue;
                Undo.RecordObject(n, "Auto Layout");
                int col = BranchColumn(n.branch);
                if (!rows.TryGetValue(col, out float y)) y = 60f;
                n.graphPosition = new Vector2(col * (NODE_W + NODE_SPACING_X) + 40f, y);
                rows[col] = y + NODE_H + NODE_SPACING_Y;
                EditorUtility.SetDirty(n);
            }
            AssetDatabase.SaveAssets(); CenterView();
        }

        // ── Align / Distribute (multi-selection) ─────────────────────────────────

        private void ShowAlignMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Align Left Edges"),    false, () => AlignSelected(AlignMode.Left));
            menu.AddItem(new GUIContent("Align Right Edges"),   false, () => AlignSelected(AlignMode.Right));
            menu.AddItem(new GUIContent("Align Top Edges"),     false, () => AlignSelected(AlignMode.Top));
            menu.AddItem(new GUIContent("Align Bottom Edges"),  false, () => AlignSelected(AlignMode.Bottom));
            menu.AddItem(new GUIContent("Align Horizontal Centers"), false, () => AlignSelected(AlignMode.CenterX));
            menu.AddItem(new GUIContent("Align Vertical Centers"),   false, () => AlignSelected(AlignMode.CenterY));
            menu.AddSeparator("");
            bool enoughForDistribute = _selection.Count >= 3;
            if (enoughForDistribute)
            {
                menu.AddItem(new GUIContent("Distribute Horizontally"), false, () => DistributeSelected(horizontal: true));
                menu.AddItem(new GUIContent("Distribute Vertically"),   false, () => DistributeSelected(horizontal: false));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Distribute Horizontally (needs 3+ nodes)"));
                menu.AddDisabledItem(new GUIContent("Distribute Vertically (needs 3+ nodes)"));
            }
            menu.ShowAsContext();
        }

        private enum AlignMode { Left, Right, Top, Bottom, CenterX, CenterY }

        /// <summary>
        /// Aligns every currently-selected node to the relevant edge/center of the
        /// selection's own bounding box. All nodes share the same fixed size
        /// (NODE_W × NODE_H), so "edges" reduce to a single shared coordinate.
        /// </summary>
        private void AlignSelected(AlignMode mode)
        {
            var nodes = _selection.Where(n => n != null).ToList();
            if (nodes.Count < 2) return;

            Undo.RecordObjects(nodes.Cast<UnityEngine.Object>().ToArray(), "Align Nodes");

            float minX = nodes.Min(n => n.graphPosition.x);
            float maxX = nodes.Max(n => n.graphPosition.x);
            float minY = nodes.Min(n => n.graphPosition.y);
            float maxY = nodes.Max(n => n.graphPosition.y);
            float avgX = nodes.Average(n => n.graphPosition.x);
            float avgY = nodes.Average(n => n.graphPosition.y);

            foreach (var n in nodes)
            {
                Vector2 p = n.graphPosition;
                switch (mode)
                {
                    case AlignMode.Left:    p.x = minX; break;
                    case AlignMode.Right:   p.x = maxX; break;
                    case AlignMode.Top:     p.y = minY; break;
                    case AlignMode.Bottom:  p.y = maxY; break;
                    case AlignMode.CenterX: p.x = avgX; break;
                    case AlignMode.CenterY: p.y = avgY; break;
                }
                n.graphPosition = SnapToGrid(p);
                EditorUtility.SetDirty(n);
            }

            AssetDatabase.SaveAssets();
            Repaint();
        }

        /// <summary>
        /// Spaces the selected nodes evenly between the leftmost/rightmost (or
        /// topmost/bottommost) node in the selection, ordered along that axis.
        /// The two end nodes don't move — only the ones between them redistribute.
        /// </summary>
        private void DistributeSelected(bool horizontal)
        {
            var nodes = _selection.Where(n => n != null).ToList();
            if (nodes.Count < 3) return;

            Undo.RecordObjects(nodes.Cast<UnityEngine.Object>().ToArray(), "Distribute Nodes");

            var ordered = horizontal
                ? nodes.OrderBy(n => n.graphPosition.x).ToList()
                : nodes.OrderBy(n => n.graphPosition.y).ToList();

            float start = horizontal ? ordered.First().graphPosition.x : ordered.First().graphPosition.y;
            float end   = horizontal ? ordered.Last().graphPosition.x  : ordered.Last().graphPosition.y;
            float step  = (end - start) / (ordered.Count - 1);

            for (int i = 0; i < ordered.Count; i++)
            {
                var n = ordered[i];
                Vector2 p = n.graphPosition;
                float coord = start + step * i;
                if (horizontal) p.x = coord; else p.y = coord;
                n.graphPosition = SnapToGrid(p);
                EditorUtility.SetDirty(n);
            }

            AssetDatabase.SaveAssets();
            Repaint();
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

        private Vector2 AutoPosition(SkillBranchSO branch)
        {
            float colX = BranchColumn(branch) * (NODE_W + NODE_SPACING_X) + 40f;
            float maxY = 60f;
            if (_tree.allNodes != null)
                foreach (var n in _tree.allNodes)
                    if (n != null && n.branch == branch)
                        maxY = Mathf.Max(maxY, n.graphPosition.y + NODE_H + NODE_SPACING_Y);
            return new Vector2(colX, maxY);
        }

        private Rect NodeRect(SkillNodeSO n)
        {
            var c = W2C(n.graphPosition);
            return new Rect(c.x, c.y, NODE_W * _zoom, NODE_H * _zoom);
        }

        /// <summary>Returns the canvas-clip rect of the label drawn below a node.</summary>
        private Rect NodeLabelRect(SkillNodeSO n)
        {
            Rect r     = NodeRect(n);
            float labelW = Mathf.Max(r.width * 2f, 90f * _zoom);
            return new Rect(r.center.x - labelW * 0.5f, r.yMax + 3f * _zoom, labelW, 18f * _zoom);
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
        private Vector2 SnapToGrid(Vector2 w) => _snapToGrid
            ? new Vector2(Mathf.Round(w.x / GRID_SIZE) * GRID_SIZE,
                          Mathf.Round(w.y / GRID_SIZE) * GRID_SIZE)
            : w;

        private static Color  BranchColor(SkillBranchSO b) => b != null ? b.color : ColNoBranch;
        private static string BranchName (SkillBranchSO b) => b != null ? b.DisplayName : "(no branch)";

        /// <summary>Column index for layout: position in tree.branches, unlisted/null go after.</summary>
        private int BranchColumn(SkillBranchSO b)
        {
            int count = _tree?.branches?.Count ?? 0;
            int idx   = _tree != null ? _tree.IndexOfBranch(b) : -1;
            return idx >= 0 ? idx : count;
        }

        /// <summary>
        /// Popup listing the tree's branches. With allowNone=false a null value is
        /// coerced to the first branch (used by the creation fields). A branch that
        /// isn't in the tree's list is still shown, flagged, so it doesn't get lost.
        /// </summary>
        private SkillBranchSO BranchPopup(string label, SkillBranchSO current, bool allowNone = true)
        {
            var list = _tree?.branches?.Where(b => b != null).ToList() ?? new List<SkillBranchSO>();

            if (list.Count == 0)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(label);
                EditorGUILayout.LabelField("No branches — add some in Tree Settings", EditorStyles.miniLabel);
                GUILayout.EndHorizontal();
                return current;
            }

            if (!allowNone && current == null) current = list[0];

            var options = new List<SkillBranchSO>();
            var labels  = new List<string>();
            if (allowNone) { options.Add(null); labels.Add("(none)"); }
            foreach (var b in list) { options.Add(b); labels.Add(b.DisplayName); }
            if (current != null && !list.Contains(current))
            { options.Add(current); labels.Add($"{current.DisplayName}  ⚠ not in tree"); }

            int sel    = Mathf.Max(0, options.IndexOf(current));
            int newSel = EditorGUILayout.Popup(label, sel, labels.ToArray());
            return options[newSel];
        }

        /// <summary>Resolves a branch from CSV/JSON text: tree list first, then any project asset.</summary>
        private SkillBranchSO FindBranch(string idOrName)
        {
            if (string.IsNullOrWhiteSpace(idOrName)) return null;
            var inTree = _tree?.GetBranch(idOrName);
            if (inTree != null) return inTree;
            foreach (var guid in AssetDatabase.FindAssets("t:SkillBranchSO"))
            {
                var b = AssetDatabase.LoadAssetAtPath<SkillBranchSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (b != null && (string.Equals(b.branchId, idOrName.Trim(), StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(b.DisplayName, idOrName.Trim(), StringComparison.OrdinalIgnoreCase)))
                    return b;
            }
            return null;
        }

        // ── Branches section (Tree Settings tab) ──────────────────────────────────
        private string        _newBranchName = "";

        /// <summary>
        /// Swaps tree.branches[index] for another asset, optionally moving the old
        /// branch's nodes (and templates) onto the new one.
        /// </summary>
        private void ReplaceBranch(int index, SkillBranchSO newBranch)
        {
            var oldBranch = _tree.branches[index];

            int existing = _tree.branches.IndexOf(newBranch);
            if (existing >= 0 && existing != index)
            {
                EditorUtility.DisplayDialog("Replace Branch",
                    $"'{newBranch.DisplayName}' is already in this tree (row {existing + 1}).", "OK");
                return;
            }

            var users = oldBranch == null ? new List<SkillNodeSO>()
                : _tree.allNodes?.Where(n => n != null && n.branch == oldBranch).ToList() ?? new List<SkillNodeSO>();

            bool moveNodes = false;
            if (users.Count > 0)
            {
                int choice = EditorUtility.DisplayDialogComplex("Replace Branch",
                    $"{users.Count} node(s) use '{BranchName(oldBranch)}'.\n\n" +
                    $"Move them to '{newBranch.DisplayName}' as well?",
                    "Move nodes", "Cancel", "Only replace in list");
                if (choice == 1) return;          // Cancel
                moveNodes = choice == 0;
            }

            Undo.SetCurrentGroupName("Replace Branch");
            Undo.RecordObject(_tree, "Replace Branch");
            _tree.branches[index] = newBranch;
            Dirty(_tree);

            if (moveNodes)
            {
                foreach (var n in users)
                {
                    Undo.RecordObject(n, "Replace Branch");
                    n.branch = newBranch;
                    Dirty(n);
                }
            }

            if (_newBranch == oldBranch)         _newBranch         = newBranch;
            if (_bulkBranch == oldBranch)        _bulkBranch        = newBranch;
            if (_quickCreateBranch == oldBranch) _quickCreateBranch = newBranch;

            AssetDatabase.SaveAssets();
            Repaint();
        }

        private void DrawBranchesSection()
        {
            GUILayout.Space(8); HLine();
            _tree.branches ??= new List<SkillBranchSO>();
            Section($"BRANCHES  ({_tree.branches.Count})");

            // Legacy data left over from the old SkillBranch enum
            if (SkillBranchMigration.TreeNeedsMigration(_tree))
            {
                EditorGUILayout.HelpBox("Some nodes still use the old hard-coded SkillBranch enum. " +
                    "Migrate to create matching SkillBranchSO assets and reassign them.", MessageType.Warning);
                if (GUILayout.Button("Migrate Legacy Branches"))
                {
                    SkillBranchMigration.MigrateTree(_tree, _branchFolder);
                    Repaint();
                }
            }

            int removeAt = -1, moveUp = -1, moveDown = -1;
            int replaceAt = -1; SkillBranchSO replaceWith = null;
            for (int i = 0; i < _tree.branches.Count; i++)
            {
                var b = _tree.branches[i];
                GUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.BeginHorizontal();

                if (b == null)
                {
                    GUILayout.Label("(missing branch — drop one below)", EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(20))) removeAt = i;
                    GUILayout.EndHorizontal();
                }
                else
                {

                    EditorGUI.BeginChangeCheck();
                    var newCol  = EditorGUILayout.ColorField(GUIContent.none, b.color, false, false, false,
                                      GUILayout.Width(36), GUILayout.Height(18));
                    var newName = EditorGUILayout.TextField(b.DisplayName, GUILayout.ExpandWidth(true));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(b, "Edit Branch");
                        b.color = newCol;
                        b.displayName = newName;
                        Dirty(b);
                    }

                    int used = _tree.allNodes?.Count(n => n != null && n.branch == b) ?? 0;
                    GUILayout.Label($"{used}", EditorStyles.miniLabel, GUILayout.Width(22));

                    GUI.enabled = i > 0;
                    if (GUILayout.Button("▲", EditorStyles.miniButtonLeft,  GUILayout.Width(20))) moveUp = i;
                    GUI.enabled = i < _tree.branches.Count - 1;
                    if (GUILayout.Button("▼", EditorStyles.miniButtonMid,   GUILayout.Width(20))) moveDown = i;
                    GUI.enabled = true;
                    if (GUILayout.Button("◎", EditorStyles.miniButtonMid,   GUILayout.Width(20))) EditorGUIUtility.PingObject(b);
                    if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(20))) removeAt = i;

                    GUILayout.EndHorizontal();
                }

                // Asset slot — drop or pick a different SkillBranchSO to replace this entry.
                var picked = (SkillBranchSO)EditorGUILayout.ObjectField(b, typeof(SkillBranchSO), false);
                if (picked != b && picked != null) { replaceAt = i; replaceWith = picked; }

                GUILayout.EndVertical();
            }

            // Deferred: showing a dialog mid-OnGUI breaks the GUILayout pass.
            if (replaceAt >= 0)
            {
                int idx = replaceAt; var with = replaceWith;
                EditorApplication.delayCall += () => { if (this != null && _tree != null && idx < _tree.branches.Count) ReplaceBranch(idx, with); };
            }

            if (moveUp > 0 || moveDown >= 0 || removeAt >= 0)
            {
                Undo.RecordObject(_tree, "Edit Tree Branches");
                if (moveUp > 0)
                    (_tree.branches[moveUp - 1], _tree.branches[moveUp]) = (_tree.branches[moveUp], _tree.branches[moveUp - 1]);
                else if (moveDown >= 0)
                    (_tree.branches[moveDown + 1], _tree.branches[moveDown]) = (_tree.branches[moveDown], _tree.branches[moveDown + 1]);
                else if (removeAt >= 0)
                {
                    var rb = _tree.branches[removeAt];
                    int used = _tree.allNodes?.Count(n => n != null && n.branch == rb) ?? 0;
                    if (used == 0 || EditorUtility.DisplayDialog("Remove Branch",
                            $"{used} node(s) still use '{BranchName(rb)}'. They keep the reference but the " +
                            "branch will have no runtime tab. (The asset itself is not deleted.)", "Remove", "Cancel"))
                        _tree.branches.RemoveAt(removeAt);
                }
                Dirty(_tree);
            }

            // Add existing — adds as soon as an asset is dropped/picked.
            GUILayout.BeginHorizontal();
            GUILayout.Label("Add", EditorStyles.miniLabel, GUILayout.Width(28));
            var toAdd = (SkillBranchSO)EditorGUILayout.ObjectField(null, typeof(SkillBranchSO), false);
            GUILayout.EndHorizontal();
            if (toAdd != null)
            {
                if (_tree.branches.Contains(toAdd))
                    ShowNotification(new GUIContent($"'{toAdd.DisplayName}' is already in this tree"));
                else
                {
                    Undo.RecordObject(_tree, "Add Branch");
                    _tree.branches.Add(toAdd);
                    Dirty(_tree);
                }
            }

            // Create new
            GUILayout.BeginHorizontal();
            _newBranchName = EditorGUILayout.TextField(_newBranchName);
            GUI.enabled = !string.IsNullOrWhiteSpace(_newBranchName);
            if (GUILayout.Button("+ New Branch", EditorStyles.miniButton, GUILayout.Width(90)))
            {
                string display = _newBranchName.Trim();
                string id      = Slugify(display);
                var b = CreateAsset<SkillBranchSO>(_branchFolder, "Branch_" + id + ".asset");
                if (b != null)
                {
                    b.branchId    = id;
                    b.displayName = display;
                    b.color       = Color.HSVToRGB(UnityEngine.Random.value, 0.65f, 0.8f);
                    Dirty(b);
                    Undo.RecordObject(_tree, "Add Branch");
                    _tree.branches.Add(b);
                    Dirty(_tree);
                    _newBranchName = "";
                }
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            _branchFolder = Field("Branch Folder", _branchFolder);
        }

        // =========================================================================
        //  Copy / Paste
        // =========================================================================
        private void CopySelection()
        {
            _clipboard.Clear();

            // Gather what to copy: multi-select group, or just _selected
            var sources = _selection.Count > 0
                ? _selection.Where(n => n != null).ToList()
                : (_selected != null ? new List<SkillNodeSO> { _selected } : null);

            if (sources == null || sources.Count == 0) return;

            _clipboard.AddRange(sources);

            // Compute centroid so paste lands relative to the copied group's centre
            _clipboardCentroid = Vector2.zero;
            foreach (var n in _clipboard) _clipboardCentroid += n.graphPosition;
            _clipboardCentroid /= _clipboard.Count;

            // Brief flash in toolbar — repaint will show the 📋 count
            Repaint();
        }

        private void PasteClipboard()
        {
            if (_clipboard.Count == 0 || _tree == null) return;

            // Paste offset: centre the pasted group at the canvas centre
            var canvasSize = new Vector2(position.width - _leftW - DIV_W, position.height - TOOLBAR_H);
            Vector2 canvasCentre = C2W(canvasSize * 0.5f);
            Vector2 pasteOffset  = SnapToGrid(canvasCentre - _clipboardCentroid);

            // Build a map from original → new node so we can rewire internal connections
            var map = new Dictionary<SkillNodeSO, SkillNodeSO>();

            Undo.RecordObject(_tree, _clipboard.Count == 1 ? "Paste Node" : "Paste Nodes");

            foreach (var src in _clipboard)
            {
                string newId   = UniqueId(src.nodeId + "_copy");
                string newName = src.displayName + " (copy)";

                var dst = CreateAsset<SkillNodeSO>(_newFolder, newId + ".asset");
                if (dst == null) continue;

                Undo.RegisterCreatedObjectUndo(dst, "Paste Node");

                // Copy all fields
                dst.nodeId        = newId;
                dst.displayName   = newName;
                dst.branch        = src.branch;
                dst.icon          = src.icon;
                dst.description   = src.description;
                dst.editorComment = src.editorComment;
                dst.maxRanks      = src.maxRanks;
                dst.graphPosition = SnapToGrid(src.graphPosition + pasteOffset);

                dst.useGlobalVisibilityRules = src.useGlobalVisibilityRules;
                dst.revealBoxRank            = src.revealBoxRank;
                dst.revealInfoRank           = src.revealInfoRank;
                dst.unlockRank               = src.unlockRank;

                // Deep-copy costs
                dst.costsPerRank = src.costsPerRank?
                    .Select(c => new ResourceCost { resource = c.resource, amountPerRank = c.amountPerRank })
                    .ToList() ?? new List<ResourceCost>();

                // Deep-copy effects (they are plain C# objects, not assets)
                dst.effects = src.effects?
                    .Where(e => e != null)
                    .Select(e => e switch
                    {
                        StatMultiplierEffect sme => (SkillEffect)new StatMultiplierEffect
                            { statId = sme.statId, displayName = sme.displayName, multiplier = sme.multiplier },
                        StatFlatBonusEffect sfb => new StatFlatBonusEffect
                            { statId = sfb.statId, displayName = sfb.displayName, bonus = sfb.bonus },
                        _ => null
                    })
                    .Where(e => e != null)
                    .ToList() ?? new List<SkillEffect>();

                // Prerequisites copied as-is for now; rewired below for intra-group links
                dst.prerequisites = src.prerequisites?
                    .Where(p => p?.node != null)
                    .Select(p => new PrerequisiteEntry { node = p.node, requiredRank = p.requiredRank })
                    .ToList() ?? new List<PrerequisiteEntry>();

                Dirty(dst);
                _tree.allNodes.Add(dst);
                map[src] = dst;
            }

            // Rewire intra-group prerequisites to point at the new copies
            foreach (var kvp in map)
            {
                var dst = kvp.Value;
                if (dst.prerequisites == null) continue;
                for (int i = 0; i < dst.prerequisites.Count; i++)
                {
                    var p = dst.prerequisites[i];
                    if (p?.node != null && map.TryGetValue(p.node, out var mapped))
                        p.node = mapped;
                }
            }

            Dirty(_tree);
            AssetDatabase.SaveAssets();

            // Select the newly pasted nodes
            _selection.Clear();
            foreach (var n in map.Values) _selection.Add(n);
            _selected      = map.Values.LastOrDefault();
            _inspectorOpen = false;

            // Update centroid so next paste is offset from the new positions
            _clipboardCentroid = Vector2.zero;
            foreach (var n in _clipboard) _clipboardCentroid += n.graphPosition;
            _clipboardCentroid /= _clipboard.Count;
            _clipboardCentroid += pasteOffset; // successive Ctrl+V keeps moving

            Repaint();
        }

        // =========================================================================
        //  Export (JSON / CSV)
        // =========================================================================
        private void ShowExportMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Export as JSON…"),  false, ExportJson);
            menu.AddItem(new GUIContent("Export as CSV…"),   false, ExportCsv);
            menu.AddItem(new GUIContent("Export both…"),     false, () => { ExportJson(); ExportCsv(); });
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Import from CSV…"), false, ImportCsv);
            menu.ShowAsContext();
        }

        // ── JSON ──────────────────────────────────────────────────────────────────
        private void ExportJson()
        {
            if (_tree == null) return;
            string path = EditorUtility.SaveFilePanel(
                "Export Skill Tree as JSON",
                System.IO.Path.Combine(Application.dataPath, ".."),
                _tree.treeName.Replace(" ", "_") + "_export",
                "json");
            if (string.IsNullOrEmpty(path)) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"treeName\": {JsonStr(_tree.treeName)},");
            sb.AppendLine($"  \"exportedAt\": {JsonStr(System.DateTime.Now.ToString("u"))},");
            sb.AppendLine($"  \"nodeCount\": {_tree.allNodes?.Count ?? 0},");
            sb.AppendLine("  \"nodes\": [");

            var nodes = _tree.allNodes?.Where(n => n != null).ToList() ?? new List<SkillNodeSO>();
            for (int i = 0; i < nodes.Count; i++)
            {
                var n   = nodes[i];
                bool last = i == nodes.Count - 1;
                sb.AppendLine("    {");
                sb.AppendLine($"      \"nodeId\":      {JsonStr(n.nodeId)},");
                sb.AppendLine($"      \"displayName\": {JsonStr(n.displayName)},");
                sb.AppendLine($"      \"branch\":      {JsonStr(n.branch != null ? n.branch.branchId : "")},");
                sb.AppendLine($"      \"maxRanks\":    {n.maxRanks},");
                sb.AppendLine($"      \"description\": {JsonStr(n.description)},");
                sb.AppendLine($"      \"graphX\":      {n.graphPosition.x},");
                sb.AppendLine($"      \"graphY\":      {n.graphPosition.y},");

                // Prerequisites
                sb.Append("      \"prerequisites\": [");
                if (n.prerequisites != null && n.prerequisites.Count > 0)
                {
                    sb.AppendLine();
                    for (int pi = 0; pi < n.prerequisites.Count; pi++)
                    {
                        var p = n.prerequisites[pi];
                        if (p?.node == null) continue;
                        bool lastP = pi == n.prerequisites.Count - 1;
                        sb.AppendLine($"        {{ \"nodeId\": {JsonStr(p.node.nodeId)}, \"requiredRank\": {p.requiredRank} }}{(lastP ? "" : ",")}");
                    }
                    sb.Append("      ");
                }
                sb.AppendLine("],");

                // Costs
                sb.Append("      \"costs\": [");
                if (n.costsPerRank != null && n.costsPerRank.Count > 0)
                {
                    sb.AppendLine();
                    for (int ci = 0; ci < n.costsPerRank.Count; ci++)
                    {
                        var c = n.costsPerRank[ci];
                        if (c?.resource == null) continue;
                        bool lastC = ci == n.costsPerRank.Count - 1;
                        sb.AppendLine($"        {{ \"resourceId\": {JsonStr(c.resource.resourceId)}, \"suffix\": {JsonStr(c.resource.shortSuffix)}, \"amountPerRank\": {c.amountPerRank} }}{(lastC ? "" : ",")}");
                    }
                    sb.Append("      ");
                }
                sb.AppendLine("],");

                // Effects
                sb.Append("      \"effects\": [");
                if (n.effects != null && n.effects.Count > 0)
                {
                    sb.AppendLine();
                    for (int ei = 0; ei < n.effects.Count; ei++)
                    {
                        var eff = n.effects[ei];
                        if (eff == null) continue;
                        bool lastE = ei == n.effects.Count - 1;
                        sb.AppendLine($"        {{ \"type\": {JsonStr(eff.GetType().Name)}, \"description\": {JsonStr(eff.GetDescription())} }}{(lastE ? "" : ",")}");
                    }
                    sb.Append("      ");
                }
                sb.AppendLine("]");

                sb.AppendLine(last ? "    }" : "    },");
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");

            System.IO.File.WriteAllText(path, sb.ToString());
            AssetDatabase.Refresh();
            Debug.Log($"[SkillTreeEditor] Exported JSON → {path}");
            EditorUtility.RevealInFinder(path);
        }

        // ── CSV ───────────────────────────────────────────────────────────────────
        private void ExportCsv()
        {
            if (_tree == null) return;
            string path = EditorUtility.SaveFilePanel(
                "Export Skill Tree as CSV",
                System.IO.Path.Combine(Application.dataPath, ".."),
                _tree.treeName.Replace(" ", "_") + "_export",
                "csv");
            if (string.IsNullOrEmpty(path)) return;

            var sb = new System.Text.StringBuilder();

            // Header row
            sb.AppendLine("nodeId,displayName,branch,maxRanks,prerequisites,costs_rank1,costs_rank2,costs_rank3,effectCount,effects,description,graphX,graphY");

            var nodes = _tree.allNodes?.Where(n => n != null).ToList() ?? new List<SkillNodeSO>();
            foreach (var n in nodes)
            {
                // Prerequisites: "nodeId(req1)|nodeId(req2)"
                string prereqs = "";
                if (n.prerequisites != null)
                    prereqs = string.Join("|", n.prerequisites
                        .Where(p => p?.node != null)
                        .Select(p => p.requiredRank > 1 ? $"{p.node.nodeId}(≥{p.requiredRank})" : p.node.nodeId));

                // Costs per rank 1/2/3
                string CostAtRank(int rank) => n.costsPerRank != null && n.costsPerRank.Count > 0
                    ? string.Join("+", n.costsPerRank
                        .Where(c => c?.resource != null)
                        .Select(c => $"{c.GetAmount(rank)}{c.resource.shortSuffix}"))
                    : "free";

                // Effects: "StatMultiplierEffect(+20%)|..."
                string effects = "";
                if (n.effects != null)
                    effects = string.Join("|", n.effects
                        .Where(e => e != null)
                        .Select(e => e.GetDescription()));

                sb.AppendLine(string.Join(",",
                    CsvCell(n.nodeId),
                    CsvCell(n.displayName),
                    CsvCell(n.branch != null ? n.branch.branchId : ""),
                    n.maxRanks.ToString(),
                    CsvCell(prereqs),
                    CsvCell(CostAtRank(1)),
                    CsvCell(CostAtRank(2)),
                    CsvCell(CostAtRank(3)),
                    (n.effects?.Count ?? 0).ToString(),
                    CsvCell(effects),
                    CsvCell(n.description),
                    n.graphPosition.x.ToString("F0"),
                    n.graphPosition.y.ToString("F0")));
            }

            System.IO.File.WriteAllText(path, sb.ToString());
            AssetDatabase.Refresh();
            Debug.Log($"[SkillTreeEditor] Exported CSV → {path}");
            EditorUtility.RevealInFinder(path);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────
        private static string JsonStr(string s)
            => s == null ? "null" : $"\"{s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n")}\"";

        private static string CsvCell(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            // Wrap in quotes if contains comma, quote, or newline
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
                return $"\"{s.Replace("\"", "\"\"")}\"";
            return s;
        }

        // =========================================================================
        //  CSV Import
        // =========================================================================
        private void ImportCsv()
        {
            if (_tree == null)
            {
                EditorUtility.DisplayDialog("Import CSV", "Please load a SkillTreeSO first.", "OK");
                return;
            }

            string path = EditorUtility.OpenFilePanel("Import Skill Tree CSV", "", "csv");
            if (string.IsNullOrEmpty(path)) return;

            string[] lines;
            try
            {
                // Use StreamReader with BOM auto-detection — handles UTF-8, UTF-16, and all Excel encodings
                using var reader = new System.IO.StreamReader(path, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                string raw = reader.ReadToEnd();
                // Belt-and-suspenders: strip any remaining BOM character at position 0
                if (raw.Length > 0 && raw[0] == '\uFEFF') raw = raw[1..];
                lines = raw.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.None);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Import CSV", $"Failed to read file:\n{ex.Message}", "OK");
                return;
            }

            if (lines.Length < 2)
            {
                EditorUtility.DisplayDialog("Import CSV", "File is empty or contains only a header row.", "OK");
                return;
            }

            // ── Parse header ──────────────────────────────────────────────────────
            var header = SplitCsvRow(lines[0]);
            var colIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < header.Length; i++)
            {
                // Strip only genuine invisible/zero-width characters — NOT printable ones
                // \uFEFF=BOM, \u200B=zero-width space, \u200C/D/E/F=zero-width joiners
                string h = header[i].Trim().TrimStart('\uFEFF', '\u200B', '\u200C', '\u200D', '\u200E', '\u200F', '\u00AD');
                h = h.TrimEnd('\uFEFF', '\u200B', '\u200C', '\u200D', '\u200E', '\u200F', '\u00AD').Trim();
                if (!string.IsNullOrEmpty(h))
                    colIdx[h] = i;
            }

            // ── Pre-load all ResourceDefinitionSO assets keyed by shortSuffix ────
            var resourceBySuffix = new Dictionary<string, ResourceDefinitionSO>(StringComparer.OrdinalIgnoreCase);
            var resourceGuids    = AssetDatabase.FindAssets("t:ResourceDefinitionSO");
            foreach (var guid in resourceGuids)
            {
                var res = AssetDatabase.LoadAssetAtPath<ResourceDefinitionSO>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (res != null && !string.IsNullOrEmpty(res.shortSuffix))
                    resourceBySuffix.TryAdd(res.shortSuffix, res);
            }

            // Validate required columns
            string[] required = { "nodeId", "displayName", "branch", "maxRanks" };
            foreach (var req in required)
                if (!colIdx.ContainsKey(req))
                {
                    EditorUtility.DisplayDialog("Import Failed",
                        $"Missing required column: '{req}'\n\n" +
                        $"Columns found: {string.Join(", ", colIdx.Keys.Select(k => $"'{k}'"))}\n\n" +
                        $"Required: {string.Join(", ", required)}", "OK");
                    return;
                }

            // Build nodeId → node lookup (trim for safety)
            _tree.allNodes ??= new List<SkillNodeSO>();
            var existingIds = new HashSet<string>(
                _tree.allNodes.Where(n => n != null && !string.IsNullOrEmpty(n.nodeId))
                              .Select(n => n.nodeId), StringComparer.Ordinal);

            // Also build a display-name fallback lookup
            var nameLookup = new Dictionary<string, SkillNodeSO>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in _tree.allNodes)
                if (n != null && !string.IsNullOrEmpty(n.displayName))
                    nameLookup.TryAdd(n.displayName.Trim(), n);

            var nodeLookup = new Dictionary<string, SkillNodeSO>(StringComparer.Ordinal);
            foreach (var n in _tree.allNodes)
                if (n != null && !string.IsNullOrEmpty(n.nodeId))
                    nodeLookup[n.nodeId.Trim()] = n;

            // ── Pass 1: create or update nodes ────────────────────────────────────
            var importedNodes = new Dictionary<string, SkillNodeSO>(StringComparer.Ordinal);
            var rawPrereqs    = new Dictionary<string, string>(StringComparer.Ordinal);

            int created = 0, updated = 0, skipped = 0, warnings = 0;
            var warnLog = new System.Text.StringBuilder();

            Undo.RecordObject(_tree, "Import Nodes from CSV");

            string GetCol(string[] cols, string col) =>
                colIdx.TryGetValue(col, out int ci) && ci < cols.Length ? cols[ci].Trim() : "";

            for (int li = 1; li < lines.Length; li++)
            {
                string line = lines[li].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var    cols   = SplitCsvRow(line);
                string nodeId = GetCol(cols, "nodeId");
                if (string.IsNullOrEmpty(nodeId)) { skipped++; continue; }

                string prereqRaw  = GetCol(cols, "prerequisites");
                string displayName = GetCol(cols, "displayName");
                string branchStr  = GetCol(cols, "branch");
                string ranksStr   = GetCol(cols, "maxRanks");
                string desc       = GetCol(cols, "description");
                string costRank1  = GetCol(cols, "costs_rank1");

                float graphX = 0f, graphY = 0f;
                float.TryParse(GetCol(cols, "graphX"), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out graphX);
                float.TryParse(GetCol(cols, "graphY"), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out graphY);

                // ── Match existing node ───────────────────────────────────────────
                if (nodeLookup.TryGetValue(nodeId, out var existingNode))
                {
                    // Update existing node
                    Undo.RecordObject(existingNode, "Import CSV");
                    bool changed = false;

                    if (!string.IsNullOrEmpty(displayName) && displayName != existingNode.displayName)
                    { existingNode.displayName = displayName; changed = true; }

                    if (!string.IsNullOrEmpty(branchStr))
                    {
                        var branch = FindBranch(branchStr);
                        if (branch == null)
                        { warnLog.AppendLine($"  • Row {li + 1}: unknown branch '{branchStr}' — kept existing."); warnings++; }
                        else if (branch != existingNode.branch)
                        { existingNode.branch = branch; changed = true; }
                    }

                    if (!string.IsNullOrEmpty(ranksStr) &&
                        int.TryParse(ranksStr, out int maxRanks) &&
                        maxRanks >= 1 && maxRanks <= 20 && maxRanks != existingNode.maxRanks)
                    { existingNode.maxRanks = maxRanks; changed = true; }

                    if (colIdx.ContainsKey("description") && desc != (existingNode.description ?? ""))
                    { existingNode.description = desc; changed = true; }

                    if (changed) { Dirty(existingNode); updated++; warnLog.AppendLine($"  + Updated: {existingNode.displayName} [{nodeId}]"); }

                    rawPrereqs[nodeId] = prereqRaw;
                    importedNodes[nodeId] = existingNode;
                    continue;
                }

                // ── Create new node ───────────────────────────────────────────────
                string safeId = UniqueId(nodeId);
                var    node   = CreateAsset<SkillNodeSO>(_newFolder, safeId + ".asset");
                if (node == null)
                {
                    warnLog.AppendLine($"  • Row {li + 1}: failed to create asset for '{nodeId}'.");
                    warnings++; continue;
                }

                Undo.RegisterCreatedObjectUndo(node, "Import Node");

                var newBranch = FindBranch(branchStr);
                if (newBranch == null && !string.IsNullOrEmpty(branchStr))
                { warnLog.AppendLine($"  • Row {li + 1}: unknown branch '{branchStr}' — left unassigned."); warnings++; }
                int.TryParse(ranksStr, out int newRanks);
                newRanks = Mathf.Max(1, newRanks);

                node.nodeId        = safeId;
                node.displayName   = string.IsNullOrEmpty(displayName) ? safeId : displayName;
                node.branch        = newBranch;
                node.maxRanks      = newRanks;
                node.description   = desc;
                node.graphPosition = new Vector2(graphX, graphY);

                // Parse costs from rank-1 cost string e.g. "100g+5s"
                if (!string.IsNullOrEmpty(costRank1) &&
                    !costRank1.Equals("free", StringComparison.OrdinalIgnoreCase))
                {
                    node.costsPerRank ??= new List<ResourceCost>();
                    foreach (var part in costRank1.Split('+'))
                    {
                        string p = part.Trim();
                        if (string.IsNullOrEmpty(p)) continue;
                        int splitAt = 0;
                        while (splitAt < p.Length && (char.IsDigit(p[splitAt]) || p[splitAt] == '-'))
                            splitAt++;
                        if (splitAt == 0) continue;
                        if (!int.TryParse(p[..splitAt], out int amount)) continue;
                        string suffix = p[splitAt..].Trim();
                        if (string.IsNullOrEmpty(suffix)) continue;
                        if (!resourceBySuffix.TryGetValue(suffix, out var resDef))
                        { warnLog.AppendLine($"  ⚠ No resource with suffix='{suffix}', cost skipped."); warnings++; continue; }
                        node.costsPerRank.Add(new ResourceCost { resource = resDef, amountPerRank = amount });
                    }
                }

                Dirty(node);
                _tree.allNodes.Add(node);
                nodeLookup[safeId]   = node;
                importedNodes[safeId] = node;
                rawPrereqs[safeId]   = prereqRaw;
                existingIds.Add(safeId);
                created++;
            }

            // ── Pass 2: wire prerequisites ────────────────────────────────────────
            foreach (var kvp in importedNodes)
            {
                string      nodeId_ = kvp.Key;
                SkillNodeSO node_   = kvp.Value;
                string      raw     = rawPrereqs.TryGetValue(nodeId_, out var r) ? r : "";
                if (string.IsNullOrEmpty(raw)) continue;

                var newPrereqs = new List<PrerequisiteEntry>();

                if (!raw.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var token in raw.Split('|'))
                    {
                        string t = token.Trim();
                        if (string.IsNullOrEmpty(t)) continue;

                        int reqRank = 1;
                        int paren   = t.IndexOf('(');
                        if (paren >= 0)
                        {
                            string inner = t.Substring(paren + 1).TrimEnd(')').Replace("\u2265", "").Replace(">=", "").Trim();
                            if (int.TryParse(inner, out int rr) && rr >= 1) reqRank = rr;
                            t = t[..paren].Trim();
                        }
                        if (string.IsNullOrEmpty(t)) continue;

                        if (!nodeLookup.TryGetValue(t, out var prereqNode))
                        { warnLog.AppendLine($"  ⚠ Prereq '{t}' for '{nodeId_}' not found."); warnings++; continue; }

                        bool already = newPrereqs.Any(p => p?.node == prereqNode);
                        if (!already)
                            newPrereqs.Add(new PrerequisiteEntry { node = prereqNode, requiredRank = reqRank });
                    }
                }

                // Only update if different
                if (!SamePrereqs(node_.prerequisites, newPrereqs))
                {
                    Undo.RecordObject(node_, "Import CSV");
                    node_.prerequisites = newPrereqs;
                    Dirty(node_);
                }
            }

            Dirty(_tree);
            AssetDatabase.SaveAssets();
            InvalidateCycleCache(); InvalidateOrphanCache(); InvalidateDuplicateCache();
            Repaint();

            var summary = new System.Text.StringBuilder();
            summary.AppendLine("Import complete.");
            summary.AppendLine($"  Created : {created} node(s)");
            summary.AppendLine($"  Updated : {updated} node(s)");
            summary.AppendLine($"  Skipped : {skipped} row(s)");
            if (warnings > 0) { summary.AppendLine($"  Warnings: {warnings}"); summary.AppendLine(); summary.Append(warnLog); }

            Debug.Log($"[SkillTreeEditor] CSV import — {created} created, {updated} updated, {skipped} skipped, {warnings} warnings.\n{warnLog}");
            EditorUtility.DisplayDialog("Import CSV", summary.ToString(), "OK");
        }

        private static bool SamePrereqs(List<PrerequisiteEntry> a, List<PrerequisiteEntry> b)
        {
            int ca = a?.Count ?? 0, cb = b?.Count ?? 0;
            if (ca != cb) return false;
            if (ca == 0) return true;
            for (int i = 0; i < ca; i++)
            {
                if (a[i]?.node != b[i]?.node) return false;
                if ((a[i]?.requiredRank ?? 1) != (b[i]?.requiredRank ?? 1)) return false;
            }
            return true;
        }

        private static string[] SplitCsvRow(string line)
        {
            var fields = new List<string>();
            int i = 0;
            while (i <= line.Length)
            {
                if (i == line.Length) { fields.Add(""); break; }

                if (line[i] == '"')
                {
                    i++;
                    var sb = new System.Text.StringBuilder();
                    while (i < line.Length)
                    {
                        if (line[i] == '"')
                        {
                            if (i + 1 < line.Length && line[i + 1] == '"')
                            { sb.Append('"'); i += 2; }
                            else
                            { i++; break; }
                        }
                        else { sb.Append(line[i++]); }
                    }
                    fields.Add(sb.ToString());
                    if (i < line.Length && line[i] == ',') i++;
                }
                else
                {
                    int start = i;
                    while (i < line.Length && line[i] != ',') i++;
                    fields.Add(line[start..i]);
                    if (i < line.Length) i++;
                }
            }
            return fields.ToArray();
        }


        // =========================================================================
        //  Simulate mode
        // =========================================================================

        private void InitSimBalances()
        {
            _simBalances.Clear();
            if (_tree?.allNodes == null) return;
            // Seed each unique resource with a generous default balance
            foreach (var node in _tree.allNodes)
            {
                if (node?.costsPerRank == null) continue;
                foreach (var cost in node.costsPerRank)
                {
                    if (cost?.resource == null) continue;
                    if (!_simBalances.ContainsKey(cost.resource.resourceId))
                        _simBalances[cost.resource.resourceId] = 1000;
                }
            }
        }

        /// <summary>Returns the simulated NodeVisibilityState without needing a running game.</summary>
        private NodeVisibilityState SimNodeState(SkillNodeSO node)
        {
            if (node == null) return NodeVisibilityState.Hidden;

            int rank = _simState.GetRank(node.nodeId);
            if (rank >= node.maxRanks && node.maxRanks > 0) return NodeVisibilityState.Unlocked;
            if (rank > 0) return NodeVisibilityState.Unlocked;

            // Check prereqs
            bool prereqsMet = _tree.ArePrereqsMet(node, _simState);
            if (!prereqsMet) return NodeVisibilityState.Visible;

            // Check affordability
            return SimCanAfford(node, rank + 1) ? NodeVisibilityState.Unlockable : NodeVisibilityState.Visible;
        }

        private bool SimCanAfford(SkillNodeSO node, int rank)
        {
            if (node.costsPerRank == null) return true;
            foreach (var cost in node.costsPerRank)
            {
                if (cost?.resource == null) continue;
                int required  = cost.GetAmount(rank);
                int available = _simBalances.TryGetValue(cost.resource.resourceId, out int b) ? b : 0;
                if (available < required) return false;
            }
            return true;
        }

        private void SimTryUnlock(SkillNodeSO node)
        {
            if (node == null) return;
            int rank = _simState.GetRank(node.nodeId);
            if (rank >= node.maxRanks) return;

            var state = SimNodeState(node);
            if (state != NodeVisibilityState.Unlockable && state != NodeVisibilityState.Visible) return;
            if (!_tree.ArePrereqsMet(node, _simState)) return;

            int nextRank = rank + 1;
            // Spend resources
            if (node.costsPerRank != null)
                foreach (var cost in node.costsPerRank)
                {
                    if (cost?.resource == null) continue;
                    string id = cost.resource.resourceId;
                    int spent = cost.GetAmount(nextRank);
                    _simBalances[id] = Mathf.Max(0,
                        (_simBalances.TryGetValue(id, out int b) ? b : 0) - spent);
                }

            _simState.nodeRanks[node.nodeId] = nextRank;
            Repaint();
        }

        private void SimTryRefund(SkillNodeSO node)
        {
            if (node == null) return;
            int rank = _simState.GetRank(node.nodeId);
            if (rank <= 0) return;

            // Block refund if another unlocked node needs this at current rank
            foreach (var other in _tree.allNodes)
            {
                if (other == null || other == node || other.prerequisites == null) continue;
                int otherRank = _simState.GetRank(other.nodeId);
                if (otherRank <= 0) continue;
                foreach (var p in other.prerequisites)
                {
                    if (p?.node != node) continue;
                    if (rank - 1 < p.requiredRank) return; // blocked
                }
            }

            // Refund resources
            if (node.costsPerRank != null)
                foreach (var cost in node.costsPerRank)
                {
                    if (cost?.resource == null) continue;
                    string id = cost.resource.resourceId;
                    int refunded = cost.GetAmount(rank);
                    _simBalances[id] = (_simBalances.TryGetValue(id, out int b) ? b : 0) + refunded;
                }

            if (rank <= 1) _simState.nodeRanks.Remove(node.nodeId);
            else           _simState.nodeRanks[node.nodeId] = rank - 1;
            Repaint();
        }

        private void DrawSimPanel(Rect canvasRect)
        {
            const float PW = 200f;
            float ph = Mathf.Min(canvasRect.height - 20f,
                36f + _simBalances.Count * 22f + 60f);
            Rect panel = new Rect(canvasRect.x + 8, canvasRect.yMax - ph - 8, PW, ph);

            // Background
            EditorGUI.DrawRect(new Rect(panel.x-1, panel.y-1, panel.width+2, panel.height+2),
                new Color(0.3f, 0.85f, 0.4f, 0.9f));
            EditorGUI.DrawRect(panel, new Color(0.1f, 0.13f, 0.1f, 0.97f));

            GUILayout.BeginArea(panel);
            GUILayout.Space(5);

            // Header
            GUILayout.BeginHorizontal();
            GUILayout.Label("▶ SIMULATE", new GUIStyle(EditorStyles.boldLabel)
                { normal = { textColor = new Color(0.4f, 1f, 0.5f) }, fontSize = 11 });
            if (GUILayout.Button("Reset", EditorStyles.miniButton, GUILayout.Width(42)))
            {
                _simState    = new SkillTreeRuntimeState();
                InitSimBalances();
                Repaint();
            }
            GUILayout.EndHorizontal();

            HLine();
            GUILayout.Label("Resources", EditorStyles.miniLabel);

            // Editable resource balances
            foreach (var key in _simBalances.Keys.ToList())
            {
                GUILayout.BeginHorizontal();
                // Find resource SO for display name
                string label = key;
                if (_tree?.allNodes != null)
                    foreach (var n in _tree.allNodes)
                        if (n?.costsPerRank != null)
                            foreach (var c in n.costsPerRank)
                                if (c?.resource?.resourceId == key)
                                { label = c.resource.shortSuffix; goto foundLabel; }
                foundLabel:
                GUILayout.Label(label, EditorStyles.miniLabel, GUILayout.Width(28));
                int newVal = EditorGUILayout.IntField(_simBalances[key], GUILayout.Width(60));
                _simBalances[key] = Mathf.Max(0, newVal);
                if (GUILayout.Button("+500", EditorStyles.miniButton, GUILayout.Width(40)))
                    _simBalances[key] += 500;
                GUILayout.EndHorizontal();
            }

            HLine();

            // Unlocked count
            int unlockedCount = _simState.nodeRanks.Count;
            GUILayout.Label($"Unlocked: {unlockedCount} / {_tree?.allNodes?.Count ?? 0}",
                new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = new Color(0.7f, 1f, 0.7f) } });

            GUILayout.Label("Click node to unlock  |  Alt+click to refund",
                new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }, wordWrap = true });

            GUILayout.EndArea();

            // Eat mouse clicks inside panel
            Event e = Event.current;
            if (e.type == EventType.MouseDown && panel.Contains(e.mousePosition))
                e.Use();
        }

        // =========================================================================
        //  Recent trees
        // =========================================================================
        private List<string> GetRecentGuids()
        {
            if (_recentGuids != null) return _recentGuids;
            string raw = EditorPrefs.GetString(RECENT_PREFS_KEY, "");
            _recentGuids = string.IsNullOrEmpty(raw)
                ? new List<string>()
                : raw.Split('|').Where(s => !string.IsNullOrEmpty(s)).ToList();
            return _recentGuids;
        }

        private void PushRecentTree(SkillTreeSO tree)
        {
            if (tree == null) return;
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tree));
            if (string.IsNullOrEmpty(guid)) return;

            var list = GetRecentGuids();
            list.Remove(guid);          // remove if already present
            list.Insert(0, guid);       // put at front
            while (list.Count > RECENT_MAX) list.RemoveAt(list.Count - 1);

            EditorPrefs.SetString(RECENT_PREFS_KEY, string.Join("|", list));
        }

        private void ShowRecentMenu()
        {
            var list = GetRecentGuids();
            var menu = new GenericMenu();

            if (list.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No recent trees"));
            }
            else
            {
                foreach (var guid in list)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path)) continue;
                    string label = System.IO.Path.GetFileNameWithoutExtension(path);
                    var capturedPath = path;
                    bool isCurrent = _tree != null &&
                        AssetDatabase.GetAssetPath(_tree) == path;
                    menu.AddItem(new GUIContent(label), isCurrent, () =>
                    {
                        var loaded = AssetDatabase.LoadAssetAtPath<SkillTreeSO>(capturedPath);
                        if (loaded != null)
                        {
                            _tree = loaded;
                            _selected = null; _inspectorOpen = false;
                            InvalidateCycleCache(); InvalidateOrphanCache(); InvalidateDuplicateCache();
                            CenterView(); Repaint();
                        }
                    });
                }
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Clear history"), false, () =>
                {
                    EditorPrefs.DeleteKey(RECENT_PREFS_KEY);
                    _recentGuids = new List<string>();
                    Repaint();
                });
            }

            menu.ShowAsContext();
        }

        // =========================================================================
        //  Templates
        // =========================================================================
        private List<NodeTemplateSO> GetTemplates()
        {
            if (!_templatesDirty) return _templates;
            _templatesDirty = false;
            _templates.Clear();
            var guids = AssetDatabase.FindAssets("t:NodeTemplateSO");
            foreach (var g in guids)
            {
                var t = AssetDatabase.LoadAssetAtPath<NodeTemplateSO>(
                    AssetDatabase.GUIDToAssetPath(g));
                if (t != null) _templates.Add(t);
            }
            _templates.Sort((a, b) => string.Compare(a.templateName, b.templateName,
                System.StringComparison.OrdinalIgnoreCase));
            return _templates;
        }

        /// <summary>Saves the selected node's settings into a new NodeTemplateSO asset.</summary>
        private void SaveAsTemplate(SkillNodeSO src, string tplName)
        {
            if (src == null || string.IsNullOrWhiteSpace(tplName)) return;

            if (!System.IO.Directory.Exists(_templateFolder))
            { System.IO.Directory.CreateDirectory(_templateFolder); AssetDatabase.Refresh(); }

            string safeName = tplName.Trim().Replace(" ", "_");
            string path = AssetDatabase.GenerateUniqueAssetPath(
                $"{_templateFolder}/{safeName}.asset");

            var tpl = ScriptableObject.CreateInstance<NodeTemplateSO>();
            tpl.templateName = tplName.Trim();
            tpl.branch       = src.branch;
            tpl.icon         = src.icon;
            tpl.maxRanks     = src.maxRanks;

            tpl.costsPerRank = src.costsPerRank?
                .Select(c => new ResourceCost { resource = c.resource, amountPerRank = c.amountPerRank })
                .ToList() ?? new List<ResourceCost>();

            tpl.effects = src.effects?
                .Where(e => e != null)
                .Select(e => e switch
                {
                    StatMultiplierEffect sme => (SkillEffect)new StatMultiplierEffect
                        { statId = sme.statId, displayName = sme.displayName, multiplier = sme.multiplier },
                    StatFlatBonusEffect sfb  => new StatFlatBonusEffect
                        { statId = sfb.statId, displayName = sfb.displayName, bonus = sfb.bonus },
                    _ => null
                })
                .Where(e => e != null)
                .ToList() ?? new List<SkillEffect>();

            tpl.useGlobalVisibilityRules = src.useGlobalVisibilityRules;
            tpl.revealBoxRank            = src.revealBoxRank;
            tpl.revealInfoRank           = src.revealInfoRank;
            tpl.unlockRank               = src.unlockRank;

            AssetDatabase.CreateAsset(tpl, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _templatesDirty = true;
            EditorGUIUtility.PingObject(tpl);
            Debug.Log($"[SkillTreeEditor] Saved template '{tpl.templateName}'");
        }

        /// <summary>
        /// Applies a template's settings to the pending quick-create state,
        /// then opens the quick-create popup so the user can still name the node.
        /// </summary>
        private void StampTemplate(NodeTemplateSO tpl)
        {
            if (tpl == null || _tree == null) return;

            // Place at canvas centre
            var canvasSize = new Vector2(position.width - _leftW - DIV_W, position.height - TOOLBAR_H);
            _quickCreateCanvasPos  = C2W(canvasSize * 0.5f);
            _quickCreateOpen       = true;
            _quickCreateName       = "";
            _quickCreateId         = "";
            _quickCreateIdEdited   = false;
            _quickCreateBranch     = tpl.branch;
            _pendingParent         = null;

            // Store template for QuickCreateNode to pick up
            _stampTemplate = tpl;
            Repaint();
        }

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

        /// <summary>
        /// Records <paramref name="obj"/> into Unity's undo stack with a label,
        /// then marks it dirty. Use this instead of bare Dirty() for any mutation.
        /// </summary>
        private static void Record(UnityEngine.Object obj, string label)
        {
            Undo.RecordObject(obj, label);
            EditorUtility.SetDirty(obj);
        }

        /// <summary>Records multiple objects as one undo step.</summary>
        private static void RecordMany(string label, params UnityEngine.Object[] objs)
        {
            Undo.RecordObjects(objs, label);
            foreach (var o in objs) EditorUtility.SetDirty(o);
        }

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