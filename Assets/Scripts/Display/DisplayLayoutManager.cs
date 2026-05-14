using UnityEngine;

public enum DisplayLayoutPreset
{
    NoOcclusion,
    PartialOcclusion,
    StrongOcclusion
}

[ExecuteAlways]
[DisallowMultipleComponent]
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
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private DisplayLayoutConfig noOcclusion = DisplayLayoutConfig.NoOcclusionDefaults();
    [SerializeField] private DisplayLayoutConfig partialOcclusion = DisplayLayoutConfig.PartialOcclusionDefaults();
    [SerializeField] private DisplayLayoutConfig strongOcclusion = DisplayLayoutConfig.StrongOcclusionDefaults();

    public DisplayLayoutPreset InitialPreset
    {
        get => initialPreset;
        set
        {
            initialPreset = value;
            ApplyLayout(initialPreset);
        }
    }

    private void Reset()
    {
        AutoAssignReferences();
        initialPreset = DisplayLayoutPreset.StrongOcclusion;
        ResetPresetDefaults();
    }

    private void Start()
    {
        if (applyOnStart)
        {
            ApplyLayout(initialPreset);
        }
    }

    private void OnValidate()
    {
        EnsurePresetDefaults();
        AutoAssignReferences();

        if (!Application.isPlaying)
        {
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

        Transform hmd = hmdCamera != null ? hmdCamera.transform : null;
        if (hmd == null || displayAFront == null || displayBBack == null)
        {
            return;
        }

        DisplayLayoutConfig layout = GetLayout(preset);

        ApplyDisplayPose(displayAFront.transform, hmd, layout.DisplayAFront);
        ApplyDisplayPose(displayBBack.transform, hmd, layout.DisplayBBack);

        ApplyDisplayScale(displayAFront, layout.DisplayAFront);
        ApplyDisplayScale(displayBBack, layout.DisplayBBack);
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

    private static void ApplyDisplayPose(Transform display, Transform hmd, DisplayPlacementConfig config)
    {
        Vector3 targetPosition = hmd.position
            + hmd.forward * config.DistanceFromHmd
            + hmd.right * config.HorizontalOffset
            + hmd.up * config.VerticalOffset;

        Vector3 directionFromHmd = targetPosition - hmd.position;
        Quaternion hmdFacingRotation = directionFromHmd.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(directionFromHmd.normalized, hmd.up)
            : Quaternion.LookRotation(hmd.forward, hmd.up);

        Quaternion rotationOffset = Quaternion.Euler(
            config.PitchDegrees,
            config.YawDegrees,
            config.RollDegrees);

        display.SetPositionAndRotation(targetPosition, hmdFacingRotation * rotationOffset);
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
        DisplayLayoutConfig config = new DisplayLayoutConfig();
        config.displayAFront = DisplayPlacementConfig.FrontDefaults();
        config.displayAFront.HorizontalOffset = -0.55f;
        config.displayBBack = DisplayPlacementConfig.BackDefaults();
        config.displayBBack.HorizontalOffset = 0.55f;
        return config;
    }

    public static DisplayLayoutConfig PartialOcclusionDefaults()
    {
        DisplayLayoutConfig config = new DisplayLayoutConfig();
        config.displayAFront = DisplayPlacementConfig.FrontDefaults();
        config.displayAFront.HorizontalOffset = -0.18f;
        config.displayBBack = DisplayPlacementConfig.BackDefaults();
        config.displayBBack.HorizontalOffset = 0.18f;
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
