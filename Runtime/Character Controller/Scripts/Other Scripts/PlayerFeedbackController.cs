using UnityEngine;
using UnityEngine.VFX;
using YuukiDev.Controller;
using YuukiDev.Input;

namespace YuukiDev.OtherScripts
{
    /*
     * Player feedback hub
     * by: YuukiDev
     *
     * Orchestrates trails, VFX, and SFX based on player state.
     */
    [DisallowMultipleComponent]
    public class PlayerFeedbackController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerController player;
        [SerializeField] private YuukiPlayerInput playerInput;
        [SerializeField] private Transform effectSpawnPoint;
        [SerializeField] private Transform stateVfxParent;

        [Header("Glide Trail")]
        [SerializeField] private TrailRenderer[] glideTrails;
        [SerializeField] private Gradient trailColorBySpeed;
        [SerializeField] private Gradient trailSecondaryColorBySpeed;
        [SerializeField] private float minTrailWidth = 0.12f;
        [SerializeField] private float maxTrailWidth = 0.28f;
        [SerializeField] private bool driveTrailMaterialColors = true;
        [SerializeField] private string trailPrimaryColorProperty = "Color01";
        [SerializeField] private string trailSecondaryColorProperty = "Color02";
        [SerializeField] private Transform[] trailSpawnPoints;
        [SerializeField] private Transform trailSpawnRoot;
        [SerializeField] private bool autoResolveTrailSpawnPoints = true;
        [SerializeField] private string trailSpawnRootName = "Trails";
        [SerializeField] private string trailSpawnPointNameFilter = "TrailSpawn";
        [SerializeField] private bool followTrailSpawnPoints = true;
        [SerializeField] private bool ignorePlayerRollForTrails = true;
        [SerializeField] private Transform trailForwardReference;

        [Header("State VFX")]
        [SerializeField] private GameObject defaultGlideVfxPrefab;
        [SerializeField] private GameObject speedUpVfxPrefab;
        [SerializeField] private GameObject slowDownVfxPrefab;

        [Header("Camera Speedline VFX")]
        [SerializeField] private VisualEffect cameraSpeedlineVfx;
        [SerializeField] private bool autoResolveCameraSpeedlineVfx = true;
        [SerializeField] private float speedlineResolveInterval = 1f;
        [SerializeField, Range(0f, 1f)] private float speedlineStartSpeedNormalized = 0.2f;
        [SerializeField] private float speedlineMaxRate = 60f;
        [SerializeField] private float speedlineResponse = 8f;
        [SerializeField] private string speedlineRateProperty = "Rate";
        [SerializeField] private bool setSpeedlineIntensityProperty = true;
        [SerializeField] private string speedlineIntensityProperty = "Intensity";
        [SerializeField] private bool keepSpeedlineAlignedToCamera = true;
        [SerializeField] private float speedlineForwardOffset = 0.4f;
        [SerializeField] private Vector3 speedlineScale = Vector3.one;

        [Header("Power Up VFX")]
        [SerializeField] private GameObject windOrbCollectVfxPrefab;
        [SerializeField] private GameObject graceTokenCollectVfxPrefab;
        [SerializeField] private GameObject lanternSparksCollectVfxPrefab;
        [SerializeField] private GameObject featherSlipCollectVfxPrefab;
        [SerializeField] private GameObject coinCollectVfxPrefab;
        [SerializeField] private float fallbackFxLifetime = 2f;

        [Header("State SFX")]
        [SerializeField] private AudioSource stateLoopSource;
        [SerializeField] private AudioClip defaultGlideLoopClip;
        [SerializeField] private AudioClip speedUpLoopClip;
        [SerializeField] private AudioClip slowDownLoopClip;
        [SerializeField] private Vector2 loopPitchBySpeed = new Vector2(0.9f, 1.25f);
        [SerializeField] private Vector2 loopVolumeBySpeed = new Vector2(0.35f, 0.9f);

        [Header("One-Shot SFX")]
        [SerializeField] private AudioSource oneShotSource;
        [SerializeField] private AudioClip windOrbSfx;
        [SerializeField] private AudioClip graceTokenSfx;
        [SerializeField] private AudioClip lanternSparksSfx;
        [SerializeField] private AudioClip featherSlipSfx;
        [SerializeField] private AudioClip coinSfx;
        [SerializeField] private float oneShotVolume = 1f;

