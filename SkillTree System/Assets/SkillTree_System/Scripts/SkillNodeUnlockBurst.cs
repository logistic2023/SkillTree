using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace JollyLlama.SkillTreeSystem
{
    /// <summary>
    /// Plays a dark-fantasy "rune ignition" burst on a skill node when it's
    /// purchased/ranked up: an expanding ember-crack glow (the EmberBurn shader),
    /// a handful of spark particles, and a quick scale punch. No dependency on
    /// Unity's ParticleSystem or a tweening package — sparks are plain code-driven
    /// UI Images, and a soft round spark sprite is generated at runtime if you
    /// don't assign one, so this drops into a node prefab with minimal setup.
    ///
    /// Setup:
    ///  1. Add a child Image under the node button (above background/icon, below
    ///     any "locked" overlay), sized to the node's bounds. Give it a Material
    ///     using "JollyLlama/UI/EmberBurn". Assign it to burnOverlay.
    ///  2. Leave punchTarget/sparkParent empty to default to this node's own
    ///     RectTransform, or assign explicitly if you want the punch/sparks
    ///     centred somewhere else.
    ///  3. Call Play() — SkillNodeButton.PlayUnlockBurst() does this, wired from
    ///     SkillTreePanel right when a purchase/rank-up succeeds.
    /// </summary>
    public class SkillNodeUnlockBurst : MonoBehaviour
    {
        [Header("Burn Ring")]
        [SerializeField] private Image burnOverlay;
        [SerializeField] private float burnDuration = 0.7f;
        [SerializeField] private AnimationCurve burnCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Scale Punch")]
        [SerializeField] private RectTransform punchTarget;
        [SerializeField] private float punchScale    = 1.18f;
        [SerializeField] private float punchDuration = 0.28f;

        [Header("Sparks")]
        [SerializeField] private RectTransform sparkParent;
        [SerializeField] private Sprite sparkSprite; // optional — auto-generated if left empty
        [SerializeField] private Color  sparkColorHot  = new Color(1f, 0.75f, 0.35f, 1f);
        [SerializeField] private Color  sparkColorCold = new Color(0.55f, 0.08f, 0.03f, 0f);
        [SerializeField] private int    sparkCount     = 10;
        [SerializeField] private float  sparkSpeed     = 140f;
        [SerializeField] private float  sparkLifetime  = 0.5f;
        [SerializeField] private float  sparkSize      = 10f;

        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static Sprite _generatedSparkSprite; // shared across every node, built once

        private Material  _burnMatInstance;
        private Coroutine _running;

        private void Awake()
        {
            if (punchTarget == null) punchTarget = transform as RectTransform;
            if (sparkParent == null) sparkParent = transform as RectTransform;

            if (burnOverlay != null)
            {
                // Own material instance so _Progress animates per-node rather than
                // globally — uGUI would otherwise share one material across every
                // node using this shader (breaking UI batching is fine here since
                // the overlay is only active during the brief burst).
                _burnMatInstance = new Material(burnOverlay.material);
                burnOverlay.material = _burnMatInstance;
                burnOverlay.raycastTarget = false;
                SetProgress(0f);
                burnOverlay.gameObject.SetActive(false);
            }
        }

        /// <summary>Call this right when a purchase/rank-up succeeds on this node.</summary>
        public void Play()
        {
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            SpawnSparks();

            if (punchTarget != null)
                yield return StartCoroutine(PunchScale());

            if (burnOverlay != null)
            {
                burnOverlay.gameObject.SetActive(true);
                float t = 0f;
                while (t < burnDuration)
                {
                    t += Time.deltaTime;
                    SetProgress(burnCurve.Evaluate(Mathf.Clamp01(t / burnDuration)));
                    yield return null;
                }
                burnOverlay.gameObject.SetActive(false);
            }

            _running = null;
        }

        private IEnumerator PunchScale()
        {
            Vector3 baseScale = Vector3.one;
            float half = punchDuration * 0.5f;

            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                punchTarget.localScale = Vector3.Lerp(baseScale, baseScale * punchScale, t / half);
                yield return null;
            }
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                punchTarget.localScale = Vector3.Lerp(baseScale * punchScale, baseScale, t / half);
                yield return null;
            }
            punchTarget.localScale = baseScale;
        }

        private void SetProgress(float p) => _burnMatInstance?.SetFloat(ProgressId, p);

        // ── Sparks — plain code-driven UI quads, no ParticleSystem dependency ──────

        private void SpawnSparks()
        {
            if (sparkParent == null) return;

            for (int i = 0; i < sparkCount; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float speed = sparkSpeed * Random.Range(0.6f, 1.2f);
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                StartCoroutine(SparkRoutine(dir * speed));
            }
        }

        private IEnumerator SparkRoutine(Vector2 velocity)
        {
            var go = new GameObject("EmberSpark", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(sparkParent, false);
            rt.sizeDelta = Vector2.one * sparkSize;
            rt.anchoredPosition = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.sprite = GetSparkSpriteInstance();
            img.raycastTarget = false;
            img.color = sparkColorHot;

            Vector2 startPos = rt.anchoredPosition;
            float t = 0f;
            while (t < sparkLifetime)
            {
                t += Time.deltaTime;
                float k = t / sparkLifetime;
                rt.anchoredPosition = startPos + velocity * t;
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.2f, k);
                img.color = Color.Lerp(sparkColorHot, sparkColorCold, k);
                yield return null;
            }

            Destroy(go);
        }

        /// <summary>A small soft-edged round sprite, generated once at runtime and
        /// shared by every node — so sparks work with zero art asset setup.</summary>
        private static Sprite GetSparkSprite()
        {
            if (_generatedSparkSprite != null) return _generatedSparkSprite;

            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Vector2 center = new Vector2(size / 2f, size / 2f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / (size / 2f);
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();

            _generatedSparkSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _generatedSparkSprite;
        }

        private Sprite GetSparkSpriteInstance() => sparkSprite != null ? sparkSprite : GetSparkSprite();
    }
}
