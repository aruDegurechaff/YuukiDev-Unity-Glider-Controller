using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YuukiDev.Controller;

namespace YuukiDev.OtherScripts
{
    public class CoinsSpawnerManager : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private CoinCollectible coinPrefab;

        [Header("Spawn Timing")]
        [SerializeField] private bool spawnOnEnable = true;
        [SerializeField] private float minSpawnInterval = 0.35f;
        [SerializeField] private float maxSpawnInterval = 0.8f;
        [SerializeField] private int maxActiveCoins = 40;
        [SerializeField] private bool dynamicSpawnRateBySpeed = true;
        [SerializeField] private float slowSpeedIntervalMultiplier = 1.35f;
        [SerializeField] private float fastSpeedIntervalMultiplier = 0.7f;

        [Header("Target")]
        [SerializeField] private Transform playerTarget;
        [SerializeField] private Transform fallbackAnchor;

        [Header("Target Resolve")]
        [SerializeField] private bool autoResolvePlayerTarget = true;
        [SerializeField] private float targetResolveInterval = 0.5f;

        [Header("Spawn Area (Ahead Of Player)")]
        [SerializeField] private float minForwardDistance = 45f;
        [SerializeField] private float maxForwardDistance = 95f;
        [SerializeField] private float sideSpread = 24f;
        [SerializeField] private float verticalSpread = 7f;
        [SerializeField] private float verticalOffset = 1.5f;
        [SerializeField] private bool dynamicDistanceBySpeed = true;
        [SerializeField] private float closeDistanceMultiplier = 0.6f;
        [SerializeField] private float farDistanceMultiplier = 1.6f;
        [SerializeField] private int maxPlacementAttempts = 20;

        [Header("Obstacle Safety")]
        [SerializeField] private string obstacleTag = "Obstacle";
        [SerializeField] private float overlapCheckRadius = 0.75f;
        [SerializeField] private LayerMask overlapCheckMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
        [SerializeField] private int overlapBufferSize = 32;

        private readonly List<CoinCollectible> activeCoins = new List<CoinCollectible>();
        private Coroutine spawnRoutine;
        private Vector3 configuredCoinScale = Vector3.one;
        private float nextTargetResolveTime = 0f;
        private Collider[] overlapResults;
        private PlayerController cachedTargetPlayer;
        private Rigidbody cachedTargetRigidbody;
        private Transform cachedTargetTransform;

        private void Awake()
        {
            configuredCoinScale = ResolveConfiguredScale();
            EnsureOverlapBuffer();
            TryResolvePlayerTarget(true);
        }

        private void OnEnable()
        {
            if (spawnOnEnable)
                StartSpawning();
        }

        private void OnDisable()
        {
            StopSpawning();
        }

        public void StartSpawning()
        {
            if (spawnRoutine != null)
                return;

            spawnRoutine = StartCoroutine(SpawnLoop());
        }

