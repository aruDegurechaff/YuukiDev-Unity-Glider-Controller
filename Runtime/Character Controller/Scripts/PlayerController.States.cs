using YuukiDev.Input;

namespace YuukiDev.Controller
{
    public partial class PlayerController
    {
        #region State stuff
        // Helper
        public void SwitchState(IGlideState newState)
        {
            currentState?.Exit(this);
            currentState = newState;
            currentState.Enter(this);
        }

        // State Selector
        private void UpdateState()
        {
            bool boostRequestedAndAvailable = input.IsSpeedingUp && CanEnterBoostState();

            IGlideState targetState =
                boostRequestedAndAvailable ? boostingState :
                input.IsSlowingDown ? slowState :
                NormalState;

            GlideMode targetMode =
                boostRequestedAndAvailable ? GlideMode.SpeedingUp :
                input.IsSlowingDown ? GlideMode.SlowingDown :
                GlideMode.Normal;

            if (targetMode != CurrentGlideMode)
            {
                CurrentGlideMode = targetMode;
                GlideModeChanged?.Invoke(CurrentGlideMode);
            }

            if (targetState != currentState)
                SwitchState(targetState);
        }
        #endregion
    }
}