        private PlayerController.GlideMode currentMode = PlayerController.GlideMode.Normal;
        private GameObject[] defaultGlideVfxInstances;
        private GameObject[] speedUpVfxInstances;
        private GameObject[] slowDownVfxInstances;
        private int speedlineRatePropertyId;
        private int speedlineIntensityPropertyId;
        private bool speedlineHasRate;
        private bool speedlineHasIntensity;
        private float speedlineVisual;
        private float nextSpeedlineResolveTime;
        private int trailPrimaryColorId;
        private int trailSecondaryColorId;
        private MaterialPropertyBlock trailPropertyBlock;

        private void Awake()
        {
            if (player == null)
                player = GetComponent<PlayerController>();
            if (playerInput == null)
                playerInput = GetComponent<YuukiPlayerInput>();

            if ((glideTrails == null || glideTrails.Length == 0) && player != null)
                glideTrails = player.GetComponentsInChildren<TrailRenderer>(true);

            if (effectSpawnPoint == null && player != null)
                effectSpawnPoint = player.transform;
            if (stateVfxParent == null && player != null)
                stateVfxParent = player.transform;

            defaultGlideVfxInstances = CreateStateVfxInstances(defaultGlideVfxPrefab);
            speedUpVfxInstances = CreateStateVfxInstances(speedUpVfxPrefab);
            slowDownVfxInstances = CreateStateVfxInstances(slowDownVfxPrefab);
            ResolveSpeedlineVfx(true);
            if (cameraSpeedlineVfx != null)
                CacheSpeedlineCapabilities();
            CacheTrailPropertyIds();
            ResolveTrailSpawnPoints();
        }

        private void OnEnable()
        {
            if (player != null)
            {
                currentMode = player.CurrentGlideMode;
                player.GlideModeChanged += OnGlideModeChanged;
            }

            PlayerController.GameOverTriggered += OnGameOver;
            PowerUp.PowerUpCollected += OnPowerUpCollected;
            CoinCollectible.CoinCollectedByPlayer += OnCoinCollectedByPlayer;

            ApplyStateFeedback(currentMode, true);
        }

        private void OnDisable()
        {
            if (player != null)
                player.GlideModeChanged -= OnGlideModeChanged;

            PlayerController.GameOverTriggered -= OnGameOver;
            PowerUp.PowerUpCollected -= OnPowerUpCollected;
            CoinCollectible.CoinCollectedByPlayer -= OnCoinCollectedByPlayer;
        }

        private void Update()
        {
            if (player == null)
                return;

            UpdateTrailsBySpeed();
            UpdateStateLoopBySpeed();
            UpdateCameraSpeedlineBySpeed();
        }

        private void LateUpdate()
        {
            if (player == null)
                return;

            ResolveTrailSpawnPoints();
            EnsureTrailReferences();
            UpdateTrailSpawnPoints();
        }

        private void OnGlideModeChanged(PlayerController.GlideMode mode)
        {
            currentMode = mode;
            ApplyStateFeedback(currentMode, false);
        }

        private void ApplyStateFeedback(PlayerController.GlideMode mode, bool forceAudioRefresh)
        {
            SetStateVfxActive(defaultGlideVfxInstances, mode == PlayerController.GlideMode.Normal);
            SetStateVfxActive(speedUpVfxInstances, mode == PlayerController.GlideMode.SpeedingUp);
            SetStateVfxActive(slowDownVfxInstances, mode == PlayerController.GlideMode.SlowingDown);

            RefreshLoopClip(mode, forceAudioRefresh);
        }

        private void RefreshLoopClip(PlayerController.GlideMode mode, bool force)
        {
            if (stateLoopSource == null)
                return;

            AudioClip targetClip = GetStateLoopClip(mode);
            if (targetClip == null)
            {
                if (stateLoopSource.isPlaying)
                    stateLoopSource.Stop();
                return;
            }

            if (force || stateLoopSource.clip != targetClip)
            {
                stateLoopSource.clip = targetClip;
                stateLoopSource.loop = true;
                stateLoopSource.Play();
            }
        }

        private AudioClip GetStateLoopClip(PlayerController.GlideMode mode)
        {
            switch (mode)
            {
                case PlayerController.GlideMode.SpeedingUp:
                    return speedUpLoopClip;
                case PlayerController.GlideMode.SlowingDown:
                    return slowDownLoopClip;
                default:
                    return defaultGlideLoopClip;
            }
        }

