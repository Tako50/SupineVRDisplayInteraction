using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public enum DisplayLayoutPreset
{
    UpDownDepth = 0,
    LeftRight = 1,
    UpDown = 2
}

[ExecuteAlways]
[DisallowMultipleComponent]
/// <summary>
/// HMD姿勢を基準に、距離と水平・垂直角度から2枚のディスプレイ配置を決める。
/// 実験中は配置を固定し、明示的なプリセット変更かHMD基準リセット時だけ再配置する。
/// </summary>
public class DisplayLayoutManager : MonoBehaviour
{
    private static readonly List<XRInputSubsystem> InputSubsystems = new List<XRInputSubsystem>();

    [Header("References")]
    [SerializeField] private Camera hmdCamera;
    [SerializeField] private Transform displaysRoot;
    [SerializeField] private DisplaySurface displayAFront;
    [SerializeField] private DisplaySurface displayBBack;

    [Header("Layout")]
    [SerializeField] private DisplayLayoutPreset initialPreset = DisplayLayoutPreset.UpDownDepth;
    [SerializeField] private bool useFixedSceneLayout;
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool resetLayoutOnXrRecenter = true;
    [SerializeField] private bool logLayoutResetEvents = true;

    [Header("Apparent Display Size")]
    [SerializeField] private float apparentWidthDegrees = DisplayGeometry.DefaultApparentWidthDegrees;
    [SerializeField] private float apparentHeightDegrees = DisplayGeometry.DefaultApparentHeightDegrees;
    [SerializeField] private bool preserveDisplayAspectRatio;
    [SerializeField] private Vector2 displayAspectRatio = DisplayGeometry.DefaultAspectRatio;
    [SerializeField] private Vector2 canvasPixelSize = DisplayGeometry.DefaultCanvasPixelSize;

    [Header("Layout 1: Up Down + Depth")]
    [SerializeField] private DisplayLayoutConfig upDownDepth = DisplayLayoutConfig.UpDownDepthDefaults();

    [Header("Layout 2: Left Right")]
    [SerializeField] private DisplayLayoutConfig leftRight = DisplayLayoutConfig.LeftRightDefaults();

    [Header("Layout 3: Up Down")]
    [SerializeField] private DisplayLayoutConfig upDown = DisplayLayoutConfig.UpDownDefaults();

    private DisplayLayoutPreset currentPreset;
    private bool hasRuntimeAnchor;
    private DisplayAnchorFrame runtimeAnchor;

    public DisplayLayoutPreset InitialPreset
    {
        get => initialPreset;
        set
        {
            initialPreset = value;
            ApplyLayout(initialPreset);
        }
    }

    public DisplayLayoutPreset CurrentPreset => currentPreset;

    public void SetApplyOnStart(bool enabled)
    {
        applyOnStart = enabled;
    }

    public void SetUseFixedSceneLayout(bool enabled)
    {
        useFixedSceneLayout = enabled;
    }

    private void Reset()
    {
        AutoAssignReferences();
        initialPreset = DisplayLayoutPreset.UpDownDepth;
        ResetPresetDefaults();
    }

    private void OnEnable()
    {
        SubscribeToXrRecenterEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromXrRecenterEvents();
    }

    private void Start()
    {
        if (applyOnStart && !useFixedSceneLayout)
        {
            CaptureCurrentHmdAsLayoutAnchor();
            ApplyLayout(initialPreset);
        }
    }

    private void OnValidate()
    {
        EnsurePresetDefaults();
        AutoAssignReferences();
    }

    [ContextMenu("Apply Selected Layout Now")]
    public void ApplySelectedLayoutNow()
    {
        ApplyLayout(initialPreset);
    }

