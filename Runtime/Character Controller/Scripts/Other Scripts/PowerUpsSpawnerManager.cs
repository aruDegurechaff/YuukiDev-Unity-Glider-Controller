using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YuukiDev.Controller;

namespace YuukiDev.OtherScripts
{
    /*
     * Power-up spawning manager
     * by: YuukiDev
     *
     * Spawns weighted power-ups with obstacle-safe placement.
     */
    public class PowerUpsSpawnerManager : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private PowerUp powerUpPrefab;

        [Header("Spawn Timing")]
        [SerializeField] private bool spawnOnEnable = true;
        [SerializeField] private float minSpawnInterval = 1.3f;
        [SerializeField] private float maxSpawnInterval = 2.8f;
        [SerializeField] private int maxActivePowerUps = 8;

        [Header("Spawn Weights")]
        [SerializeField] private float windOrbWeight = 1f;
        [SerializeField] private float graceTokenWeight = 1f;
        [SerializeField] private float lanternSparksWeight = 1f;
        [SerializeField] private float featherSlipWeight = 1f;

        [Header("Target")]
        [SerializeField] private Transform playerTarget;
        [SerializeField] private Transform fallbackAnchor;

        [Header("Target Resolve")]
        [SerializeField] private bool autoResolvePlayerTarget = true;
        [SerializeField] private float targetResolveInterval = 0.5f;

        [Header("Spawn Area (Ahead Of Player)")]
        [SerializeField] private float minForwardDistance = 65f;
        [SerializeField] private float maxForwardDistance = 110f;
        [SerializeField] private float sideSpread = 20f;
        [SerializeField] private float verticalSpread = 10f;
        [SerializeField] private float verticalOffset = 2f;
        [SerializeField] private bool dynamicDistanceBySpeed = true;
        [SerializeField] private float closeDistanceMultiplier = 0.6f;
        [SerializeField] private float farDistanceMultiplier = 1.6f;
        [SerializeField] private int maxPlacementAttempts = 20;

        [Header("Obstacle Safety")]
        [SerializeField] private string obstacleTag = "Obstacle";
        [SerializeField] private float overlapCheckRadius = 1f;
        [SerializeField] private LayerMask overlapCheckMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
        [SerializeField] private int overlapBufferSize = 32;

        private readonly List<PowerUp> activePowerUps = new List<PowerUp>();
        private Coroutine spawnRoutine;
        private PowerUp.PowerUpType[] availableTypes;
        private Vector3 configuredPowerUpScale = Vector3.one;
        private float nextTargetResolveTime = 0f;
        private Collider[] overlapResults;
        private PlayerController cachedTargetPlayer;
        private Rigidbody cachedTargetRigidbody;
        private Transform cachedTargetTransform;

