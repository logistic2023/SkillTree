using UnityEngine;
using UnityEngine.UI;

namespace JollyLlama.SkillTreeSystem
{
    /// <summary>
    /// Draws a UI line between two node RectTransforms using a stretched Image.
    /// Attach this to a GameObject that has a RectTransform + Image.
    /// The Image should use a 1×1 white sprite (or any solid sprite) so it can be tinted.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Image))]
    public class NodeConnection : MonoBehaviour
    {
        [Header("Colors")] [SerializeField] private Color defaultColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);
        [SerializeField] private Color highlightedColor = new Color(1.0f, 0.9f, 0.2f, 1.0f);

        [Header("Line")] [SerializeField] private float lineWidth = 3f;

        // Exposed so SkillTreePanel can check visibility
        public SkillNodeButton FromBtn { get; private set; }
        public SkillNodeButton ToBtn { get; private set; }

        private RectTransform _rt;
        private Image _img;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            _img = GetComponent<Image>();

            // Anchor to top-left so anchoredPosition matches nodeLayer space
            _rt.anchorMin = new Vector2(0f, 1f);
            _rt.anchorMax = new Vector2(0f, 1f);
            _rt.pivot = new Vector2(0f, 0.5f);

            _img.color = defaultColor;
            _img.raycastTarget = false;
        }

        /// <summary>
        /// Call once after instantiation to hook up the two endpoints.
        /// Both RectTransforms must be children of the same parent (nodeLayer).
        /// </summary>
        public void Initialize(RectTransform from, RectTransform to)
        {
            FromBtn = from.GetComponent<SkillNodeButton>();
            ToBtn = to.GetComponent<SkillNodeButton>();
            UpdateLine(from, to);
        }

        /// <summary>
        /// Recompute length + rotation from the two anchored positions.
        /// Call this if nodes are ever repositioned at runtime.
        /// </summary>
        public void UpdateLine(RectTransform from, RectTransform to)
        {
            if (from == null || to == null) return;

            Vector2 fromPos = from.anchoredPosition;
            Vector2 toPos = to.anchoredPosition;

            Vector2 delta = toPos - fromPos;
            float len = delta.magnitude;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            // Position the rect's left edge at the 'from' node centre
            _rt.anchoredPosition = fromPos;
            _rt.sizeDelta = new Vector2(len, lineWidth);
            _rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        public void SetHighlighted(bool highlighted)
            => _img.color = highlighted ? highlightedColor : defaultColor;
    }
}