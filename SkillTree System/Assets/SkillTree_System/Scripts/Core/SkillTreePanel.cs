using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JollyLlama.SkillTreeSystem
{
    public class SkillTreePanel : MonoBehaviour,
        IPointerClickHandler, IBeginDragHandler, IDragHandler
    {
        [Header("Core")]
        [SerializeField] private SkillTreeManager skillTreeManager;

        [Header("Hierarchy")]
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform panRoot;
        [SerializeField] private RectTransform connectionLayer;
        [SerializeField] private RectTransform nodeLayer;

        [Header("Prefabs")]
        [SerializeField] private GameObject nodeButtonPrefab;
        [SerializeField] private GameObject nodeConnectionPrefab;

        [Header("Tabs")]
        [SerializeField] private Button allTabButton;
        [SerializeField] private Button offenseTabButton;
        [SerializeField] private Button controlTabButton;
        [SerializeField] private Button economyTabButton;
        [SerializeField] private Button defenseTabButton;

        [Header("Tooltip")]
        [SerializeField] private SkillTreeTooltip tooltip;

        [Header("HUD — Resource Display")]
        [Tooltip("Optional. Each entry shows one resource balance in the HUD. " +
                 "Assign ResourceDefinitionSO + a TMP label per resource you want displayed.")]
        [SerializeField] private List<ResourceHUDEntry> resourceHUDEntries = new();

        [Header("HUD — Buttons")]
        [SerializeField] private Button unlockButton;
        [SerializeField] private TextMeshProUGUI unlockButtonText;
        [SerializeField] private TextMeshProUGUI feedbackText;

        [Header("Slide Animation")]
        [SerializeField] private float slideDuration  = 0.25f;
        [SerializeField] private float slideDistance  = 0f;

        [Header("Refund")]
        [SerializeField] private Button refundConfirmButton;

        // ── Runtime ───────────────────────────────────────────────────────────────

        private readonly Dictionary<string, SkillNodeButton> _buttonMap  = new();
        private readonly List<SkillNodeButton>               _buttons    = new();
        private readonly List<NodeConnection>                _connections = new();

        private SkillNodeSO  _selectedNode;
        private SkillNodeSO  _pendingRefundNode;
        private SkillBranch? _activeFilter;
        private int          _activeTabIndex;

        private Vector2   _panOffset;
        private Vector2   _dragStartPan;
        private Vector2   _dragStartMouse;
        private Coroutine _slideCoroutine;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (skillTreeManager == null)
            {
                SkillTreeLogger.LogError("SkillTreePanel", "skillTreeManager not assigned!");
                return;
            }

            allTabButton?.onClick.AddListener(() => SetFilterAnimated(null, 0));
            offenseTabButton?.onClick.AddListener(() => SetFilterAnimated(SkillBranch.Offense, 1));
            controlTabButton?.onClick.AddListener(() => SetFilterAnimated(SkillBranch.Control, 2));
            economyTabButton?.onClick.AddListener(() => SetFilterAnimated(SkillBranch.Economy, 3));
            defenseTabButton?.onClick.AddListener(() => SetFilterAnimated(SkillBranch.Defense, 4));

            unlockButton?.onClick.AddListener(OnUnlockClicked);
            refundConfirmButton?.onClick.AddListener(OnRefundConfirmed);
            if (refundConfirmButton != null) refundConfirmButton.gameObject.SetActive(false);

            skillTreeManager.OnNodeUnlocked += OnNodeStateChanged;
            skillTreeManager.OnNodeRefunded += OnNodeStateChanged;
            skillTreeManager.OnTreeLoaded   += OnTreeReloaded;

            // Listen for any resource change so the HUD and node states stay fresh
            ResourceManager.OnResourceChanged += OnResourceChanged;
        }

        private void Start() => gameObject.SetActive(false);

        private void OnDestroy()
        {
            ResourceManager.OnResourceChanged -= OnResourceChanged;
            if (skillTreeManager == null) return;
            skillTreeManager.OnNodeUnlocked -= OnNodeStateChanged;
            skillTreeManager.OnNodeRefunded -= OnNodeStateChanged;
            skillTreeManager.OnTreeLoaded   -= OnTreeReloaded;
        }

        private void OnEnable()  => Cursor.visible = true;
        private void OnDisable() => Cursor.visible = false;

        // ── Public API ────────────────────────────────────────────────────────────

        public void Open()
        {
            if (!IsReady()) return;
            gameObject.SetActive(true);

            if (_buttons.Count == 0)
            {
                SpawnButtons();
                SpawnConnections();
            }

            CenterView();
            RefreshAll();
            SetFeedback(string.Empty);
            tooltip?.Hide();
        }

        public void Close()
        {
            gameObject.SetActive(false);
            _selectedNode = null;
            tooltip?.Hide();
        }

        // ── Input ─────────────────────────────────────────────────────────────────

        public void OnPointerClick(PointerEventData e)
        {
            if (e.dragging) return;
            if (e.button != PointerEventData.InputButton.Left) return;
            _selectedNode = null;
            RefreshHighlights();
            RefreshUnlockButton();
            tooltip?.Hide();
        }

        public void OnBeginDrag(PointerEventData e)
        {
            _dragStartMouse = e.position;
            _dragStartPan   = _panOffset;
        }

        public void OnDrag(PointerEventData e)
            => SetPanOffset(_dragStartPan + (e.position - _dragStartMouse));

        public void OnNodeButtonClicked(SkillNodeSO node)
        {
            _selectedNode = node;
            RefreshHighlights();
            RefreshUnlockButton();

            if (_buttonMap.TryGetValue(node.nodeId, out var btn))
                tooltip?.Show(node, skillTreeManager.GetRank(node.nodeId),
                    btn.GetComponent<RectTransform>().position,
                    skillTreeManager.GetNodeVisibilityState(node),
                    GetTooltipLockReason(node));
        }

        public void OnNodeHovered(SkillNodeSO node, Vector3 screenPos)
            => tooltip?.Show(node, skillTreeManager.GetRank(node.nodeId), screenPos,
                skillTreeManager.GetNodeVisibilityState(node),
                GetTooltipLockReason(node));

        /// <summary>
        /// Returns a lock reason worth showing in the tooltip, or null if the node
        /// is already unlockable/unlocked (nothing to explain) or at max rank.
        /// </summary>
        private string GetTooltipLockReason(SkillNodeSO node)
        {
            var visibility = skillTreeManager.GetNodeVisibilityState(node);
            if (visibility != NodeVisibilityState.Visible) return null; // Unlockable/Unlocked/Mystery/Hidden
            if (skillTreeManager.IsMaxRank(node.nodeId)) return null;
            string reason = skillTreeManager.GetLockReason(node.nodeId);
            return string.IsNullOrEmpty(reason) ? null : reason;
        }

        public void OnNodeHoverEnd() => tooltip?.Hide();

        public void OnNodeRightClicked(SkillNodeSO node)
        {
            if (node == null || !skillTreeManager.IsUnlocked(node.nodeId)) return;

            string blockReason = GetRefundBlockReason(node);
            if (!string.IsNullOrEmpty(blockReason)) { SetFeedback(blockReason); return; }

            int currentRank = skillTreeManager.GetRank(node.nodeId);

            var returned = skillTreeManager.PreviewRefund(node.nodeId);
            string returnLine = FormatCosts(returned);

            SetFeedback($"Refund '{node.displayName}' rank {currentRank}?\nYou will receive {returnLine} back.");
            _pendingRefundNode = node;
            if (refundConfirmButton != null) refundConfirmButton.gameObject.SetActive(true);
        }

        private static string FormatCosts(IReadOnlyList<(ResourceDefinitionSO Resource, int Amount)> costs)
        {
            if (costs == null || costs.Count == 0) return "nothing";
            var sb = new System.Text.StringBuilder();
            foreach (var (res, amt) in costs)
            {
                if (res == null) continue;
                if (sb.Length > 0) sb.Append("  ");
                sb.Append(res.Format(amt));
            }
            return sb.Length > 0 ? sb.ToString() : "nothing";
        }

        // ── Tab slide ─────────────────────────────────────────────────────────────

        private void SetFilterAnimated(SkillBranch? branch, int newTabIndex)
        {
            if (branch == _activeFilter) return;
            int direction  = newTabIndex > _activeTabIndex ? 1 : -1;
            _activeTabIndex = newTabIndex;
            if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
            _slideCoroutine = StartCoroutine(SlidePanelTransition(branch, direction));
        }

        private IEnumerator SlidePanelTransition(SkillBranch? newBranch, int direction)
        {
            float distance = slideDistance > 0f
                ? slideDistance
                : (viewport != null ? viewport.rect.width : 800f);

            Vector2 restPos = _panOffset;
            Vector2 outPos  = restPos + new Vector2(-direction * distance, 0f);
            yield return LerpPanRoot(restPos, outPos, slideDuration * 0.45f);

            _activeFilter = newBranch;
            ApplyFilterVisibility();
            CenterView();

            Vector2 newRestPos  = _panOffset;
            Vector2 arriveFrom  = newRestPos + new Vector2(direction * distance, 0f);
            panRoot.anchoredPosition = arriveFrom;
            yield return LerpPanRoot(arriveFrom, newRestPos, slideDuration * 0.55f);

            _slideCoroutine = null;
        }

        private IEnumerator LerpPanRoot(Vector2 from, Vector2 to, float duration)
        {
            if (duration <= 0f) { panRoot.anchoredPosition = to; yield break; }
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t  = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                panRoot.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
                yield return null;
            }
            panRoot.anchoredPosition = to;
        }

        // ── Spawn ─────────────────────────────────────────────────────────────────

        private void SpawnButtons()
        {
            var tree = skillTreeManager.GetTree();
            if (tree == null || nodeButtonPrefab == null)
            {
                SkillTreeLogger.LogError("SkillTreePanel", "Missing tree or nodeButtonPrefab.");
                return;
            }

            foreach (var node in tree.allNodes)
            {
                if (node == null) continue;

                var go  = Instantiate(nodeButtonPrefab, nodeLayer);
                var btn = go.GetComponent<SkillNodeButton>();
                if (btn == null) { Destroy(go); continue; }

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin        = new Vector2(0f, 1f);
                rt.anchorMax        = new Vector2(0f, 1f);
                rt.pivot            = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(node.graphPosition.x, -node.graphPosition.y);

                btn.Initialize(node, this);
                _buttons.Add(btn);
                _buttonMap[node.nodeId] = btn;
            }
        }

        private void SpawnConnections()
        {
            if (nodeConnectionPrefab == null)
            {
                SkillTreeLogger.LogError("SkillTreePanel", "nodeConnectionPrefab not assigned!");
                return;
            }

            var tree = skillTreeManager.GetTree();
            if (tree == null) return;

            RectTransform connParent = connectionLayer != null ? connectionLayer : nodeLayer;

            foreach (var node in tree.allNodes)
            {
                if (node?.prerequisites == null || node.prerequisites.Count == 0) continue;
                if (!_buttonMap.TryGetValue(node.nodeId, out var toBtn)) continue;

                foreach (var entry in node.prerequisites)
                {
                    if (entry?.node == null) continue;
                    if (!_buttonMap.TryGetValue(entry.node.nodeId, out var fromBtn)) continue;

                    var go   = Instantiate(nodeConnectionPrefab, connParent);
                    var conn = go.GetComponent<NodeConnection>();
                    if (conn == null) { Destroy(go); continue; }

                    if (connectionLayer == null) go.transform.SetAsFirstSibling();

                    conn.Initialize(
                        fromBtn.GetComponent<RectTransform>(),
                        toBtn.GetComponent<RectTransform>());

                    _connections.Add(conn);
                }
            }
        }

        // ── View / pan ────────────────────────────────────────────────────────────

        private void CenterView()
        {
            if (_buttons.Count == 0 || viewport == null || panRoot == null) return;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            bool  any  = false;

            foreach (var btn in _buttons)
            {
                if (btn == null || !btn.gameObject.activeSelf || btn.Node == null) continue;
                minX = Mathf.Min(minX, btn.Node.graphPosition.x);
                minY = Mathf.Min(minY, btn.Node.graphPosition.y);
                maxX = Mathf.Max(maxX, btn.Node.graphPosition.x);
                maxY = Mathf.Max(maxY, btn.Node.graphPosition.y);
                any  = true;
            }

            if (!any) return;

            var treeCenter = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            var viewSize   = viewport.rect.size;
            SetPanOffset(new Vector2(
                viewSize.x * 0.5f - treeCenter.x,
                -(viewSize.y * 0.5f - treeCenter.y)));
        }

        private void SetPanOffset(Vector2 offset)
        {
            _panOffset = offset;
            if (panRoot != null) panRoot.anchoredPosition = _panOffset;
        }

        // ── Refresh ───────────────────────────────────────────────────────────────

        private void RefreshAll()
        {
            if (!IsReady()) return;
            RefreshHUD();
            ApplyFilterVisibility();
            RefreshHighlights();
            RefreshUnlockButton();
        }

        /// <summary>Updates each resource label in the HUD entries list.</summary>
        private void RefreshHUD()
        {
            foreach (var entry in resourceHUDEntries)
            {
                if (entry.label == null || entry.resource == null) continue;
                int balance   = ResourceManager.Instance != null
                    ? ResourceManager.Instance.GetBalance(entry.resource) : 0;
                entry.label.text  = entry.resource.Format(balance);
                entry.label.color = entry.resource.displayColor;
            }
        }

        private void ApplyFilterVisibility()
        {
            if (!IsReady()) return;

            foreach (var btn in _buttons)
            {
                if (btn == null || btn.Node == null) continue;

                bool matchesFilter   = _activeFilter == null || btn.Node.branch == _activeFilter;
                var  visibilityState = skillTreeManager.GetNodeVisibilityState(btn.Node);
                bool shouldShow      = matchesFilter && visibilityState != NodeVisibilityState.Hidden;

                btn.gameObject.SetActive(shouldShow);
                if (!shouldShow) continue;

                btn.Refresh(skillTreeManager.GetRank(btn.Node.nodeId), visibilityState);
            }

            RefreshHighlights();
        }

        private void RefreshHighlights()
        {
            foreach (var conn in _connections)
            {
                if (conn == null) continue;

                bool fromVisible = conn.FromBtn != null && conn.FromBtn.gameObject.activeSelf;
                bool toVisible   = conn.ToBtn   != null && conn.ToBtn.gameObject.activeSelf;
                bool show        = fromVisible && toVisible;

                conn.gameObject.SetActive(show);
                if (!show) continue;

                bool highlighted = _selectedNode != null &&
                                   (conn.FromBtn.Node == _selectedNode ||
                                    conn.ToBtn.Node   == _selectedNode);
                conn.SetHighlighted(highlighted);
            }
        }

        private void RefreshUnlockButton()
        {
            if (unlockButton == null) return;

            if (_selectedNode == null)
            {
                unlockButton.interactable = false;
                if (unlockButtonText != null) unlockButtonText.text = "Select a node";
                return;
            }

            bool isMax     = skillTreeManager.IsMaxRank(_selectedNode.nodeId);
            bool canUnlock = skillTreeManager.CanUnlock(_selectedNode.nodeId);

            unlockButton.interactable = canUnlock;
            if (unlockButtonText == null) return;

            if (isMax)
            {
                unlockButtonText.text = "MAX RANK";
                return;
            }

            string reason = skillTreeManager.GetLockReason(_selectedNode.nodeId);
            if (!string.IsNullOrEmpty(reason))
            {
                unlockButtonText.text = reason;
                return;
            }

            int nextRank = skillTreeManager.GetRank(_selectedNode.nodeId) + 1;
            unlockButtonText.text = $"Unlock  {_selectedNode.GetCostString(nextRank)}";
        }

        // ── Unlock ────────────────────────────────────────────────────────────────

        private void OnUnlockClicked()
        {
            if (_selectedNode == null) return;

            if (ResourceManager.Instance == null)
            {
                SetFeedback("ResourceManager not found!");
                return;
            }

            var result = skillTreeManager.TryUnlock(_selectedNode.nodeId);

            if (result.IsSuccess)
            {
                SetFeedback(string.Empty);
                RefreshAll();
            }
            else
            {
                SetFeedback(result.Message);
            }
        }

        // ── Refund ────────────────────────────────────────────────────────────────

        private void OnRefundConfirmed()
        {
            if (_pendingRefundNode == null) return;

            var result = skillTreeManager.TryRefund(_pendingRefundNode.nodeId);
            if (result.IsSuccess)
            {
                SetFeedback(string.Empty);
                RefreshAll();
            }
            else
            {
                SetFeedback(result.Message);
            }

            _pendingRefundNode = null;
            if (refundConfirmButton != null) refundConfirmButton.gameObject.SetActive(false);
        }

        private string GetRefundBlockReason(SkillNodeSO node)
        {
            var config = skillTreeManager.config;
            if (config != null && !config.AreRefundsAllowed())
                return "Refunds are disabled.";

            int currentRank = skillTreeManager.GetRank(node.nodeId);
            int rankAfter   = currentRank - 1;

            foreach (var other in skillTreeManager.GetTree().GetUnlockedNodes(skillTreeManager.GetState()))
            {
                if (other.nodeId == node.nodeId || other.prerequisites == null) continue;
                foreach (var entry in other.prerequisites)
                {
                    if (entry?.node == null || entry.node.nodeId != node.nodeId) continue;
                    if (rankAfter < entry.requiredRank)
                        return $"Cannot refund — '{other.displayName}' requires rank {entry.requiredRank}.";
                }
            }
            return string.Empty;
        }

        // ── Events ────────────────────────────────────────────────────────────────

        private void OnResourceChanged(ResourceDefinitionSO resource, int newAmount)
        {
            if (!gameObject.activeSelf) return;
            RefreshHUD();
            RefreshAll();
        }

        private void OnTreeReloaded()
        {
            foreach (var btn in _buttons)  if (btn)  Destroy(btn.gameObject);
            foreach (var conn in _connections) if (conn) Destroy(conn.gameObject);
            _buttons.Clear();
            _buttonMap.Clear();
            _connections.Clear();

            if (gameObject.activeSelf)
            {
                SpawnButtons();
                SpawnConnections();
                CenterView();
                RefreshAll();
            }
        }

        private void OnNodeStateChanged(SkillNodeSO n, int r)
        {
            if (gameObject.activeSelf) RefreshAll();
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void SetFeedback(string msg)
        {
            if (feedbackText == null) return;
            feedbackText.text = msg;
            feedbackText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
        }

        private bool IsReady()
        {
            if (skillTreeManager == null || skillTreeManager.GetState() == null
                || panRoot == null || nodeLayer == null)
            {
                SkillTreeLogger.LogError("SkillTreePanel", "Not ready — check Inspector assignments.");
                return false;
            }
            return true;
        }

        // ── Inner types ───────────────────────────────────────────────────────────

        /// <summary>
        /// Pairs a ResourceDefinitionSO with a TMP label in the HUD.
        /// Add one entry per resource you want displayed at the top of the skill tree panel.
        /// </summary>
        [System.Serializable]
        public class ResourceHUDEntry
        {
            public ResourceDefinitionSO resource;
            public TextMeshProUGUI      label;
        }
    }
}