        public void StopSpawning()
        {
            if (spawnRoutine == null)
                return;

            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        private IEnumerator SpawnLoop()
        {
            while (true)
            {
                CleanupActiveList();
                TryResolvePlayerTarget();

                if (coinPrefab != null && activeCoins.Count < Mathf.Max(0, maxActiveCoins))
                    TrySpawnOne();

                float min = Mathf.Min(minSpawnInterval, maxSpawnInterval);
                float max = Mathf.Max(minSpawnInterval, maxSpawnInterval);
                float waitSeconds = UnityEngine.Random.Range(min, max) * GetDynamicSpawnIntervalScale();
                waitSeconds = Mathf.Max(0.05f, waitSeconds);
                yield return new WaitForSeconds(waitSeconds);
            }
        }

        private void TrySpawnOne()
        {
            TryResolvePlayerTarget();
            int attempts = Mathf.Max(1, maxPlacementAttempts);

            for (int i = 0; i < attempts; i++)
            {
                Vector3 position = GenerateRandomPosition();
                if (IsInsideObstacle(position))
                    continue;

                SpawnCoin(position);
                return;
            }
        }

        private void SpawnCoin(Vector3 position)
        {
            CoinCollectible coin = Instantiate(coinPrefab, position, Quaternion.identity);
            coin.InitializeSpawn(playerTarget, configuredCoinScale);
            activeCoins.Add(coin);
        }

        private Vector3 GenerateRandomPosition()
        {
            Transform anchor = GetSpawnAnchor();
            GetSpawnBasis(out Vector3 forward, out Vector3 right);

            float minDistance = Mathf.Min(minForwardDistance, maxForwardDistance);
            float maxDistance = Mathf.Max(minForwardDistance, maxForwardDistance);
            float distanceScale = GetDynamicDistanceScale();
            minDistance *= distanceScale;
            maxDistance *= distanceScale;

            float forwardDistance = UnityEngine.Random.Range(minDistance, maxDistance);
            float sideOffset = UnityEngine.Random.Range(-Mathf.Abs(sideSpread), Mathf.Abs(sideSpread));
            float upOffset = UnityEngine.Random.Range(-Mathf.Abs(verticalSpread), Mathf.Abs(verticalSpread)) + verticalOffset;

            return anchor.position + forward * forwardDistance + right * sideOffset + Vector3.up * upOffset;
        }

        private bool IsInsideObstacle(Vector3 position)
        {
            float checkRadius = Mathf.Max(0.05f, overlapCheckRadius);
            if (HasObstacleOverlap(position, checkRadius, triggerInteraction))
                return true;

            // Also check with trigger colliders in case obstacle volumes are trigger-based.
            if (triggerInteraction == QueryTriggerInteraction.Ignore)
                return HasObstacleOverlap(position, checkRadius, QueryTriggerInteraction.Collide);

            return false;
        }

        private bool HasObstacleOverlap(Vector3 position, float radius, QueryTriggerInteraction query)
        {
            EnsureOverlapBuffer();
            int hitCount = Physics.OverlapSphereNonAlloc(
                position,
                radius,
                overlapResults,
                overlapCheckMask,
                query
            );

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = overlapResults[i];
                if (IsObstacleCollider(hit))
                    return true;
            }

            // Fallback safety if buffer overflows.
            if (hitCount >= overlapResults.Length)
            {
                Collider[] overflowHits = Physics.OverlapSphere(position, radius, overlapCheckMask, query);
                for (int i = 0; i < overflowHits.Length; i++)
                {
                    if (IsObstacleCollider(overflowHits[i]))
                        return true;
                }
            }

            return false;
        }

        private bool IsObstacleCollider(Collider collider)
        {
            if (collider == null)
                return false;

            Transform current = collider.transform;
            while (current != null)
            {
                if (current.CompareTag(obstacleTag))
                    return true;

                current = current.parent;
            }

            return false;
        }

        private void CleanupActiveList()
        {
            for (int i = activeCoins.Count - 1; i >= 0; i--)
            {
                if (activeCoins[i] == null)
                    activeCoins.RemoveAt(i);
            }
        }

        private Transform GetSpawnAnchor()
        {
            TryResolvePlayerTarget();
            if (playerTarget != null)
                return playerTarget;
            if (fallbackAnchor != null)
                return fallbackAnchor;
            return transform;
        }

