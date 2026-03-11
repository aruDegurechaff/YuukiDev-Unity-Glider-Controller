using UnityEngine;
using YuukiDev.Input;
using YuukiDev.OtherScripts;
using static YuukiDev.Input.IGlideState;

namespace YuukiDev.Controller
{
    /*
     * Camera follow, rotation, and FOV control
     * by: YuukiDev
     *
     * Keeps the view responsive to player speed and input.
     */
    [DefaultExecutionOrder(-1)]
    public class CameraFollowAndRotate : MonoBehaviour
    {
        [Header("Target Components")]
        [SerializeField] private Camera cam;
        public MovementTracker movementTracker;
        public PlayerController playerController;
        public YuukiPlayerInput input;
        

        [Header("Follow Settings")]
        public Transform target;
        public float smoothTime = 0.15f;
        public Vector3 offset;

        private Vector3 smoothVelocity;

        [Header("FOV Settings")]
        public float baseFOV = 60f;
        [SerializeField] private float speedFovRange = 12f;
        [SerializeField] private float maxFOV = 90f;
        [SerializeField] private float fovBoostAmount = 10f; // how much FOV changes on speed-up
        [SerializeField] private float fovSlowAmount = 5f;   // how much FOV changes on slow-down
        [SerializeField] private float fovSmoothTime = 0.08f;

        private float fovVelocity;
        private float temporaryFovBoost = 0f;
        private float temporaryFovTimer = 0f;
        private float temporaryFovDuration = 0f;

        [Header("Rotation Settings")]
        public float sensitivity = 0.25f;
        public float maxVerticalAngle = 80f;
        public float maxHorizontalAngle = 180f;

        [Header("Controller Settings")]
        private bool isController;
        public float controllerSensitivity = 1f;
        public float mouseSensitivity = 0.05f;
        public float controllerDeadzone = 0.15f;

        [Header("Dynamic Offset")]
        public Vector3 baseOffset;

        [Tooltip("Offset for mouse movement.")]
        public float mouseOffsetAmount = 0.5f;

        [Tooltip("Offset for controller movement (weaker).")]
        public float controllerOffsetAmount = 0.25f;

        public float offsetSpeed = 0.2f; // slow movement
        private Vector3 dynamicOffset;
        private Vector3 dynamicOffsetVelocity;

        private float yaw;   // Mouse X
        private float pitch; // Mouse Y

        private float yawVelocity;
        private float pitchVelocity;
        public float rotationSmoothTime = 0.02f;

        private Vector2 lookInput;
        private IGlideState currentState;

        public void SetLookInput(Vector2 input)
        {
            lookInput = input;
        }

        public void SetDevice(bool controller)
        {
            isController = controller;
        }

        private void Awake()
        {
            if (cam == null)
                cam = GetComponentInChildren<Camera>();

            if (cam != null && baseFOV <= 0f)
                baseFOV = cam.fieldOfView;
        }

        public void TriggerTemporaryFovBoost(float extraFov, float duration)
        {
            if (extraFov <= 0f || duration <= 0f)
                return;

            temporaryFovBoost = Mathf.Max(temporaryFovBoost, extraFov);
            temporaryFovTimer = Mathf.Max(temporaryFovTimer, duration);
            temporaryFovDuration = Mathf.Max(temporaryFovDuration, duration);
        }

        private void LateUpdate()
        {
            FollowTarget();
            RotateCamera();

            UpdateTemporaryFovBoost();
            FOVDynamicUpdate();
        }

        private void FollowTarget()
        {
            UpdateDynamicOffset();

            Vector3 desiredPos = target.position + baseOffset + dynamicOffset;

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPos,
                ref smoothVelocity,
                smoothTime
            );
        }

        private void RotateCamera()
        {
            float s = isController ? controllerSensitivity : mouseSensitivity;

            Vector2 processedInput = lookInput;

            // Apply deadzone only for controller
            if (isController)
            {
                if (processedInput.magnitude < controllerDeadzone)
                    processedInput = Vector2.zero;
            }

            float targetYaw = yaw + processedInput.x * s;
            float targetPitch = pitch - processedInput.y * s;

            targetPitch = Mathf.Clamp(targetPitch, -maxVerticalAngle, maxVerticalAngle);

            yaw = Mathf.SmoothDamp(yaw, targetYaw, ref yawVelocity, rotationSmoothTime);
            pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVelocity, rotationSmoothTime);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void UpdateDynamicOffset()
        {
            float horizontal = lookInput.x;
            float vertical = lookInput.y;

            // Choose correct offset amount based on device
            float chosenOffset = isController ? controllerOffsetAmount : mouseOffsetAmount;

            Vector3 targetOffset = Vector3.zero;

            // Horizontal look shifts X
            if (Mathf.Abs(horizontal) > 0.1f)
                targetOffset.x = Mathf.Sign(horizontal) * chosenOffset;

            // Downward look shifts Y
            if (vertical > 0.1f)
                targetOffset.y = -chosenOffset;

            // Smooth blend toward target offset
            dynamicOffset = Vector3.SmoothDamp(
                dynamicOffset,
                targetOffset,
                ref dynamicOffsetVelocity,
                offsetSpeed
            );
        }

        private void FOVDynamicUpdate()
        {
            if (cam == null)
                return;

            float targetFOV = baseFOV;

            if (playerController != null)
            {
                float speedFactor = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(playerController.SpeedNormalized));
                targetFOV += Mathf.Max(0f, speedFovRange) * speedFactor;

                if (playerController.CurrentGlideMode == PlayerController.GlideMode.SpeedingUp && playerController.CurrentBoost > 0f)
                {
                    // Keep boost FOV visible immediately, then scale stronger as speed ramps up.
                    float boostVisualFactor = Mathf.Lerp(0.55f, 1f, speedFactor);
                    targetFOV += fovBoostAmount * boostVisualFactor;
                }
                else if (playerController.CurrentGlideMode == PlayerController.GlideMode.SlowingDown)
                {
                    float slowFactor = 1f - speedFactor;
                    targetFOV -= fovSlowAmount * Mathf.Lerp(0.45f, 1f, slowFactor);
                }
            }

            if (temporaryFovTimer > 0f)
            {
                float fade = Mathf.Clamp01(temporaryFovTimer / Mathf.Max(temporaryFovDuration, 0.01f));
                targetFOV += temporaryFovBoost * fade;
            }

            float effectiveMaxFov = Mathf.Max(maxFOV, baseFOV);
            targetFOV = Mathf.Min(targetFOV, effectiveMaxFov);

            cam.fieldOfView = Mathf.SmoothDamp(
                cam.fieldOfView,
                targetFOV,
                ref fovVelocity,
                fovSmoothTime
            );
        }

        private void UpdateTemporaryFovBoost()
        {
            if (temporaryFovTimer <= 0f)
                return;

            temporaryFovTimer -= Time.deltaTime;
            if (temporaryFovTimer <= 0f)
            {
                temporaryFovTimer = 0f;
                temporaryFovBoost = 0f;
                temporaryFovDuration = 0f;
            }
        }
    }
}
