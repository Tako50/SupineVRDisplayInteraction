using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum DisplayLayoutPreset
{
    NoOcclusion,
    PartialOcclusion,
    StrongOcclusion
}

[ExecuteAlways]
[DisallowMultipleComponent]
/// <summary>
/// HMD姿勢を基準に2枚の仮想ディスプレイ配置を決める。
/// 実験中は参加者が自由に動かすのではなく、プリセットとHMD正面リセットだけで配置を固定する。
/// </summary>
public class DisplayLayoutManager : MonoBehaviour
{
    private static readonly Vector2 BasePhysicalSizeMeters = new Vector2(0.80f, 0.45f);
    private static readonly Vector2 BasePixelSize = new Vector2(800f, 450f);

    [Header("References")]
    [SerializeField] private Camera hmdCamera;
    [SerializeField] private Transform displaysRoot;
    [SerializeField] private DisplaySurface displayAFront;
    [SerializeField] private DisplaySurface displayBBack;

    [Header("Layout")]
    [SerializeField] private DisplayLayoutPreset initialPreset = DisplayLayoutPreset.StrongOcclusion;
    [SerializeField] private bool useFixedSceneLayout = false;
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool resetLayoutOnXrRecenter = true;
    [SerializeField] private bool logLayoutResetEvents = true;
    [SerializeField] private DisplayLayoutConfig noOcclusion = DisplayLayoutConfig.NoOcclusionDefaults();
    [SerializeField] private DisplayLayoutConfig partialOcclusion = DisplayLayoutConfig.PartialOcclusionDefaults();
    [SerializeField] private DisplayLayoutConfig strongOcclusion = DisplayLayoutConfig.StrongOcclusionDefaults();

    private static readonly List<XRInputSubsystem> InputSubsystems = new List<XRInputSubsystem>();

    private DisplayLayoutPreset currentPreset;
    private bool hasRuntimeAnchor;
    private Vector3 anchorPosition;
    private Vector3 anchorForward = Vector3.forward;
    private Vector3 anchorUp = Vector3.up;

    public DisplayLayoutPreset InitialPreset
    {
        get => initialPreset;
        set
        {
            initialPreset = value;
            ApplyLayout(initialPreset);
        }
    }

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
        initialPreset = DisplayLayoutPreset.StrongOcclusion;
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

        if (!Application.isPlaying && applyOnStart && !useFixedSceneLayout)
        {
#if UNITY_EDITOR
            if (BuildPipeline.isBuildingPlayer)
            {
                return;
            }
#endif
            ApplyLayout(initialPreset);
        }
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

        // Play中は開始時またはリセンター時のHMD姿勢を基準に固定し、毎フレーム頭へ追従させない。
        if (Application.isPlaying && !hasRuntimeAnchor)
        {
            CaptureCurrentHmdAsLayoutAnchor();
        }

        DisplayLayoutConfig layout = GetLayout(preset);

        if (Application.isPlaying)
        {
            ApplyDisplayPose(displayAFront.transform, anchorPosition, anchorForward, anchorUp, layout.DisplayAFront);
            ApplyDisplayPose(displayBBack.transform, anchorPosition, anchorForward, anchorUp, layout.DisplayBBack);
        }
        else
        {
            ApplyDisplayPose(displayAFront.transform, hmd.position, hmd.forward, hmd.up, layout.DisplayAFront);
            ApplyDisplayPose(displayBBack.transform, hmd.position, hmd.forward, hmd.up, layout.DisplayBBack);
        }

