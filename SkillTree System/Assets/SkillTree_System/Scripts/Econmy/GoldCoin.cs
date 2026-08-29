using UnityEngine;

namespace JollyLlama.SkillTreeSystem
{
    [RequireComponent(typeof(BobAndSpin))]
    public class GoldCoin : MonoBehaviour
    {
        [Header("Resource")]
        [Tooltip("Which resource this coin awards. Assign your Gold ResourceDefinitionSO here.")]
        public ResourceDefinitionSO resource;

        [Header("Value")]
        public int value = 1;

        [Header("Attraction Settings")]
        public float pickupRange   = 8f;
        public float timeToAttract = 1.2f;

        [Tooltip("Arc height curve over normalised travel time (0–1).")]
        public AnimationCurve arcCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 0f);
        public float maxArcHeight = 1.5f;

        [Header("Idle Timeout")]
        public float lifetime = 15f;

        // ── Private state ─────────────────────────────────────────────────────────

        private BobAndSpin _bobAndSpin;
        private bool       _attracting;
        private float      _timer;
        private float      _attractSpeed = 0.01f;
        private Vector3    _startPos;
        private Transform  _target;
        private float      _lifetimeTimer;

        private void Start()
        {
            _bobAndSpin = GetComponent<BobAndSpin>();
            _startPos   = transform.position;

            // Warn immediately at spawn so misconfigured prefabs are caught early
            if (resource == null)
                SkillTreeLogger.LogWarning("GoldCoin", $"'{name}' has no ResourceDefinitionSO assigned. No resource will be awarded on collection.");
        }

        private void Update()
        {
            _lifetimeTimer += Time.deltaTime;
            if (_lifetimeTimer >= lifetime) { Destroy(gameObject); return; }

            if (_attracting) FlyTowardTarget();
            else             CheckForNearestTotem();
        }

        private void CheckForNearestTotem()
        {
            Transform nearest = FindNearestTotem();
            if (nearest == null) return;
            if (Vector3.Distance(transform.position, nearest.position) > pickupRange) return;

            _target       = nearest;
            _attracting   = true;
            _startPos     = transform.position;
            _timer        = 0f;
            _attractSpeed = 0.01f;
            if (_bobAndSpin != null) _bobAndSpin.enabled = false;
        }

        private void FlyTowardTarget()
        {
            if (_target == null)
            {
                _target = FindNearestTotem();
                if (_target == null) { Destroy(gameObject); return; }
                _startPos = transform.position;
                _timer    = 0f;
            }

            _attractSpeed  = Mathf.Lerp(_attractSpeed, 1f, Time.deltaTime * 15f);
            _timer        += Time.deltaTime * _attractSpeed;

            float   t       = Mathf.Clamp01(_timer / timeToAttract);
            float   yOffset = arcCurve.Evaluate(t) * maxArcHeight;
            Vector3 pos     = Vector3.Lerp(_startPos, _target.position, t);
            pos.y          += yOffset;
            transform.position = pos;

            if (t >= 1f) Collect();
        }

        private void Collect()
        {
            if (_target != null)
            {
                var fx = _target.GetComponent<TotemGoldCollectFX>();
                fx?.PlayCollectEffect();
            }

            if (ResourceManager.Instance == null)
            {
                SkillTreeLogger.LogWarning("GoldCoin", "Collect: ResourceManager.Instance is null. Add a ResourceManager component to a scene GameObject.");
            }
            else if (resource == null)
            {
                SkillTreeLogger.LogWarning("GoldCoin", "Collect: 'resource' field is null. Assign a ResourceDefinitionSO to this GoldCoin prefab.");
            }
            else
            {
                ResourceManager.Instance.Add(resource, value);
            }

            Destroy(gameObject);
        }

        private Transform FindNearestTotem()
        {
            GameObject[] totems  = GameObject.FindGameObjectsWithTag("Totem");
            Transform    nearest = null;
            float        best    = float.MaxValue;
            foreach (var totem in totems)
            {
                float dist = Vector3.Distance(transform.position, totem.transform.position);
                if (dist < best) { best = dist; nearest = totem.transform; }
            }
            return nearest;
        }
    }
}