    public void AutoAssignReferences()
    {
        if (hmdCamera == null)
        {
            hmdCamera = Camera.main;
        }

        if (displaysRoot == null)
        {
            Transform foundRoot = transform.Find("Displays");
            displaysRoot = foundRoot != null ? foundRoot : transform;
        }

        if (displayAFront == null && displaysRoot != null)
        {
            Transform displayA = displaysRoot.Find("Display_A_Front");
            if (displayA != null)
            {
                displayAFront = displayA.GetComponent<DisplaySurface>();
            }
        }

        if (displayBBack == null && displaysRoot != null)
        {
            Transform displayB = displaysRoot.Find("Display_B_Back");
            if (displayB != null)
            {
                displayBBack = displayB.GetComponent<DisplaySurface>();
            }
        }
    }

    public void ApplyLayout(DisplayLayoutPreset preset)
    {
        EnsurePresetDefaults();
        AutoAssignReferences();
        currentPreset = preset;

        if (useFixedSceneLayout)
        {
            Debug.Log($"[DisplayLayoutManager] fixed scene layout is enabled. Ignored preset apply: {preset}");
            return;
        }

        Transform hmd = hmdCamera != null ? hmdCamera.transform : null;
        if (hmd == null || displayAFront == null || displayBBack == null)
        {
            return;
        }

        if (Application.isPlaying && !hasRuntimeAnchor)
        {
            CaptureCurrentHmdAsLayoutAnchor();
        }

        DisplayAnchorFrame anchor = Application.isPlaying
            ? runtimeAnchor
            : DisplayAnchorFrame.FromTransform(hmd);
        DisplayLayoutConfig layout = GetLayout(preset);

        ApplyDisplay(displayAFront, anchor, layout.DisplayA);
        ApplyDisplay(displayBBack, anchor, layout.DisplayB);
        Debug.Log($"[DisplayLayoutManager] applied angular layout={preset}");
    }

    public void ResetLayoutFromCurrentHmd()
    {
        if (useFixedSceneLayout)
        {
            Debug.Log("[DisplayLayoutManager] fixed scene layout is enabled. Ignored XR recenter layout reset.");
            return;
        }

        CaptureCurrentHmdAsLayoutAnchor();
        ApplyLayout(currentPreset);
        if (logLayoutResetEvents)
        {
            Debug.Log($"[DisplayLayoutManager] reset layout anchor from HMD. preset={currentPreset}, forward={runtimeAnchor.Forward}");
        }
    }

    public void ResetLayoutFromCurrentHmd(DisplayLayoutPreset preset)
    {
        currentPreset = preset;
        ResetLayoutFromCurrentHmd();
    }

    private void ApplyDisplay(
        DisplaySurface display,
        DisplayAnchorFrame anchor,
        DisplayPlacementConfig placement)
    {
        DisplayGeometry.ComputeDisplayPose(
            anchor,
            placement.DistanceMeters,
            placement.HorizontalAngleDegrees,
            placement.VerticalAngleDegrees,
            placement.RotationOffsetDegrees,
            Vector3.zero,
            out Vector3 position,
            out Quaternion rotation);

        display.transform.SetPositionAndRotation(position, rotation);
        display.transform.localScale = Vector3.one;
        display.SetSize(
            DisplayGeometry.ComputePhysicalSize(
                placement.DistanceMeters,
                apparentWidthDegrees,
                apparentHeightDegrees,
                preserveDisplayAspectRatio,
                displayAspectRatio),
            ValidPixelSize());
    }

    private Vector2 ValidPixelSize()
    {
        return DisplayGeometry.ValidPixelSize(canvasPixelSize);
    }

    private void CaptureCurrentHmdAsLayoutAnchor()
    {
        AutoAssignReferences();
        Transform hmd = hmdCamera != null ? hmdCamera.transform : null;
        if (hmd == null)
        {
            return;
        }

        runtimeAnchor = DisplayAnchorFrame.FromTransform(hmd);
        hasRuntimeAnchor = true;
    }

    private void SubscribeToXrRecenterEvents()
    {
        if (!Application.isPlaying || !resetLayoutOnXrRecenter)
        {
            return;
        }

        SubsystemManager.GetInstances(InputSubsystems);
        for (int i = 0; i < InputSubsystems.Count; i++)
        {
            InputSubsystems[i].trackingOriginUpdated -= HandleTrackingOriginUpdated;
            InputSubsystems[i].trackingOriginUpdated += HandleTrackingOriginUpdated;
        }
    }

