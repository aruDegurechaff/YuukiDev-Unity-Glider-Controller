using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YuukiDev.Controller;

namespace YuukiDev.OtherScripts
{
    [DisallowMultipleComponent]
    public class CameraProximityVfxController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private PlayerController player;
        [SerializeField] private ProximityChecker proximityChecker;
        [SerializeField] private Volume targetVolume;

        [Header("Runtime Resolve")]
        [SerializeField] private float referenceResolveInterval = 0.5f;

        [Header("Game Camera Output")]
        [SerializeField] private bool forceEnableCameraPostProcessing = true;

        [Header("Volume Auto Setup")]
        [SerializeField] private bool createRuntimeVolumeIfMissing = true;
        [SerializeField] private string runtimeVolumeObjectName = "Camera Runtime VFX Volume";
        [SerializeField] private float runtimeVolumePriority = 100f;

        [Header("Proximity Colors")]
        [SerializeField] private Color farColor = new Color(0.78f, 0.95f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float farWeight = 0.1f;
        [SerializeField] private Color midColor = new Color(1f, 0.88f, 0.42f, 1f);
        [SerializeField, Range(0f, 1f)] private float midWeight = 0.3f;
        [SerializeField] private Color closeColor = new Color(1f, 0.35f, 0.35f, 1f);
        [SerializeField, Range(0f, 1f)] private float closeWeight = 0.6f;

        [Header("Post Process Strength")]
        [SerializeField] private float maxSaturationBoost = 25f;
        [SerializeField] private float maxExposureBoost = 0.22f;
        [SerializeField] private float blendSpeed = 7.5f;

        private ColorAdjustments colorAdjustments;
        private VolumeProfile runtimeProfile;
        private UniversalAdditionalCameraData cameraData;
        private float nextResolveTime;

        private Color currentFilterColor = Color.white;
        private float currentSaturation;
        private float currentExposure;

        private void Awake()
        {
            ResolveReferences(true);
            EnsureVolumeSetup();
            ApplyDefaultsImmediately();
        }

        private void Update()
        {
            ResolveReferences(false);
            EnsureGameCameraOutput();

            float baseIntensity = EvaluateProximityIntensity(out Color proximityColor);
            Color targetColor = Color.Lerp(Color.white, proximityColor, baseIntensity);
            float targetSaturation = maxSaturationBoost * baseIntensity;
            float targetExposure = maxExposureBoost * baseIntensity;

            float smoothing = 1f - Mathf.Exp(-Mathf.Max(0.01f, blendSpeed) * Time.deltaTime);
            currentFilterColor = Color.Lerp(currentFilterColor, targetColor, smoothing);
            currentSaturation = Mathf.Lerp(currentSaturation, targetSaturation, smoothing);
            currentExposure = Mathf.Lerp(currentExposure, targetExposure, smoothing);

            ApplyToVolume(currentFilterColor, currentSaturation, currentExposure);
        }

        private void ResolveReferences(bool force)
        {
            if (!force && Time.unscaledTime < nextResolveTime)
                return;

            nextResolveTime = Time.unscaledTime + Mathf.Max(0.1f, referenceResolveInterval);

            if (targetCamera == null)
                targetCamera = GetComponent<Camera>();
            if (targetCamera == null)
                targetCamera = GetComponentInChildren<Camera>(true);
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (player == null)
            {
                CameraFollowAndRotate follow = GetComponentInParent<CameraFollowAndRotate>();
                if (follow != null && follow.playerController != null)
                    player = follow.playerController;
            }

            if (player == null)
                player = FindAnyObjectByType<PlayerController>();

            if (proximityChecker == null && player != null)
                proximityChecker = player.GetComponentInChildren<ProximityChecker>(true);

            if (targetVolume == null && targetCamera != null)
                targetVolume = targetCamera.GetComponent<Volume>();

            if (targetVolume == null && createRuntimeVolumeIfMissing)
                EnsureVolumeSetup();

            if (cameraData == null && targetCamera != null)
                targetCamera.TryGetComponent(out cameraData);
        }

        private void EnsureGameCameraOutput()
        {
            if (!forceEnableCameraPostProcessing || cameraData == null)
                return;

            if (!cameraData.renderPostProcessing)
                cameraData.renderPostProcessing = true;
        }

        private float EvaluateProximityIntensity(out Color proximityColor)
        {
            proximityColor = farColor;
            float weight = 0f;

            if (proximityChecker == null)
                return 0f;

            if (player != null && player.IsPhantomGraceActive)
                return 0f;

            if (proximityChecker.IsClose)
            {
                proximityColor = closeColor;
                weight = closeWeight;
            }
            else if (proximityChecker.IsMid)
            {
                proximityColor = midColor;
                weight = midWeight;
            }
            else if (proximityChecker.IsFar)
            {
                proximityColor = farColor;
                weight = farWeight;
            }

            return Mathf.Clamp01(weight);
        }

        private void EnsureVolumeSetup()
        {
            if (targetVolume == null && createRuntimeVolumeIfMissing && targetCamera != null)
            {
                Transform existingChild = targetCamera.transform.Find(runtimeVolumeObjectName);
                if (existingChild != null)
                    targetVolume = existingChild.GetComponent<Volume>();

                if (targetVolume == null)
                {
                    GameObject volumeObject = new GameObject(runtimeVolumeObjectName);
                    volumeObject.transform.SetParent(targetCamera.transform, false);
                    targetVolume = volumeObject.AddComponent<Volume>();
                }
            }

            if (targetVolume == null)
                return;

            targetVolume.isGlobal = true;
            targetVolume.priority = runtimeVolumePriority;

            VolumeProfile profile = targetVolume.profile;
            if (profile == null)
            {
                runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                targetVolume.profile = runtimeProfile;
                profile = runtimeProfile;
            }

            if (profile == null)
                return;

            if (!profile.TryGet(out colorAdjustments))
                colorAdjustments = profile.Add<ColorAdjustments>(true);

            colorAdjustments.colorFilter.overrideState = true;
            colorAdjustments.saturation.overrideState = true;
            colorAdjustments.postExposure.overrideState = true;
        }

        private void ApplyDefaultsImmediately()
        {
            currentFilterColor = Color.white;
            currentSaturation = 0f;
            currentExposure = 0f;
            ApplyToVolume(currentFilterColor, currentSaturation, currentExposure);
        }

        private void ApplyToVolume(Color filterColor, float saturation, float exposure)
        {
            if (colorAdjustments == null)
                return;

            colorAdjustments.colorFilter.value = filterColor;
            colorAdjustments.saturation.value = saturation;
            colorAdjustments.postExposure.value = exposure;
        }
    }
}
