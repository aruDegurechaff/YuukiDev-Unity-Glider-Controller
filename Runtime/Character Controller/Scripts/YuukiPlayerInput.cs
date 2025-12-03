using UnityEngine;
using UnityEngine.InputSystem;

namespace YuukiDev.Controller
{
    [DefaultExecutionOrder(-2)]
    public class YuukiPlayerInput : MonoBehaviour, CameraCtrls.ICameraActions
    {
        public CameraCtrls CameraCtrls { get; private set; }

        public Vector2 LookInput { get; private set; }

        private bool usingController = false;

        public CameraFollowAndRotate cameraFollow;

        private void OnEnable()
        {
            CameraCtrls = new CameraCtrls();
            CameraCtrls.Enable();

            CameraCtrls.Camera.SetCallbacks(this);
        }

        private void OnDisable()
        {
            CameraCtrls.Camera.RemoveCallbacks(this);
            CameraCtrls.Disable();
        }

        // LOOK INPUT
        private void OnLook(InputAction.CallbackContext context)
        {
            LookInput = context.ReadValue<Vector2>();

            // Detect the device used
            var device = context.control.device;
            usingController = device is Gamepad;

            cameraFollow.SetDevice(usingController);
            cameraFollow.SetLookInput(LookInput);
        }

        void CameraCtrls.ICameraActions.OnLook(InputAction.CallbackContext context)
        {
            OnLook(context);
        }
    }
}