        private void Awake()
        {
            availableTypes = (PowerUp.PowerUpType[])Enum.GetValues(typeof(PowerUp.PowerUpType));
            configuredPowerUpScale = ResolveConfiguredScale();
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

                if (powerUpPrefab != null && activePowerUps.Count < Mathf.Max(0, maxActivePowerUps))
                {
                    TrySpawnOne();
                }

                float min = Mathf.Min(minSpawnInterval, maxSpawnInterval);
                float max = Mathf.Max(minSpawnInterval, maxSpawnInterval);
                float waitSeconds = UnityEngine.Random.Range(min, max);
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

                PowerUp spawned = Instantiate(powerUpPrefab, position, Quaternion.identity);
                spawned.ApplySpawnScaleOverride(configuredPowerUpScale);
                spawned.SetPowerUpType(GetRandomType());
                spawned.SetDespawnTarget(playerTarget);
                activePowerUps.Add(spawned);
                return;
            }
        }

        private Vector3 GenerateRandomPosition()
        {
            Transform anchor = GetSpawnAnchor();
            Vector3 origin = anchor.position;

            Vector3 forward = anchor.forward;
            if (playerTarget != null)
            {
                // Keep spawn direction forward in world space so items don't spawn underground on steep pitch.
                forward = Vector3.ProjectOnPlane(playerTarget.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < 0.001f)
                    forward = anchor.forward;
            }

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            float minDistance = Mathf.Min(minForwardDistance, maxForwardDistance);
            float maxDistance = Mathf.Max(minForwardDistance, maxForwardDistance);
            float distanceScale = GetDynamicDistanceScale();
            minDistance *= distanceScale;
            maxDistance *= distanceScale;
            float forwardDistance = UnityEngine.Random.Range(minDistance, maxDistance);
            float sideOffset = UnityEngine.Random.Range(-Mathf.Abs(sideSpread), Mathf.Abs(sideSpread));
            float upOffset = UnityEngine.Random.Range(-Mathf.Abs(verticalSpread), Mathf.Abs(verticalSpread)) + verticalOffset;

            return origin + forward * forwardDistance + right * sideOffset + Vector3.up * upOffset;
        }

        private bool IsInsideObstacle(Vector3 position)
        {
            EnsureOverlapBuffer();
            int hitCount = Physics.OverlapSphereNonAlloc(
                position,
                Mathf.Max(0.05f, overlapCheckRadius),
                overlapResults,
                overlapCheckMask,
                triggerInteraction
            );

            for (int i = 0; i < hitCount; i++)
            {
                if (IsObstacleCollider(overlapResults[i]))
                    return true;
            }

            // Fallback safety if buffer overflows.
            if (hitCount >= overlapResults.Length)
            {
                Collider[] overflowHits = Physics.OverlapSphere(
                    position,
                    Mathf.Max(0.05f, overlapCheckRadius),
                    overlapCheckMask,
                    triggerInteraction
                );

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

        private PowerUp.PowerUpType GetRandomType()
        {
            if (availableTypes == null || availableTypes.Length == 0)
                return PowerUp.PowerUpType.WindOrb;

            float totalWeight = 0f;
            for (int i = 0; i < availableTypes.Length; i++)
                totalWeight += Mathf.Max(0f, GetWeightForType(availableTypes[i]));

            if (totalWeight <= 0f)
            {
                int fallbackIndex = UnityEngine.Random.Range(0, availableTypes.Length);
                return availableTypes[fallbackIndex];
            }

            float roll = UnityEngine.Random.value * totalWeight;
            float cumulative = 0f;

            for (int i = 0; i < availableTypes.Length; i++)
            {
                cumulative += Mathf.Max(0f, GetWeightForType(availableTypes[i]));
                if (roll <= cumulative)
                    return availableTypes[i];
            }

            return availableTypes[availableTypes.Length - 1];
        }

        private float GetWeightForType(PowerUp.PowerUpType type)
        {
            switch (type)
            {
                case PowerUp.PowerUpType.WindOrb:
                    return windOrbWeight;
                case PowerUp.PowerUpType.GraceToken:
                    return graceTokenWeight;
                case PowerUp.PowerUpType.LanternSparks:
                    return lanternSparksWeight;
                case PowerUp.PowerUpType.FeatherSlip:
                    return featherSlipWeight;
                default:
                    return 1f;
            }
        }

        private void CleanupActiveList()
        {
            for (int i = activePowerUps.Count - 1; i >= 0; i--)
            {
                if (activePowerUps[i] == null)
                    activePowerUps.RemoveAt(i);
            }
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

            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.25f);
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
            Gizmos.DrawCube(Vector3.zero, size);
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 1f);
            Gizmos.DrawWireCube(Vector3.zero, size);
            Gizmos.matrix = previous;
        }

        private float GetDynamicDistanceScale()
        {
            TryResolvePlayerTarget();
            if (!dynamicDistanceBySpeed)
                return 1f;

            float speed01 = 0f;
            if (playerTarget != null)
            {
                RefreshTargetComponentCache();
                if (cachedTargetPlayer != null)
                {
                    speed01 = Mathf.Clamp01(cachedTargetPlayer.SpeedNormalized);
                }
                else if (cachedTargetRigidbody != null)
                {
                    speed01 = Mathf.InverseLerp(5f, 45f, cachedTargetRigidbody.linearVelocity.magnitude);
                }
            }

            float minScale = Mathf.Max(0.1f, closeDistanceMultiplier);
            float maxScale = Mathf.Max(minScale, farDistanceMultiplier);
            return Mathf.Lerp(minScale, maxScale, speed01);
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

        private Vector3 ResolveConfiguredScale()
        {
            if (powerUpPrefab == null)
                return Vector3.one;

            Vector3 scale = powerUpPrefab.transform.localScale;
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
