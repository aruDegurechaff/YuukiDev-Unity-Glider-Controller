using System;
using System.Collections;
using UnityEngine;
using YuukiDev.Controller;

namespace YuukiDev.OtherScripts
{
    /*
     * Coin collectible behavior
     * by: YuukiDev
     *
     * Handles pickup, attraction, spawn animation, and despawn logic.
     */
    public class CoinCollectible : MonoBehaviour
    {
        [SerializeField] private int coinValue = 25;
        [SerializeField] private float spinSpeed = 120f;

        [Header("Pickup Feedback")]
        [SerializeField, Range(0f, 1f)] private float coinStaminaRefillPercent = 0.04f;
        
        [Header("Spawn Animation")]
        [SerializeField] private bool playSpawnAnimation = true;
        [SerializeField] private float spawnAnimationDuration = 0.2f;
        [SerializeField] private float spawnVerticalOffset = 0.35f;
        [SerializeField, Range(0.05f, 1f)] private float spawnStartScaleMultiplier = 0.65f;
        [SerializeField] private AnimationCurve spawnEaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private Vector3 authoredBaseScale = Vector3.one;
        
        [Header("Attraction")]
        [SerializeField] private bool attractToPlayer = true;
        [SerializeField] private float attractionRadius = 8f;
        [SerializeField] private float attractionSpeed = 22f;
        [SerializeField] private float nearAttractionMultiplier = 1.8f;

        [Header("Distance Despawn")]
        [SerializeField] private bool enableDistanceDespawn = true;
        [SerializeField] private float despawnDistance = 220f;
        [SerializeField] private float despawnDelay = 2f;
        [SerializeField] private float targetResolveInterval = 1f;

        public static event Action<int> CoinCollected;
        public static event Action<PlayerController, int> CoinCollectedByPlayer;

        private Transform despawnTarget;
        private float farTimer = 0f;
        private float nextResolveTime = 0f;
        private float sqrDespawnDistance = 0f;
        private float sqrAttractionRadius = 0f;
        private Vector3 baseScale = Vector3.one;
        private Coroutine spawnAnimationRoutine;
        private Collider[] cachedColliders;

        private void Awake()
        {
            baseScale = ResolveBaseScale();
            transform.localScale = baseScale;
            CacheDespawnDistance();
            CacheAttractionRadius();
            CacheColliders();
        }

        private void OnEnable()
        {
            if (baseScale == Vector3.zero)
                baseScale = ResolveBaseScale();

            transform.localScale = baseScale;
            RestartSpawnAnimation();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
                authoredBaseScale = transform.localScale;

            CacheDespawnDistance();
            CacheAttractionRadius();
        }

        private void Update()
        {
            TryAttractToTarget();
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
            HandleDistanceDespawn();
        }

        public void SetDespawnTarget(Transform target)
        {
            despawnTarget = target;
            farTimer = 0f;
        }

        public void InitializeSpawn(Transform target, Vector3 spawnScale)
        {
            SetDespawnTarget(target);

            if (spawnScale.sqrMagnitude > 0.0001f)
            {
                baseScale = spawnScale;
                authoredBaseScale = spawnScale;
                transform.localScale = baseScale;
            }

            RestartSpawnAnimation(true);
        }

        public void ApplySpawnScaleOverride(Vector3 spawnScale)
        {
            if (spawnScale.sqrMagnitude <= 0.0001f)
                return;

            baseScale = spawnScale;
            authoredBaseScale = spawnScale;
            transform.localScale = baseScale;

            RestartSpawnAnimation(true);
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null || player.IsGameOver)
                return;

            int safeValue = Mathf.Max(1, coinValue);
            CoinCollected?.Invoke(safeValue);
            CoinCollectedByPlayer?.Invoke(player, safeValue);
            PowerUp.NotifyCoinCollected(safeValue);
            player.ApplyPickupFeedback(coinStaminaRefillPercent);
            Destroy(gameObject);
        }

