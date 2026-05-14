using UnityEngine;

public enum GazeSource
{
    HmdForward,
    EyeTracking
}

[DisallowMultipleComponent]
public class GazeProvider : MonoBehaviour
{
    [SerializeField] private GazeSource gazeSource = GazeSource.HmdForward;
    [SerializeField] private Camera hmdCamera;
    [SerializeField] private Transform eyeTrackingRaySource;
    [SerializeField] private bool warnWhenEyeTrackingFallsBack = true;

    private bool warnedAboutFallback;

    public GazeSource CurrentGazeSource => gazeSource;

    private void Awake()
    {
        if (hmdCamera == null)
        {
            hmdCamera = Camera.main;
        }
    }

    public Ray GetRay()
    {
        if (gazeSource == GazeSource.EyeTracking && TryGetEyeTrackingRay(out Ray eyeRay))
        {
            return eyeRay;
        }

        if (gazeSource == GazeSource.EyeTracking && warnWhenEyeTrackingFallsBack && !warnedAboutFallback)
        {
            warnedAboutFallback = true;
            Debug.LogWarning("[GazeProvider] EyeTracking is selected, but no eye-tracking adapter is connected. Falling back to HMD forward for development only.");
        }

        Camera cameraForRay = hmdCamera != null ? hmdCamera : Camera.main;
        Transform source = cameraForRay != null ? cameraForRay.transform : transform;
        return new Ray(source.position, source.forward);
    }

    public bool IsEyeTrackingAvailable()
    {
        return eyeTrackingRaySource != null;
    }

    private bool TryGetEyeTrackingRay(out Ray ray)
    {
        if (eyeTrackingRaySource != null)
        {
            ray = new Ray(eyeTrackingRaySource.position, eyeTrackingRaySource.forward);
            return true;
        }

        ray = default;
        return false;
    }
}
