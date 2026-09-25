using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JollyLlama.SkillTreeSystem
{
    /// <summary>
    /// Renders a smooth curved connection between two skill nodes as a custom UI
    /// mesh — matching the bezier look of the editor's canvas view — with a small
    /// direction arrowhead and an animated ember "flow" pulse along connections
    /// whose prerequisite has already been unlocked.
    ///
    /// This IS its own Graphic (like Image is), so it renders directly without
    /// needing a sprite. IMPORTANT: if your "Node Connection" prefab previously had
    /// a separate Image component (from the old straight-line version), remove it —
    /// two Graphic components fighting over one CanvasRenderer on the same
    /// GameObject won't render correctly. This script needs to be the only Graphic
    /// on its GameObject.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class NodeConnection : MaskableGraphic
    {
        [Header("Colors")]
        [Tooltip("Locked / prerequisite not yet met — dim, unlit rune.")]
        [SerializeField] private Color defaultColor = new Color(0.45f, 0.32f, 0.22f, 0.55f);
        [Tooltip("Prerequisite satisfied — warm ember glow, and the flow animation plays.")]
        [SerializeField] private Color activeColor = new Color(0.95f, 0.55f, 0.18f, 0.85f);
        [Tooltip("An endpoint of this connection is the currently selected node.")]
        [SerializeField] private Color highlightedColor = new Color(1.00f, 0.85f, 0.35f, 1.00f);

        [Header("Shape")]
        [SerializeField] private float lineWidth   = 4f;
        [SerializeField] private int   segments    = 24;
        [SerializeField] private float arrowLength = 12f;
        [SerializeField] private float arrowWidth  = 7f;

        [Header("Flow Animation")]
        [SerializeField] private bool  animateFlow      = true;
        [Tooltip("Cycles per second the bright band travels along the curve.")]
        [SerializeField] private float flowSpeed        = 0.6f;
        [Tooltip("Fraction of the curve's length lit up at once.")]
        [SerializeField] private float flowBandWidth    = 0.18f;
        [SerializeField] [Range(0f, 1f)] private float flowBrightness = 0.6f;

        public SkillNodeButton FromBtn { get; private set; }
        public SkillNodeButton ToBtn   { get; private set; }

        private RectTransform _rt;
        private Vector2 _fromPos, _toPos;
        private bool  _highlighted;
        private bool  _flowActive;
        private float _flowPhase;

        protected override void Awake()
        {
            base.Awake();
            _rt = GetComponent<RectTransform>();
            raycastTarget = false;

            _rt.anchorMin = new Vector2(0f, 1f);
            _rt.anchorMax = new Vector2(0f, 1f);
            _rt.pivot     = new Vector2(0.5f, 0.5f);
        }

        /// <summary>
        /// Call once after instantiation to hook up the two endpoints.
        /// Both RectTransforms must be children of the same parent (nodeLayer).
        /// </summary>
        public void Initialize(RectTransform from, RectTransform to)
        {
            FromBtn = from.GetComponent<SkillNodeButton>();
            ToBtn   = to.GetComponent<SkillNodeButton>();
            UpdateLine(from, to);
        }

        /// <summary>
        /// Recompute the curve from the two anchored positions. Call this if nodes
        /// are ever repositioned at runtime.
        /// </summary>
        public void UpdateLine(RectTransform from, RectTransform to)
        {
            if (from == null || to == null) return;
            _fromPos = from.anchoredPosition;
            _toPos   = to.anchoredPosition;

            // Size/position this element's own rect to cover the curve's bounding
            // box (plus room for line width and the arrowhead) — the mesh itself is
            // built in local space relative to that rect's centre, in OnPopulateMesh.
            Vector2 pad = Vector2.one * (lineWidth + arrowLength);
            Vector2 min = Vector2.Min(_fromPos, _toPos) - pad;
            Vector2 max = Vector2.Max(_fromPos, _toPos) + pad;
            _rt.anchoredPosition = (min + max) * 0.5f;
            _rt.sizeDelta        = max - min;

            SetVerticesDirty();
        }

        public void SetHighlighted(bool highlighted)
        {
            if (_highlighted == highlighted) return;
            _highlighted = highlighted;
            SetVerticesDirty();
        }

        /// <summary>Whether this connection's prerequisite has been satisfied — drives
        /// the ember color and the animated flow pulse (SkillTreePanel sets this from
        /// GetRank(FromBtn.Node.nodeId) > 0).</summary>
        public void SetFlowActive(bool active)
        {
            if (_flowActive == active) return;
            _flowActive = active;
            _flowPhase  = 0f;
            SetVerticesDirty();
        }

        private void Update()
        {
            if (!_flowActive || !animateFlow) return;
            _flowPhase += Time.deltaTime * flowSpeed;
            if (_flowPhase > 1f) _flowPhase -= 1f;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_rt == null) return;

            Vector2 rectCenter = _rt.anchoredPosition;
            Vector2 from  = _fromPos - rectCenter;
            Vector2 to    = _toPos   - rectCenter;
            Vector2 delta = to - from;
            float   dist  = delta.magnitude;
            if (dist < 0.001f) return;
            Vector2 dirN = delta / dist;

            // Same control-point construction as the editor canvas, for a matching look.
            float   tan     = Mathf.Clamp(dist * 0.35f, 20f, 80f);
            Vector2 tangent = dirN * tan;
            Vector2 p0 = from;
            Vector2 p1 = from + tangent;
            Vector2 p2 = to - tangent;
            Vector2 p3 = to;

            Color baseColor = _highlighted ? highlightedColor : (_flowActive ? activeColor : defaultColor);

            var points = new List<Vector2>(segments + 1);
            for (int i = 0; i <= segments; i++)
                points.Add(EvalBezier(p0, p1, p2, p3, (float)i / segments));

            for (int i = 0; i <= segments; i++)
            {
                float   t   = (float)i / segments;
                Vector2 pt  = points[i];
                Vector2 tangentDir = i < segments
                    ? (points[i + 1] - pt).normalized
                    : (pt - points[i - 1]).normalized;
                Vector2 normal = new Vector2(-tangentDir.y, tangentDir.x);

                Color col = baseColor;
                if (_flowActive && animateFlow)
                {
                    float d = Mathf.Abs(t - _flowPhase);
                    d = Mathf.Min(d, 1f - d); // wrap-around distance on a looping 0..1 band
                    float band = Mathf.Clamp01(1f - d / Mathf.Max(0.0001f, flowBandWidth));
                    col = Color.Lerp(baseColor, Color.white, band * flowBrightness);
                }

                int baseIndex = vh.currentVertCount;
                vh.AddVert(pt - normal * (lineWidth * 0.5f), col, new Vector2(t, 0f));
                vh.AddVert(pt + normal * (lineWidth * 0.5f), col, new Vector2(t, 1f));

                if (i > 0)
                {
                    int prevA = baseIndex - 2, prevB = baseIndex - 1;
                    int curA  = baseIndex,     curB  = baseIndex + 1;
                    vh.AddTriangle(prevA, prevB, curB);
                    vh.AddTriangle(prevA, curB, curA);
                }
            }

            // Direction arrowhead at the curve's midpoint, pointing toward 'to'.
            Vector2 midPt  = EvalBezier(p0, p1, p2, p3, 0.5f);
            Vector2 midDir = (EvalBezier(p0, p1, p2, p3, 0.52f) - EvalBezier(p0, p1, p2, p3, 0.48f)).normalized;
            AddArrow(vh, midPt, midDir, baseColor);
        }

        private void AddArrow(VertexHelper vh, Vector2 tip, Vector2 dir, Color col)
        {
            Vector2 back = -dir;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            Vector2 baseCenter = tip + back * arrowLength;
            Vector2 p1 = baseCenter + perp * arrowWidth;
            Vector2 p2 = baseCenter - perp * arrowWidth;

            int start = vh.currentVertCount;
            vh.AddVert(tip, col, Vector2.zero);
            vh.AddVert(p1,  col, Vector2.zero);
            vh.AddVert(p2,  col, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
        }

        private static Vector2 EvalBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u = 1f - t;
            return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
        }
    }
}