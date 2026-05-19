using UnityEngine;

#pragma warning disable 0414

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

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
    [SerializeField] private bool logPoseChanges = false;
    [SerializeField] private float poseLogIntervalSeconds = 1f;
    [SerializeField] private bool requestMetaQuestEyeTrackingPermission = true;
    [SerializeField] private string metaQuestEyeTrackingPermission = "com.oculus.permission.EYE_TRACKING";

    private bool wasAvailable;
    private bool permissionRequested;
    private float nextPoseLogTime;
    private Vector3 previousForward;
    private bool hasPreviousForward;

    public bool IsEyeTrackingAvailable { get; private set; }
    public bool HasEyeTrackingPermission { get; private set; } = true;
    public string LastDeviceName { get; private set; } = "None";
    public float LastIsTrackedValue { get; private set; }
    public Vector3 LastRayOrigin { get; private set; }
    public Vector3 LastRayDirection { get; private set; } = Vector3.forward;
    public float LastForwardDeltaDegrees { get; private set; }
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
        RequestEyeTrackingPermissionIfNeeded();
        UpdateEyeTrackingRay();
    }

    private void RequestEyeTrackingPermissionIfNeeded()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!requestMetaQuestEyeTrackingPermission || string.IsNullOrEmpty(metaQuestEyeTrackingPermission))
        {
            HasEyeTrackingPermission = true;
            return;
        }

        HasEyeTrackingPermission = Permission.HasUserAuthorizedPermission(metaQuestEyeTrackingPermission);
        if (!HasEyeTrackingPermission && !permissionRequested)
        {
            permissionRequested = true;
            Permission.RequestUserPermission(metaQuestEyeTrackingPermission);
            Debug.Log($"[EyeTrackingRayAdapter] requested permission={metaQuestEyeTrackingPermission}");
        }
#else
        HasEyeTrackingPermission = true;
#endif
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
        LastDeviceName = eyeGazeDevice != null ? eyeGazeDevice.displayName : "None";
        LastIsTrackedValue = eyeGazeDevice != null ? eyeGazeDevice.pose.isTracked.ReadValue() : 0f;
        bool available = HasEyeTrackingPermission && eyeGazeDevice != null && LastIsTrackedValue > 0f;
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
            LastRayOrigin = raySource.position;
            LastRayDirection = raySource.forward;

            if (hasPreviousForward)
            {
                LastForwardDeltaDegrees = Vector3.Angle(previousForward, LastRayDirection);
            }

            previousForward = LastRayDirection;
            hasPreviousForward = true;
            LogPoseChangeIfNeeded();
        }
#else
        IsEyeTrackingAvailable = false;
        LastDeviceName = "InputSystem disabled";
        LastIsTrackedValue = 0f;
#endif

        if (logAvailabilityChanges && IsEyeTrackingAvailable != wasAvailable)
        {
            Debug.Log($"[EyeTrackingRayAdapter] eyeTrackingAvailable={IsEyeTrackingAvailable}, permission={HasEyeTrackingPermission}, device={LastDeviceName}, isTracked={LastIsTrackedValue:0.000}");
            wasAvailable = IsEyeTrackingAvailable;
        }
    }

    private void LogPoseChangeIfNeeded()
    {
        if (!logPoseChanges || Time.time < nextPoseLogTime)
        {
            return;
        }

        nextPoseLogTime = Time.time + Mathf.Max(0.1f, poseLogIntervalSeconds);
        Debug.Log($"[EyeTrackingRayAdapter] device={LastDeviceName}, isTracked={LastIsTrackedValue:0.000}, origin={Format(LastRayOrigin)}, direction={Format(LastRayDirection)}, deltaDeg={LastForwardDeltaDegrees:0.000}");
    }

    private static string Format(Vector3 value)
    {
        return $"({value.x:0.000}, {value.y:0.000}, {value.z:0.000})";
    }
}

#pragma warning restore 0414