        private void HandleDistanceDespawn()
        {
            if (!enableDistanceDespawn)
                return;

            if (despawnTarget == null)
            {
                TryResolveTarget();
                if (despawnTarget == null)
                    return;
            }

            if ((transform.position - despawnTarget.position).sqrMagnitude > sqrDespawnDistance)
            {
                farTimer += Time.deltaTime;
                if (farTimer >= despawnDelay)
                    Destroy(gameObject);
            }
            else
            {
                farTimer = 0f;
            }
        }

        private void TryResolveTarget()
        {
            if (Time.time < nextResolveTime)
                return;

            nextResolveTime = Time.time + Mathf.Max(0.2f, targetResolveInterval);
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null)
                despawnTarget = player.transform;
        }

        private void CacheDespawnDistance()
        {
            float safeDistance = Mathf.Max(1f, despawnDistance);
            sqrDespawnDistance = safeDistance * safeDistance;
        }

        private void CacheAttractionRadius()
        {
            float safeRadius = Mathf.Max(0f, attractionRadius);
            sqrAttractionRadius = safeRadius * safeRadius;
        }

        private void TryAttractToTarget()
        {
            if (!attractToPlayer || sqrAttractionRadius <= 0f)
                return;

            if (despawnTarget == null)
            {
                TryResolveTarget();
                if (despawnTarget == null)
                    return;
            }

            Vector3 targetPosition = despawnTarget.position;
            Vector3 toTarget = targetPosition - transform.position;
            float sqrDistance = toTarget.sqrMagnitude;

            if (sqrDistance <= 0.0001f || sqrDistance > sqrAttractionRadius)
                return;

            float distance = Mathf.Sqrt(sqrDistance);
            float radius = Mathf.Max(0.001f, attractionRadius);
            float pull01 = 1f - Mathf.Clamp01(distance / radius);
            float speed = Mathf.Max(0.1f, attractionSpeed) * Mathf.Lerp(1f, Mathf.Max(1f, nearAttractionMultiplier), pull01);

            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                speed * Time.deltaTime
            );
        }

        private void CacheColliders()
        {
            if (cachedColliders != null && cachedColliders.Length > 0)
                return;

            cachedColliders = GetComponentsInChildren<Collider>(true);
        }

        private void RestartSpawnAnimation(bool forcePlay = false)
        {
            if ((!playSpawnAnimation && !forcePlay) || !Application.isPlaying)
                return;

            if (spawnAnimationRoutine != null)
                StopCoroutine(spawnAnimationRoutine);

            spawnAnimationRoutine = StartCoroutine(PlaySpawnAnimationRoutine());
        }

        private IEnumerator PlaySpawnAnimationRoutine()
        {
            CacheColliders();
            SetCollidersEnabled(false);

            float duration = Mathf.Max(0.01f, spawnAnimationDuration);
            Vector3 startPosition = transform.position - Vector3.up * Mathf.Max(0f, spawnVerticalOffset);
            Vector3 endPosition = transform.position;
            Vector3 startScale = baseScale * Mathf.Clamp(spawnStartScaleMultiplier, 0.05f, 1f);

            transform.localScale = startScale;
            transform.position = startPosition;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = spawnEaseCurve != null ? Mathf.Clamp01(spawnEaseCurve.Evaluate(t)) : t;

                transform.localScale = Vector3.LerpUnclamped(startScale, baseScale, eased);
                transform.position = Vector3.Lerp(startPosition, endPosition, eased);
                yield return null;
            }

            transform.localScale = baseScale;
            transform.position = endPosition;
            SetCollidersEnabled(true);
            spawnAnimationRoutine = null;
        }

        private void SetCollidersEnabled(bool value)
        {
            if (cachedColliders == null)
                return;

            for (int i = 0; i < cachedColliders.Length; i++)
            {
                if (cachedColliders[i] != null)
                    cachedColliders[i].enabled = value;
            }
        }

        private Vector3 ResolveBaseScale()
        {
            if (authoredBaseScale.sqrMagnitude > 0.0001f)
                return authoredBaseScale;

            return transform.localScale.sqrMagnitude > 0.0001f ? transform.localScale : Vector3.one;
        }
    }
}
