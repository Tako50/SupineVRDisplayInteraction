using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.XR.OpenXR.Features.Interactions;
#endif

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class EyeTrackingRayAdapter : MonoBehaviour
{
    [SerializeField] private Transform raySource;
    [SerializeField] private Camera hmdCamera;
    [SerializeField] private bool useHmdPositionWithEyeRotation = false;
    [SerializeField] private bool logAvailabilityChanges = true;

    private bool wasAvailable;

    public bool IsEyeTrackingAvailable { get; private set; }
    public Transform RaySource => raySource;

    private void Awake()
    {
        if (hmdCamera == null)
        {
            hmdCamera = Camera.main;
        }

        if (raySource == null)
        {
            GameObject raySourceObject = new GameObject("EyeTracking_RaySource");
            raySourceObject.transform.SetParent(transform, false);
            raySource = raySourceObject.transform;
        }
    }

    private void Update()
    {
        UpdateEyeTrackingRay();
    }

    public bool TryGetRay(out Ray ray)
    {
        if (IsEyeTrackingAvailable && raySource != null)
        {
            ray = new Ray(raySource.position, raySource.forward);
            return true;
        }

        ray = default;
        return false;
    }

    private void UpdateEyeTrackingRay()
    {
#if ENABLE_INPUT_SYSTEM
        EyeGazeInteraction.EyeGazeDevice eyeGazeDevice = InputSystem.GetDevice<EyeGazeInteraction.EyeGazeDevice>();
        bool available = eyeGazeDevice != null && eyeGazeDevice.pose.isTracked.ReadValue() > 0f;
        IsEyeTrackingAvailable = available;

        if (available && raySource != null)
        {
            Vector3 position = eyeGazeDevice.pose.position.ReadValue();
            Quaternion rotation = eyeGazeDevice.pose.rotation.ReadValue();

            if (useHmdPositionWithEyeRotation && hmdCamera != null)
            {
                position = hmdCamera.transform.position;
            }

            raySource.SetPositionAndRotation(position, rotation);
        }
#else
        IsEyeTrackingAvailable = false;
#endif

        if (logAvailabilityChanges && IsEyeTrackingAvailable != wasAvailable)
        {
            Debug.Log($"[EyeTrackingRayAdapter] eyeTrackingAvailable={IsEyeTrackingAvailable}");
            wasAvailable = IsEyeTrackingAvailable;
        }
    }
}