        private void GetSpawnBasis(out Vector3 forward, out Vector3 right)
        {
            Transform anchor = GetSpawnAnchor();
            forward = anchor.forward;

            if (playerTarget != null)
            {
                forward = Vector3.ProjectOnPlane(playerTarget.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < 0.001f)
                    forward = anchor.forward;
            }

            if (forward.sqrMagnitude < 0.001f)
                forward = transform.forward.sqrMagnitude > 0.001f ? transform.forward : Vector3.forward;

            right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.sqrMagnitude < 0.001f)
                right = anchor.right.sqrMagnitude > 0.001f ? anchor.right : Vector3.right;
        }

        private void OnDrawGizmosSelected()
        {
            Transform anchor = GetSpawnAnchor();
            Vector3 forward = anchor.forward;
            if (playerTarget != null)
            {
                forward = Vector3.ProjectOnPlane(playerTarget.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < 0.001f)
                    forward = anchor.forward;
            }

            float minDistance = Mathf.Min(minForwardDistance, maxForwardDistance);
            float maxDistance = Mathf.Max(minForwardDistance, maxForwardDistance);
            float midDistance = (minDistance + maxDistance) * 0.5f;
            float depth = Mathf.Max(0.1f, maxDistance - minDistance);

            Vector3 center = anchor.position + forward * midDistance + Vector3.up * verticalOffset;
            Vector3 size = new Vector3(Mathf.Abs(sideSpread) * 2f, Mathf.Abs(verticalSpread) * 2f, depth);
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);

            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.2f);
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
            Gizmos.DrawCube(Vector3.zero, size);
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 1f);
            Gizmos.DrawWireCube(Vector3.zero, size);
            Gizmos.matrix = previous;
        }

        private float GetDynamicDistanceScale()
        {
            if (!dynamicDistanceBySpeed)
                return 1f;

            float speed01 = GetPlayerSpeed01();

            float minScale = Mathf.Max(0.1f, closeDistanceMultiplier);
            float maxScale = Mathf.Max(minScale, farDistanceMultiplier);
            return Mathf.Lerp(minScale, maxScale, speed01);
        }

        private float GetDynamicSpawnIntervalScale()
        {
            if (!dynamicSpawnRateBySpeed)
                return 1f;

            float speed01 = GetPlayerSpeed01();
            float slowScale = Mathf.Max(0.05f, slowSpeedIntervalMultiplier);
            float fastScale = Mathf.Max(0.05f, fastSpeedIntervalMultiplier);
            return Mathf.Lerp(slowScale, fastScale, speed01);
        }

        private float GetPlayerSpeed01()
        {
            TryResolvePlayerTarget();
            float speed01 = 0f;
            if (playerTarget == null)
                return speed01;

            RefreshTargetComponentCache();
            if (cachedTargetPlayer != null)
                return Mathf.Clamp01(cachedTargetPlayer.SpeedNormalized);

            if (cachedTargetRigidbody != null)
                speed01 = Mathf.InverseLerp(5f, 45f, cachedTargetRigidbody.linearVelocity.magnitude);

            return speed01;
        }

        private Vector3 ResolveConfiguredScale()
        {
            if (coinPrefab == null)
                return Vector3.one;

            Vector3 scale = coinPrefab.transform.localScale;
            return scale.sqrMagnitude > 0.0001f ? scale : Vector3.one;
        }

        public void SetPlayerTarget(Transform target)
        {
            playerTarget = target;
            nextTargetResolveTime = 0f;
            RefreshTargetComponentCache(true);
        }

        private void TryResolvePlayerTarget(bool force = false)
        {
            if (playerTarget != null)
                return;

            if (!Application.isPlaying && !force)
                return;

            if (!autoResolvePlayerTarget && !force)
                return;

            if (!force && Time.time < nextTargetResolveTime)
                return;

            nextTargetResolveTime = Time.time + Mathf.Max(0.1f, targetResolveInterval);
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                playerTarget = player.transform;
                RefreshTargetComponentCache(true);
            }
        }

        private void RefreshTargetComponentCache(bool force = false)
        {
            if (!force && cachedTargetTransform == playerTarget)
                return;

            cachedTargetTransform = playerTarget;
            cachedTargetPlayer = null;
            cachedTargetRigidbody = null;

            if (playerTarget == null)
                return;

            cachedTargetPlayer = playerTarget.GetComponent<PlayerController>();
            if (cachedTargetPlayer == null)
                cachedTargetPlayer = playerTarget.GetComponentInParent<PlayerController>();

            cachedTargetRigidbody = playerTarget.GetComponent<Rigidbody>();
            if (cachedTargetRigidbody == null)
                cachedTargetRigidbody = playerTarget.GetComponentInParent<Rigidbody>();
        }

        private void EnsureOverlapBuffer()
        {
            int size = Mathf.Max(8, overlapBufferSize);
            if (overlapResults == null || overlapResults.Length != size)
                overlapResults = new Collider[size];
        }
    }
}