    private void UnsubscribeFromXrRecenterEvents()
    {
        SubsystemManager.GetInstances(InputSubsystems);
        for (int i = 0; i < InputSubsystems.Count; i++)
        {
            InputSubsystems[i].trackingOriginUpdated -= HandleTrackingOriginUpdated;
        }
    }

    private void HandleTrackingOriginUpdated(XRInputSubsystem subsystem)
    {
        if (resetLayoutOnXrRecenter)
        {
            ResetLayoutFromCurrentHmd();
        }
    }

    private void EnsurePresetDefaults()
    {
        upDownDepth ??= DisplayLayoutConfig.UpDownDepthDefaults();
        leftRight ??= DisplayLayoutConfig.LeftRightDefaults();
        upDown ??= DisplayLayoutConfig.UpDownDefaults();
    }

    private void ResetPresetDefaults()
    {
        upDownDepth = DisplayLayoutConfig.UpDownDepthDefaults();
        leftRight = DisplayLayoutConfig.LeftRightDefaults();
        upDown = DisplayLayoutConfig.UpDownDefaults();
    }

    private DisplayLayoutConfig GetLayout(DisplayLayoutPreset preset)
    {
        switch (preset)
        {
            case DisplayLayoutPreset.LeftRight:
                return leftRight;
            case DisplayLayoutPreset.UpDown:
                return upDown;
            case DisplayLayoutPreset.UpDownDepth:
            default:
                return upDownDepth;
        }
    }

}

[System.Serializable]
public class DisplayLayoutConfig
{
    [SerializeField] private DisplayPlacementConfig displayA = new DisplayPlacementConfig();
    [SerializeField] private DisplayPlacementConfig displayB = new DisplayPlacementConfig();

    public DisplayPlacementConfig DisplayA => displayA;
    public DisplayPlacementConfig DisplayB => displayB;

    public DisplayLayoutConfig(DisplayPlacementConfig displayA, DisplayPlacementConfig displayB)
    {
        this.displayA = displayA;
        this.displayB = displayB;
    }

    public static DisplayLayoutConfig UpDownDepthDefaults()
    {
        // Layout 1: 従来のT1RayOcclusionLayoutProbeと同じ配置。
        return new DisplayLayoutConfig(
            new DisplayPlacementConfig(1.15f, 0f, -25f),
            new DisplayPlacementConfig(1.90f, 0f, 0f));
    }

    public static DisplayLayoutConfig LeftRightDefaults()
    {
        return new DisplayLayoutConfig(
            new DisplayPlacementConfig(1.25f, -22f, 0f),
            new DisplayPlacementConfig(1.25f, 22f, 0f));
    }

    public static DisplayLayoutConfig UpDownDefaults()
    {
        return new DisplayLayoutConfig(
            new DisplayPlacementConfig(1.25f, 0f, -18f),
            new DisplayPlacementConfig(1.25f, 0f, 18f));
    }

}

[System.Serializable]
public class DisplayPlacementConfig
{
    [SerializeField] private float distanceMeters = 1.25f;
    [SerializeField] private float horizontalAngleDegrees;
    [SerializeField] private float verticalAngleDegrees;
    [SerializeField] private Vector3 rotationOffsetDegrees = Vector3.zero;

    public float DistanceMeters => distanceMeters;
    public float HorizontalAngleDegrees => horizontalAngleDegrees;
    public float VerticalAngleDegrees => verticalAngleDegrees;
    public Vector3 RotationOffsetDegrees => rotationOffsetDegrees;

    public DisplayPlacementConfig()
    {
    }

    public DisplayPlacementConfig(
        float distanceMeters,
        float horizontalAngleDegrees,
        float verticalAngleDegrees,
        Vector3 rotationOffsetDegrees = default)
    {
        this.distanceMeters = distanceMeters;
        this.horizontalAngleDegrees = horizontalAngleDegrees;
        this.verticalAngleDegrees = verticalAngleDegrees;
        this.rotationOffsetDegrees = rotationOffsetDegrees;
    }
}
