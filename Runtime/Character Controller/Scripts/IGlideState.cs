using YuukiDev.Controller;

namespace YuukiDev.Input
{
    /*
     * This makes the Controller use States 
     * instead of multiple if statements.
     * Makes it less complex and therefore
     * more lightweight.
     * 
     * This handles movement inputs which turns them into a state,
     * rather than continously checking if the player is doing something.
     * This is a better scalable alternative.
    */
    public interface IGlideState
    {
        void Enter(PlayerController player);
        void Tick(PlayerController player);
        void Exit(PlayerController player);

        public class NormalGlideState : IGlideState
        {
            public void Enter(PlayerController player) { }

            public void Tick(PlayerController player)
            {
                player.ApplyNaturalMovement();
                player.RegenBoosters(1f);
            }

            public void Exit(PlayerController player) { }
        }

        public class BoostingGlideState : IGlideState
        {
            public void Enter(PlayerController player) { }

            public void Tick(PlayerController player)
            {
                if (player.CurrentBoost <= 0f)
                {
                    return;
                }

                player.ApplyNaturalMovement();
                player.ApplyBoostAcceleration();
                player.ConsumeBoosters();
            }

            public void Exit(PlayerController player) { }
        }

        public class SlowGlideState : IGlideState
        {
            public void Enter(PlayerController player) { }

            public void Tick(PlayerController player)
            {
                player.ApplyNaturalMovement();
                player.ApplySlowDown();
                // Slower regen rate when in slowed state.
                player.RegenBoosters(0.85f);
            }

            public void Exit(PlayerController player) { }
        }

        // To be Added...
        // Will handle the power up that makes the player phase through objects. AKA "Phantom Grace Token" which will remove colliders for 7 seconds
        // Other power ups will not be states. Because some of them are just a small burst of speed and increases score multiplier.
        public abstract class PermeationState : IGlideState
        {
            public void Enter(PlayerController player) { }

            public void Tick(PlayerController player) 
            {
                // Will add stuff at PlayerControlls.cs that makes this happen...
            }

            public abstract void Exit(PlayerController player);
        }
    }
}