        private void UpdateStateLoopBySpeed()
        {
            if (stateLoopSource == null || !stateLoopSource.isPlaying || player == null)
                return;

            float speed01 = Mathf.Clamp01(player.SpeedNormalized);
            stateLoopSource.pitch = Mathf.Lerp(loopPitchBySpeed.x, loopPitchBySpeed.y, speed01);
            stateLoopSource.volume = Mathf.Lerp(loopVolumeBySpeed.x, loopVolumeBySpeed.y, speed01);
        }

        private void UpdateTrailsBySpeed()
        {
            if (glideTrails == null || glideTrails.Length == 0 || player == null)
                return;

            float speed01 = Mathf.Clamp01(player.SpeedNormalized);
            float width = Mathf.Lerp(minTrailWidth, maxTrailWidth, speed01);

            for (int i = 0; i < glideTrails.Length; i++)
            {
                TrailRenderer trail = glideTrails[i];
                if (trail == null)
                    continue;

                trail.widthMultiplier = width;
            }
        }

        private void OnPowerUpCollected(PowerUp.PowerUpType type, PlayerController collector)
        {
            if (collector != player)
                return;

            switch (type)
            {
                case PowerUp.PowerUpType.WindOrb:
                    SpawnPickupVfx(windOrbCollectVfxPrefab);
                    PlayOneShot(windOrbSfx);
                    break;
                case PowerUp.PowerUpType.GraceToken:
                    SpawnPickupVfx(graceTokenCollectVfxPrefab);
                    PlayOneShot(graceTokenSfx);
                    break;
                case PowerUp.PowerUpType.LanternSparks:
                    SpawnPickupVfx(lanternSparksCollectVfxPrefab);
                    PlayOneShot(lanternSparksSfx);
                    break;
                case PowerUp.PowerUpType.FeatherSlip:
                    SpawnPickupVfx(featherSlipCollectVfxPrefab);
                    PlayOneShot(featherSlipSfx);
                    break;
            }
        }

        private void OnCoinCollectedByPlayer(PlayerController collector, int amount)
        {
            if (collector != player)
                return;

            SpawnPickupVfx(coinCollectVfxPrefab);
            PlayOneShot(coinSfx);
        }

        private void OnGameOver(PlayerController deadPlayer)
        {
            if (deadPlayer != player)
                return;

            SetStateVfxActive(defaultGlideVfxInstances, false);
            SetStateVfxActive(speedUpVfxInstances, false);
            SetStateVfxActive(slowDownVfxInstances, false);

            if (stateLoopSource != null && stateLoopSource.isPlaying)
                stateLoopSource.Stop();

            speedlineVisual = 0f;
            ApplySpeedlineValues(0f);
        }

        private void SpawnPickupVfx(GameObject fxPrefab)
        {
            if (fxPrefab == null)
                return;

            Vector3 spawnPos = effectSpawnPoint != null ? effectSpawnPoint.position : transform.position;
            Quaternion spawnRot = effectSpawnPoint != null ? effectSpawnPoint.rotation : Quaternion.identity;
            GameObject spawnedFx = Instantiate(fxPrefab, spawnPos, spawnRot);
            float ttl = EstimateVfxLifetime(spawnedFx);

            Destroy(spawnedFx, ttl + 0.1f);
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (oneShotSource == null || clip == null)
                return;

            oneShotSource.PlayOneShot(clip, oneShotVolume);
        }

        private GameObject[] CreateStateVfxInstances(GameObject prefab)
        {
            if (prefab == null)
                return System.Array.Empty<GameObject>();

            Transform[] points = (trailSpawnPoints != null && trailSpawnPoints.Length > 0) ? trailSpawnPoints : null;
            if (points == null)
                return new[] { CreateStateVfxInstanceAt(prefab, stateVfxParent != null ? stateVfxParent : transform) };

            GameObject[] instances = new GameObject[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                Transform parent = points[i] != null ? points[i] : (stateVfxParent != null ? stateVfxParent : transform);
                instances[i] = CreateStateVfxInstanceAt(prefab, parent);
            }

            return instances;
        }

        private static GameObject CreateStateVfxInstanceAt(GameObject prefab, Transform parent)
        {
            GameObject instance = Instantiate(prefab, parent);
            Transform instanceTransform = instance.transform;
            instanceTransform.localPosition = Vector3.zero;
            instanceTransform.localRotation = Quaternion.identity;
            instance.SetActive(false);
            return instance;
        }

