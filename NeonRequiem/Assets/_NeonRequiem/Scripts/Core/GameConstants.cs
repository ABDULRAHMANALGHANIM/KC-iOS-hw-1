namespace NeonRequiem.Core
{
    /// <summary>Scene names. Each must match a scene file listed in the Build Settings.</summary>
    public static class SceneIds
    {
        public const string Bootstrap = "Bootstrap";
        public const string MovementTest = "MovementTest";
        public const string MobileControlsTest = "MobileControlsTest";
    }

    /// <summary>
    /// Physics layer indices. These mirror ProjectSettings/TagManager.asset; the
    /// project validator and the EditMode tests check that the two stay in sync.
    /// </summary>
    public static class GameLayers
    {
        public const int Default = 0;
        public const int TransparentFX = 1;
        public const int IgnoreRaycast = 2;
        public const int Water = 4;
        public const int UI = 5;
        public const int Player = 6;

        public const string PlayerName = "Player";

        /// <summary>Solid world geometry the player walks on and the camera collides with.</summary>
        public const int WorldGeometryMask = 1 << Default;
    }

    /// <summary>
    /// Script execution order. Input is sampled after the uGUI EventSystem (order 0) has
    /// dispatched this frame's pointer events, the player consumes it afterwards, and
    /// presentation follows. Cameras run in LateUpdate.
    /// </summary>
    public static class ExecutionOrder
    {
        public const int Bootstrap = -1000;
        public const int Runtime = -900;
        public const int InputRouter = 10;
        public const int Player = 20;
        public const int Hud = 30;
    }
}
