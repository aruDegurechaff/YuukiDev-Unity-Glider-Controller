namespace YuukiDev.Controller
{
    public partial class PlayerController
    {
        #region Setup
        private void ResolveCollisionGameOverHandler()
        {
            collisionGameOverHandler = GetComponent<PlayerCollisionAndGameOverHandler>();
            if (collisionGameOverHandler == null)
                collisionGameOverHandler = gameObject.AddComponent<PlayerCollisionAndGameOverHandler>();

            collisionGameOverHandler.Configure(
                onlyObstacleCollisionsAreLethal,
                obstacleTag,
                freezeTimeOnGameOver,
                reviveCollisionImmunityDuration);
            collisionGameOverHandler.Initialize(this, rb, input);
        }
        #endregion
    }
}