        private float EstimateVfxLifetime(GameObject fxInstance)
        {
            float ttl = Mathf.Max(fallbackFxLifetime, 0.1f);
            ParticleSystem[] systems = fxInstance.GetComponentsInChildren<ParticleSystem>(true);

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem.MainModule main = systems[i].main;
                float systemLifetime = main.duration + GetStartLifetimeMax(main.startLifetime);
                ttl = Mathf.Max(ttl, systemLifetime);
            }

            return ttl;
        }

        private static float GetStartLifetimeMax(ParticleSystem.MinMaxCurve curve)
        {
            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return curve.constant;
                case ParticleSystemCurveMode.TwoConstants:
                    return curve.constantMax;
                case ParticleSystemCurveMode.Curve:
                case ParticleSystemCurveMode.TwoCurves:
                    return curve.constantMax;
                default:
                    return curve.constantMax;
            }
        }

        private static void SetStateVfxActive(GameObject[] vfxObjects, bool shouldBeActive)
        {
            if (vfxObjects == null)
                return;

            for (int i = 0; i < vfxObjects.Length; i++)
            {
                GameObject vfxObject = vfxObjects[i];
                if (vfxObject == null)
                    continue;

                if (vfxObject.activeSelf != shouldBeActive)
                    vfxObject.SetActive(shouldBeActive);
            }
        }

        private void UpdateCameraSpeedlineBySpeed()
        {
            ResolveSpeedlineVfx(false);
            if (cameraSpeedlineVfx == null || player == null)
                return;

            float speed01 = Mathf.Clamp01(player.SpeedNormalized);
            float targetVisual = Mathf.InverseLerp(
                Mathf.Clamp01(speedlineStartSpeedNormalized),
                1f,
                speed01
            );

            float step = Mathf.Max(0.01f, speedlineResponse) * Time.deltaTime;
            speedlineVisual = Mathf.MoveTowards(speedlineVisual, targetVisual, step);
            ApplySpeedlineValues(speedlineVisual);
            AlignSpeedlineToCamera();
        }

        private void ResolveSpeedlineVfx(bool force)
        {
            if (cameraSpeedlineVfx != null)
                return;

            if (!autoResolveCameraSpeedlineVfx)
                return;

            if (!force && Time.unscaledTime < nextSpeedlineResolveTime)
                return;

            nextSpeedlineResolveTime = Time.unscaledTime + Mathf.Max(0.1f, speedlineResolveInterval);

            Camera mainCam = Camera.main;
            if (mainCam == null)
                return;

            cameraSpeedlineVfx = mainCam.GetComponentInChildren<VisualEffect>(true);
            if (cameraSpeedlineVfx == null)
            {
                Transform camParent = mainCam.transform.parent;
                if (camParent != null)
                    cameraSpeedlineVfx = camParent.GetComponentInChildren<VisualEffect>(true);
            }

            CacheSpeedlineCapabilities();
        }

        private void CacheSpeedlineCapabilities()
        {
            if (cameraSpeedlineVfx == null)
                return;

            speedlineRatePropertyId = Shader.PropertyToID(speedlineRateProperty);
            speedlineIntensityPropertyId = Shader.PropertyToID(speedlineIntensityProperty);
            speedlineHasRate = cameraSpeedlineVfx.HasFloat(speedlineRatePropertyId);
            speedlineHasIntensity = setSpeedlineIntensityProperty && cameraSpeedlineVfx.HasFloat(speedlineIntensityPropertyId);
        }

        private void ApplySpeedlineValues(float visual01)
        {
            if (cameraSpeedlineVfx == null)
                return;

            float clamped = Mathf.Clamp01(visual01);
            if (speedlineHasRate)
            {
                float rate = Mathf.Lerp(0f, Mathf.Max(0f, speedlineMaxRate), clamped);
                cameraSpeedlineVfx.SetFloat(speedlineRatePropertyId, rate);
            }

            if (speedlineHasIntensity)
                cameraSpeedlineVfx.SetFloat(speedlineIntensityPropertyId, clamped);
        }

        private void AlignSpeedlineToCamera()
        {
            if (!keepSpeedlineAlignedToCamera || cameraSpeedlineVfx == null)
                return;

            Camera cam = Camera.main;
            if (cam == null)
                return;

            Transform vfxTransform = cameraSpeedlineVfx.transform;
            float minForward = cam.nearClipPlane + 0.03f;
            float forwardDistance = Mathf.Max(minForward, speedlineForwardOffset);

            vfxTransform.position = cam.transform.position + cam.transform.forward * forwardDistance;
            vfxTransform.rotation = cam.transform.rotation;
            vfxTransform.localScale = speedlineScale;
        }

        private void UpdateTrailSpawnPoints()
        {
            if (!followTrailSpawnPoints || trailSpawnPoints == null || trailSpawnPoints.Length == 0 || glideTrails == null)
                return;

            Transform forwardRef = trailForwardReference != null ? trailForwardReference : player.transform;
            Quaternion noRollRotation = forwardRef != null
                ? Quaternion.LookRotation(forwardRef.forward, Vector3.up)
                : Quaternion.identity;

            int lastIndex = trailSpawnPoints.Length - 1;
            for (int i = 0; i < glideTrails.Length; i++)
            {
                TrailRenderer trail = glideTrails[i];
                if (trail == null)
                    continue;

                int index = Mathf.Clamp(i, 0, lastIndex);
                Transform spawnPoint = trailSpawnPoints[index];
                if (spawnPoint == null)
                    continue;

                trail.transform.position = spawnPoint.position;
                trail.transform.rotation = ignorePlayerRollForTrails ? noRollRotation : spawnPoint.rotation;
            }
        }

        private void EnsureTrailReferences()
        {
            if ((glideTrails == null || glideTrails.Length == 0) && player != null)
                glideTrails = player.GetComponentsInChildren<TrailRenderer>(true);

            if (glideTrails == null || glideTrails.Length == 0)
                return;
        }

        private void ResolveTrailSpawnPoints()
        {
            if (trailSpawnPoints != null && trailSpawnPoints.Length > 0)
            {
                bool hasNull = false;
                for (int i = 0; i < trailSpawnPoints.Length; i++)
                {
                    if (trailSpawnPoints[i] == null)
                    {
                        hasNull = true;
                        break;
                    }
                }

                if (!hasNull)
                    return;
            }

            if (!autoResolveTrailSpawnPoints)
                return;

            if (trailSpawnRoot == null && player != null)
            {
                Transform found = null;

                if (!string.IsNullOrWhiteSpace(trailSpawnRootName))
                {
                    Transform[] all = player.GetComponentsInChildren<Transform>(true);
                    for (int i = 0; i < all.Length; i++)
                    {
                        Transform candidate = all[i];
                        if (candidate != null && candidate.name == trailSpawnRootName)
                        {
                            found = candidate;
                            break;
                        }
                    }
                }

                if (found != null)
                    trailSpawnRoot = found;
            }

            if (trailSpawnRoot == null)
                return;

            System.Collections.Generic.List<Transform> points = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < trailSpawnRoot.childCount; i++)
            {
                Transform child = trailSpawnRoot.GetChild(i);
                if (child == null)
                    continue;

                if (!string.IsNullOrEmpty(trailSpawnPointNameFilter))
                {
                    if (!child.name.Contains(trailSpawnPointNameFilter))
                        continue;
                }

                points.Add(child);
            }

            if (points.Count == 0)
            {
                for (int i = 0; i < trailSpawnRoot.childCount; i++)
                {
                    Transform child = trailSpawnRoot.GetChild(i);
                    if (child != null)
                        points.Add(child);
                }
            }

            if (points.Count > 0)
                trailSpawnPoints = points.ToArray();
        }

        private void CacheTrailPropertyIds()
        {
            trailPrimaryColorId = Shader.PropertyToID(trailPrimaryColorProperty);
            trailSecondaryColorId = Shader.PropertyToID(trailSecondaryColorProperty);
            if (trailPropertyBlock == null)
                trailPropertyBlock = new MaterialPropertyBlock();
        }

        private void ApplyTrailMaterialColors(TrailRenderer trail, Color primary, Color secondary)
        {
            if (trail == null)
                return;

            if (trailPropertyBlock == null)
                trailPropertyBlock = new MaterialPropertyBlock();

            trail.GetPropertyBlock(trailPropertyBlock);
            trailPropertyBlock.SetColor(trailPrimaryColorId, primary);
            trailPropertyBlock.SetColor(trailSecondaryColorId, secondary);
            trail.SetPropertyBlock(trailPropertyBlock);
        }

    }
}
