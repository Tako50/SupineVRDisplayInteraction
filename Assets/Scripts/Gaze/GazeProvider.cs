using UnityEngine;

public enum GazeSource
{
    HmdForward,
    EyeTracking,
    DebugCameraForward
}

[DisallowMultipleComponent]
/// <summary>
/// 視線Rayの入口。実験ではEyeTrackingを使い、HMD/Camera forwardは開発用フォールバックとしてだけ使う。
/// FocusManagerなどはこのクラスだけを見ればよいようにして、実機視線とEditor検証の差を閉じ込める。
/// </summary>
public class GazeProvider : MonoBehaviour
{
    [SerializeField] private GazeSource gazeSource = GazeSource.HmdForward;
    [SerializeField] private Camera hmdCamera;
    [SerializeField] private EyeTrackingRayAdapter eyeTrackingAdapter;
    [SerializeField] private Transform eyeTrackingRaySource;
    [SerializeField] private bool warnWhenEyeTrackingFallsBack = true;
    [SerializeField] private bool allowRawEyeTrackingRaySourceFallback = false;

    private bool warnedAboutFallback;
    private GazeSource activeGazeSource = GazeSource.HmdForward;

    public GazeSource CurrentGazeSource => activeGazeSource;
    public GazeSource ConfiguredGazeSource => gazeSource;

    private void Awake()
    {
        if (hmdCamera == null)
        {
            hmdCamera = Camera.main;
        }

        if (eyeTrackingAdapter == null)
        {
            eyeTrackingAdapter = FindObjectOfType<EyeTrackingRayAdapter>();
        }
    }

    public Ray GetGazeRay()
    {
        // EditorでQuestなしに検証するためのCamera forwardモード。
        if (gazeSource == GazeSource.DebugCameraForward)
        {
            Camera debugCamera = Camera.main != null ? Camera.main : hmdCamera;
            Transform debugSource = debugCamera != null ? debugCamera.transform : transform;
            activeGazeSource = GazeSource.DebugCameraForward;
            return new Ray(debugSource.position, debugSource.forward);
        }

        // 実験用の本命経路。Adapterが有効でトラッキングできる場合だけEyeTrackingとして返す。
        if (gazeSource == GazeSource.EyeTracking && TryGetEyeTrackingRay(out Ray eyeRay))
        {
            activeGazeSource = GazeSource.EyeTracking;
            return eyeRay;
        }

        if (Application.isPlaying && gazeSource == GazeSource.EyeTracking && warnWhenEyeTrackingFallsBack && !warnedAboutFallback)
        {
            warnedAboutFallback = true;
            Debug.LogWarning("[GazeProvider] EyeTracking is selected, but eye tracking is unavailable or untracked. Falling back to HMD forward for development only.");
        }

        // 視線が取れないときは開発用にHMD forwardへ落とす。ログ上ではHmdForwardとして区別される。
        Camera cameraForRay = hmdCamera != null ? hmdCamera : Camera.main;
        Transform source = cameraForRay != null ? cameraForRay.transform : transform;
        activeGazeSource = GazeSource.HmdForward;
        return new Ray(source.position, source.forward);
    }

    public Ray GetRay()
    {
        return GetGazeRay();
    }

    public void SetGazeSource(GazeSource source)
    {
        if (gazeSource == source)
        {
            return;
        }

        gazeSource = source;
        warnedAboutFallback = false;
        Debug.Log($"[GazeProvider] configuredGazeSource={gazeSource}");
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

        if (eyeTrackingAdapter == null && allowRawEyeTrackingRaySourceFallback && eyeTrackingRaySource != null)
        {
            ray = new Ray(eyeTrackingRaySource.position, eyeTrackingRaySource.forward);
            return true;
        }

        ray = default;
        return false;
    }
}