        ApplyDisplayScale(displayAFront, layout.DisplayAFront);
        ApplyDisplayScale(displayBBack, layout.DisplayBBack);
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
            Debug.Log($"[DisplayLayoutManager] reset layout anchor from HMD. preset={currentPreset}, forward={anchorForward}");
        }
    }

    public void ResetLayoutFromCurrentHmd(DisplayLayoutPreset preset)
    {
        currentPreset = preset;
        ResetLayoutFromCurrentHmd();
    }

    private void CaptureCurrentHmdAsLayoutAnchor()
    {
        AutoAssignReferences();
        Transform hmd = hmdCamera != null ? hmdCamera.transform : null;
        if (hmd == null)
        {
            return;
        }

        anchorPosition = hmd.position;
        anchorForward = NormalizeOrFallback(hmd.forward, Vector3.forward);
        anchorUp = NormalizeOrFallback(hmd.up, Vector3.up);
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
        if (!resetLayoutOnXrRecenter)
        {
            return;
        }

        ResetLayoutFromCurrentHmd();
    }

    private void EnsurePresetDefaults()
    {
        noOcclusion ??= DisplayLayoutConfig.NoOcclusionDefaults();
        partialOcclusion ??= DisplayLayoutConfig.PartialOcclusionDefaults();
        strongOcclusion ??= DisplayLayoutConfig.StrongOcclusionDefaults();
    }

    private void ResetPresetDefaults()
    {
        noOcclusion = DisplayLayoutConfig.NoOcclusionDefaults();
        partialOcclusion = DisplayLayoutConfig.PartialOcclusionDefaults();
        strongOcclusion = DisplayLayoutConfig.StrongOcclusionDefaults();
    }

    private DisplayLayoutConfig GetLayout(DisplayLayoutPreset preset)
    {
        switch (preset)
        {
            case DisplayLayoutPreset.NoOcclusion:
                return noOcclusion;
            case DisplayLayoutPreset.PartialOcclusion:
                return partialOcclusion;
            case DisplayLayoutPreset.StrongOcclusion:
            default:
                return strongOcclusion;
        }
    }

    private static void ApplyDisplayPose(
        Transform display,
        Vector3 origin,
        Vector3 forward,
        Vector3 up,
        DisplayPlacementConfig config)
    {
        forward = NormalizeOrFallback(forward, Vector3.forward);
        up = NormalizeOrFallback(up, Vector3.up);
        Vector3 right = Vector3.Cross(up, forward);
        if (right.sqrMagnitude < 0.0001f)
        {
            right = Vector3.Cross(Vector3.up, forward);
        }

        right = NormalizeOrFallback(right, Vector3.right);
        up = Vector3.Cross(forward, right).normalized;

        // HMD基準のforward/right/upで、距離・横・縦オフセットをワールド座標へ変換する。
        Vector3 targetPosition = origin
            + forward * config.DistanceFromHmd
            + right * config.HorizontalOffset
            + up * config.VerticalOffset;

        Vector3 directionFromHmd = targetPosition - origin;
        // まずディスプレイがHMD側を向く姿勢を作り、そのあとプリセットのpitch/yaw/rollを足す。
        Quaternion hmdFacingRotation = directionFromHmd.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(directionFromHmd.normalized, up)
            : Quaternion.LookRotation(forward, up);

        Quaternion rotationOffset = Quaternion.Euler(
            config.PitchDegrees,
            config.YawDegrees,
            config.RollDegrees);

        display.SetPositionAndRotation(targetPosition, hmdFacingRotation * rotationOffset);
    }

    private static Vector3 NormalizeOrFallback(Vector3 value, Vector3 fallback)
    {
        return value.sqrMagnitude > 0.0001f ? value.normalized : fallback;
    }

    private static void ApplyDisplayScale(DisplaySurface display, DisplayPlacementConfig config)
    {
        float clampedScale = Mathf.Max(0.01f, config.Scale);
        display.transform.localScale = Vector3.one;
        display.SetSize(BasePhysicalSizeMeters * clampedScale, BasePixelSize * clampedScale);
    }
}

[System.Serializable]
public class DisplayLayoutConfig
{
    [SerializeField] private DisplayPlacementConfig displayAFront = DisplayPlacementConfig.FrontDefaults();
    [SerializeField] private DisplayPlacementConfig displayBBack = DisplayPlacementConfig.BackDefaults();

    public DisplayPlacementConfig DisplayAFront => displayAFront;
    public DisplayPlacementConfig DisplayBBack => displayBBack;

