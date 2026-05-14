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
    [SerializeField] private EyeTrackingRayAdapter eyeTrackingAdapter;
    [SerializeField] private Transform eyeTrackingRaySource;
    [SerializeField] private bool warnWhenEyeTrackingFallsBack = true;

    private bool warnedAboutFallback;
    private GazeSource activeGazeSource = GazeSource.HmdForward;

    public GazeSource CurrentGazeSource => activeGazeSource;

    private void Awake()
    {
        if (hmdCamera == null)
        {
            hmdCamera = Camera.main;
        }
    }

    public Ray GetGazeRay()
    {
        if (gazeSource == GazeSource.EyeTracking && TryGetEyeTrackingRay(out Ray eyeRay))
        {
            activeGazeSource = GazeSource.EyeTracking;
            return eyeRay;
        }

        if (gazeSource == GazeSource.EyeTracking && warnWhenEyeTrackingFallsBack && !warnedAboutFallback)
        {
            warnedAboutFallback = true;
            Debug.LogWarning("[GazeProvider] EyeTracking is selected, but no eye-tracking adapter is connected. Falling back to HMD forward for development only.");
        }

        Camera cameraForRay = hmdCamera != null ? hmdCamera : Camera.main;
        Transform source = cameraForRay != null ? cameraForRay.transform : transform;
        activeGazeSource = GazeSource.HmdForward;
        return new Ray(source.position, source.forward);
    }

    public Ray GetRay()
    {
        return GetGazeRay();
    }

    public bool IsEyeTrackingAvailable()
    {
        if (eyeTrackingAdapter != null)
        {
            return eyeTrackingAdapter.IsEyeTrackingAvailable;
        }

        return eyeTrackingRaySource != null;
    }

    private bool TryGetEyeTrackingRay(out Ray ray)
    {
        if (eyeTrackingAdapter != null && eyeTrackingAdapter.TryGetRay(out ray))
        {
            return true;
        }

        if (eyeTrackingRaySource != null)
        {
            ray = new Ray(eyeTrackingRaySource.position, eyeTrackingRaySource.forward);
            return true;
        }

        ray = default;
        return false;
    }
}
