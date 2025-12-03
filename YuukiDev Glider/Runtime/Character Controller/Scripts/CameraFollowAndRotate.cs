using UnityEngine;
using UnityEngine.InputSystem;

namespace YuukiDev.Controller
{
    [DefaultExecutionOrder(-1)]
    public class CameraFollowAndRotate : MonoBehaviour
    {
        [Header("Follow Settings")]
        public Transform target;
        public float smoothTime = 0.15f;
        public Vector3 offset;

        private Vector3 smoothVelocity;

        [Header("Rotation Settings")]
        public float sensitivity = 1.5f;
        public float maxVerticalAngle = 80f;
        public float maxHorizontalAngle = 180f;

        [Header("Controller Settings")]
        private bool isController;
        public float controllerSensitivity = 2f;
        public float mouseSensitivity = 1.0f;
        public float controllerDeadzone = 0.15f;

        [Header("Dynamic Offset")]
        public Vector3 baseOffset;

        [Tooltip("Offset for mouse movement.")]
        public float mouseOffsetAmount = 0.5f;

        [Tooltip("Offset for controller movement (weaker).")]
        public float controllerOffsetAmount = 0.25f;

        public float offsetSpeed = 0.5f; // slow movement
        private Vector3 dynamicOffset;
        private Vector3 dynamicOffsetVelocity;

        private float yaw;   // Mouse X
        private float pitch; // Mouse Y

        private float yawVelocity;
        private float pitchVelocity;
        public float rotationSmoothTime = 0.1f;

        private Vector2 lookInput;

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
    }
}
