using UnityEngine;
using UnityEngine.InputSystem;
using YuukiDev.Controller;

namespace YuukiDev.Input
{
    [DefaultExecutionOrder(-2)]
    public class YuukiPlayerInput : MonoBehaviour, MovementCtrls.ICameraActions, MovementCtrls.IMovementActions
    {
        private bool usingController = false;

        [Header("Speed Controls")]
        public bool speedUpIsToggle = false;
        public bool slowDownIsToggle = false;

        // These booleans tell the PlayerController what the player is doing
        public bool IsSpeedingUp { get; private set; }
        public bool IsSlowingDown { get; private set; }

        public MovementCtrls MovementCtrls { get; private set; }

        public Vector2 LookInput { get; private set; }

        public CameraFollowAndRotate cameraFollow;

        private void OnEnable()
        {
            MovementCtrls = new MovementCtrls();
            MovementCtrls.Enable();

            MovementCtrls.Camera.SetCallbacks(this);
        }

        private void OnDisable()
        {
            MovementCtrls.Camera.RemoveCallbacks(this);
            MovementCtrls.Disable();
        }

        // LOOK INPUT (Mouse Vector 2 Input)
        private void OnLook(InputAction.CallbackContext context)
        {
            LookInput = context.ReadValue<Vector2>();

            // Detect the device used
            var device = context.control.device;
            usingController = device is Gamepad;

            cameraFollow.SetDevice(usingController);
            cameraFollow.SetLookInput(LookInput);
        }

        // NEW SPEEDUP & SLOWDOWN INPUTS
        void MovementCtrls.ICameraActions.OnLook(InputAction.CallbackContext context)
        {
            OnLook(context);
        }

        // Left mouse & left shoulder
        public void OnSpeedup(InputAction.CallbackContext context)
        {
            if (speedUpIsToggle)
            {
                if (context.performed)
                    IsSpeedingUp = !IsSpeedingUp;
            }
            else
            {
                if (context.performed) IsSpeedingUp = true;
                if (context.canceled) IsSpeedingUp = false;
            }
        }

        // Right mouse & right shoulder
        public void OnSlowdown(InputAction.CallbackContext context)
        {
            if (slowDownIsToggle)
            {
                if (context.performed)
                    IsSlowingDown = !IsSlowingDown;
            }
            else
            {
                if (context.performed) IsSlowingDown = true;
                if (context.canceled) IsSlowingDown = false;
            }
        }
    }
}
