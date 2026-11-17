namespace NeonRequiem.Cameras
{
    /// <summary>Heading of the gameplay camera, used for camera-relative movement.</summary>
    public interface ICameraYawSource
    {
        /// <summary>Yaw in degrees, clockwise from world +Z when seen from above.</summary>
        float YawDegrees { get; }
    }
}
