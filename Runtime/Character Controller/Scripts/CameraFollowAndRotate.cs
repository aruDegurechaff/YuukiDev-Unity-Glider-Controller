using UnityEngine;
using YuukiDev.Input;
using YuukiDev.OtherScripts;
using static YuukiDev.Input.IGlideState;

namespace YuukiDev.Controller
{
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
        [SerializeField] private float fovBoostAmount = 10f; // how much FOV changes on speed-up
        [SerializeField] private float fovSlowAmount = 5f;   // how much FOV changes on slow-down
        [SerializeField] private float fovSmoothTime = 0.08f;

        private float fovVelocity;

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

        private void LateUpdate()
        {
            FollowTarget();
            RotateCamera();

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
            float targetFOV = baseFOV;

            if (input.IsSpeedingUp && playerController.canBoost)
            {
                float boostFactor = Mathf.SmoothStep(0f, 1f, playerController.BoostNormalized);
                targetFOV += fovBoostAmount * boostFactor;
            }
            else if (input.IsSlowingDown)
            {
                targetFOV -= fovSlowAmount;
            }

            cam.fieldOfView = Mathf.SmoothDamp(
                cam.fieldOfView,
                targetFOV,
                ref fovVelocity,
                fovSmoothTime
            );
        }
    }
}