    public static DisplayLayoutConfig NoOcclusionDefaults()
    {
        // 上下配置のみ。距離差と角度差をなくして、遮蔽が起きない確認用にする。
        DisplayLayoutConfig config = new DisplayLayoutConfig();
        config.displayAFront = DisplayPlacementConfig.FrontDefaults();
        config.displayAFront.DistanceFromHmd = 1.25f;
        config.displayAFront.VerticalOffset = -0.45f;
        config.displayAFront.HorizontalOffset = 0f;
        config.displayAFront.PitchDegrees = 0f;
        config.displayBBack = DisplayPlacementConfig.BackDefaults();
        config.displayBBack.DistanceFromHmd = 1.25f;
        config.displayBBack.VerticalOffset = 0.45f;
        config.displayBBack.HorizontalOffset = 0f;
        config.displayBBack.PitchDegrees = 0f;
        return config;
    }

    public static DisplayLayoutConfig PartialOcclusionDefaults()
    {
        // 上下配置に少しだけ前後差をつける。角度差はつけず、Strongより弱い遮蔽確認に使う。
        DisplayLayoutConfig config = new DisplayLayoutConfig();
        config.displayAFront = DisplayPlacementConfig.FrontDefaults();
        config.displayAFront.DistanceFromHmd = 1.15f;
        config.displayAFront.VerticalOffset = -0.30f;
        config.displayAFront.HorizontalOffset = 0f;
        config.displayAFront.PitchDegrees = 0f;
        config.displayBBack = DisplayPlacementConfig.BackDefaults();
        config.displayBBack.DistanceFromHmd = 1.45f;
        config.displayBBack.VerticalOffset = 0.20f;
        config.displayBBack.HorizontalOffset = 0f;
        config.displayBBack.PitchDegrees = 0f;
        return config;
    }

    public static DisplayLayoutConfig StrongOcclusionDefaults()
    {
        DisplayLayoutConfig config = new DisplayLayoutConfig();
        config.displayAFront = DisplayPlacementConfig.FrontDefaults();
        config.displayBBack = DisplayPlacementConfig.BackDefaults();
        return config;
    }
}

[System.Serializable]
public class DisplayPlacementConfig
{
    [SerializeField] private float distanceFromHmd = 1.15f;
    [SerializeField] private float verticalOffset = -0.35f;
    [SerializeField] private float horizontalOffset;
    [SerializeField] private float scale = 0.75f;
    [SerializeField] private float pitchDegrees = 10f;
    [SerializeField] private float yawDegrees;
    [SerializeField] private float rollDegrees;

    public float DistanceFromHmd
    {
        get => distanceFromHmd;
        set => distanceFromHmd = value;
    }

    public float VerticalOffset
    {
        get => verticalOffset;
        set => verticalOffset = value;
    }

    public float HorizontalOffset
    {
        get => horizontalOffset;
        set => horizontalOffset = value;
    }

    public float Scale
    {
        get => scale;
        set => scale = value;
    }

    public float PitchDegrees
    {
        get => pitchDegrees;
        set => pitchDegrees = value;
    }

    public float YawDegrees
    {
        get => yawDegrees;
        set => yawDegrees = value;
    }

    public float RollDegrees
    {
        get => rollDegrees;
        set => rollDegrees = value;
    }

    public static DisplayPlacementConfig FrontDefaults()
    {
        return new DisplayPlacementConfig
        {
            distanceFromHmd = 1.15f,
            verticalOffset = -0.35f,
            horizontalOffset = 0f,
            scale = 0.75f,
            pitchDegrees = 10f,
            yawDegrees = 0f,
            rollDegrees = 0f
        };
    }

    public static DisplayPlacementConfig BackDefaults()
    {
        return new DisplayPlacementConfig
        {
            distanceFromHmd = 1.90f,
            verticalOffset = 0.15f,
            horizontalOffset = 0f,
            scale = 1.25f,
            pitchDegrees = -5f,
            yawDegrees = 0f,
            rollDegrees = 0f
        };
    }
}
