using UnityEngine;
using System;
using System.Collections;
using YuukiDev.Controller;

public class PowerUp : MonoBehaviour
{
    /*
     * Power-up pickup logic
     * by: YuukiDev
     *
     * Applies a specific effect and handles visuals and spawn animation.
     */
    public enum PowerUpType
    {
        WindOrb,
        GraceToken,
        LanternSparks,
        FeatherSlip
    }

    [Header("Type")]
    [SerializeField] private PowerUpType powerUpType = PowerUpType.WindOrb;

    [Header("Wind Orb")]
    [SerializeField] private float windOrbSpeedMultiplier = 1.28f;
    [SerializeField] private float windOrbInstantSpeedGain = 10f;
    [SerializeField] private float windOrbFovBoost = 12f;
    [SerializeField] private Vector2 windOrbDurationRange = new Vector2(1f, 2f);

    [Header("Phantom Grace Token")]
    [SerializeField] private float phantomGraceDuration = 7f;

    [Header("Lantern Sparks")]
    [SerializeField] private float scoreMultiplier = 1.75f;
    [SerializeField] private float scoreBoostDuration = 5f;

    [Header("Feather Slip")]
    [SerializeField] private float maneuverabilityMultiplier = 1.35f;
    [SerializeField] private float maneuverabilityDuration = 4f;

    [Header("Pickup Feedback")]
    [SerializeField, Range(0f, 1f)] private float powerUpStaminaRefillPercent = 0f;

    [Header("Power Up Materials")]
    [SerializeField] private Renderer[] powerUpRenderers;
    [SerializeField] private Material windOrbMaterial;
    [SerializeField] private Material graceTokenMaterial;
    [SerializeField] private Material lanternSparksMaterial;
    [SerializeField] private Material featherSlipMaterial;

    [Header("Spawn Animation")]
    [SerializeField] private bool playSpawnAnimation = true;
    [SerializeField] private float spawnAnimationDuration = 0.25f;
    [SerializeField] private float spawnVerticalOffset = 0.45f;
    [SerializeField] private AnimationCurve spawnEaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private Vector3 authoredBaseScale = Vector3.one;
    
    [Header("Attraction")]
    [SerializeField] private bool attractToPlayer = true;
    [SerializeField] private float attractionRadius = 8f;
    [SerializeField] private float attractionSpeed = 18f;
    [SerializeField] private float nearAttractionMultiplier = 1.7f;

    [Header("Distance Despawn")]
    [SerializeField] private bool enableDistanceDespawn = true;
    [SerializeField] private float despawnDistance = 220f;
    [SerializeField] private float despawnDelay = 2.5f;
    [SerializeField] private float targetResolveInterval = 1f;

    public static event Action<float, float> LanternSparksCollected;
    public static event Action<int> CoinCollected;
    public static event Action<PowerUpType, PlayerController> PowerUpCollected;

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
        CacheRenderersIfNeeded();
        ApplyVisualByType();
        CacheDespawnDistance();
        CacheAttractionRadius();
        CacheColliders();
    }

    private void OnEnable()
    {
        if (baseScale == Vector3.zero)
            baseScale = ResolveBaseScale();

        transform.localScale = baseScale;
        StartSpawnAnimation();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            authoredBaseScale = transform.localScale;

        CacheRenderersIfNeeded();
        ApplyVisualByType();
        CacheDespawnDistance();
        CacheAttractionRadius();
    }

    private void Update()
    {
        TryAttractToTarget();
        HandleDistanceDespawn();
    }

    public void SetPowerUpType(PowerUpType type)
    {
        powerUpType = type;
        ApplyVisualByType();
    }

    public void SetDespawnTarget(Transform target)
    {
        despawnTarget = target;
        farTimer = 0f;
    }

    public void ApplySpawnScaleOverride(Vector3 spawnScale)
    {
        if (spawnScale.sqrMagnitude <= 0.0001f)
            return;

        baseScale = spawnScale;
        authoredBaseScale = spawnScale;
        transform.localScale = baseScale;

        if (spawnAnimationRoutine != null)
            StopCoroutine(spawnAnimationRoutine);

        StartSpawnAnimation();
    }

    public static void NotifyCoinCollected(int value)
    {
        CoinCollected?.Invoke(Mathf.Max(1, value));
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player.IsGameOver)
            return;

        ApplyPowerUp(player);
        PowerUpCollected?.Invoke(powerUpType, player);
        player.ApplyPickupFeedback(powerUpStaminaRefillPercent);
        Destroy(gameObject);
    }

    private void ApplyPowerUp(PlayerController player)
    {
        switch (powerUpType)
        {
            case PowerUpType.WindOrb:
                float minDuration = Mathf.Min(windOrbDurationRange.x, windOrbDurationRange.y);
                float maxDuration = Mathf.Max(windOrbDurationRange.x, windOrbDurationRange.y);
                float duration = UnityEngine.Random.Range(minDuration, maxDuration);
                player.ApplyWindOrbBurst(windOrbSpeedMultiplier, duration, windOrbInstantSpeedGain, windOrbFovBoost);
                break;

            case PowerUpType.GraceToken:
                player.ApplyPhantomGrace(phantomGraceDuration);
                break;

            case PowerUpType.LanternSparks:
                player.ApplyLanternSparksTimer(scoreBoostDuration);
                LanternSparksCollected?.Invoke(scoreMultiplier, scoreBoostDuration);
                break;

            case PowerUpType.FeatherSlip:
                player.ApplyFeatherSlip(maneuverabilityMultiplier, maneuverabilityDuration);
                break;
        }
    }

    private void CacheRenderersIfNeeded()
    {
        if (powerUpRenderers != null && powerUpRenderers.Length > 0)
            return;

        powerUpRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void CacheColliders()
    {
        if (cachedColliders != null && cachedColliders.Length > 0)
            return;

        cachedColliders = GetComponentsInChildren<Collider>(true);
    }

    private void StartSpawnAnimation()
    {
        if (!playSpawnAnimation || !Application.isPlaying)
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

        transform.localScale = baseScale;
        transform.position = startPosition;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = spawnEaseCurve != null ? Mathf.Clamp01(spawnEaseCurve.Evaluate(t)) : t;

            transform.localScale = baseScale;
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

    private void ApplyVisualByType()
    {
        Material selectedMaterial = GetMaterialForType(powerUpType);
        if (selectedMaterial == null || powerUpRenderers == null)
            return;

        for (int i = 0; i < powerUpRenderers.Length; i++)
        {
            if (powerUpRenderers[i] == null)
                continue;

            powerUpRenderers[i].sharedMaterial = selectedMaterial;
        }
    }

    private Material GetMaterialForType(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.WindOrb:
                return windOrbMaterial;
            case PowerUpType.GraceToken:
                return graceTokenMaterial;
            case PowerUpType.LanternSparks:
                return lanternSparksMaterial;
            case PowerUpType.FeatherSlip:
                return featherSlipMaterial;
            default:
                return null;
        }
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

    private Vector3 ResolveBaseScale()
    {
        if (authoredBaseScale.sqrMagnitude > 0.0001f)
            return authoredBaseScale;

        return transform.localScale.sqrMagnitude > 0.0001f ? transform.localScale : Vector3.one;
    }
}
