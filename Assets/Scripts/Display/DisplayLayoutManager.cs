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
    [Header("References")]
    [SerializeField] private Camera hmdCamera;
    [SerializeField] private Transform displaysRoot;
    [SerializeField] private DisplaySurface displayAFront;
    [SerializeField] private DisplaySurface displayBBack;

    [Header("Layout")]
    [SerializeField] private DisplayLayoutPreset initialPreset = DisplayLayoutPreset.StrongOcclusion;
    [SerializeField] private bool applyOnStart = true;

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
        AutoAssignReferences();

        Transform hmd = hmdCamera != null ? hmdCamera.transform : null;
        if (hmd == null || displayAFront == null || displayBBack == null)
        {
            return;
        }

        LayoutPose frontPose;
        LayoutPose backPose;
        GetPresetPoses(preset, out frontPose, out backPose);

        ApplyDisplayPose(displayAFront.transform, hmd, frontPose);
        ApplyDisplayPose(displayBBack.transform, hmd, backPose);

        displayAFront.SetSize(frontPose.sizeMeters, frontPose.pixelSize);
        displayBBack.SetSize(backPose.sizeMeters, backPose.pixelSize);
    }

    private static void ApplyDisplayPose(Transform display, Transform hmd, LayoutPose pose)
    {
        Vector3 targetPosition = hmd.position
            + hmd.forward * pose.distanceMeters
            + hmd.right * pose.horizontalOffsetMeters
            + hmd.up * pose.verticalOffsetMeters;

        Quaternion baseRotation = Quaternion.LookRotation(hmd.forward, hmd.up);
        Quaternion rotationOffset = Quaternion.Euler(pose.rotationOffsetEuler);
        display.SetPositionAndRotation(targetPosition, baseRotation * rotationOffset);
    }

    private static void GetPresetPoses(DisplayLayoutPreset preset, out LayoutPose front, out LayoutPose back)
    {
        front = new LayoutPose(
            horizontalOffsetMeters: -0.18f,
            verticalOffsetMeters: -0.18f,
            distanceMeters: 1.10f,
            rotationOffsetEuler: Vector3.zero,
            sizeMeters: new Vector2(0.70f, 0.40f),
            pixelSize: new Vector2(700f, 400f));

        back = new LayoutPose(
            horizontalOffsetMeters: 0.18f,
            verticalOffsetMeters: 0.10f,
            distanceMeters: 1.55f,
            rotationOffsetEuler: Vector3.zero,
            sizeMeters: new Vector2(1.10f, 0.62f),
            pixelSize: new Vector2(1100f, 620f));

        switch (preset)
        {
            case DisplayLayoutPreset.NoOcclusion:
                front.horizontalOffsetMeters = -0.55f;
                front.verticalOffsetMeters = -0.22f;
                front.rotationOffsetEuler = Vector3.zero;
                back.horizontalOffsetMeters = 0.55f;
                back.verticalOffsetMeters = 0.18f;
                back.rotationOffsetEuler = Vector3.zero;
                break;
            case DisplayLayoutPreset.PartialOcclusion:
                front.horizontalOffsetMeters = -0.18f;
                front.verticalOffsetMeters = -0.18f;
                front.rotationOffsetEuler = Vector3.zero;
                back.horizontalOffsetMeters = 0.18f;
                back.verticalOffsetMeters = 0.10f;
                back.rotationOffsetEuler = Vector3.zero;
                break;
            case DisplayLayoutPreset.StrongOcclusion:
                front.horizontalOffsetMeters = -0.04f;
                front.verticalOffsetMeters = -0.16f;
                front.rotationOffsetEuler = Vector3.zero;
                back.horizontalOffsetMeters = 0.04f;
                back.verticalOffsetMeters = 0.02f;
                back.rotationOffsetEuler = Vector3.zero;
                break;
        }
    }

    private struct LayoutPose
    {
        public float horizontalOffsetMeters;
        public float verticalOffsetMeters;
        public float distanceMeters;
        public Vector3 rotationOffsetEuler;
        public Vector2 sizeMeters;
        public Vector2 pixelSize;

        public LayoutPose(float horizontalOffsetMeters, float verticalOffsetMeters, float distanceMeters, Vector3 rotationOffsetEuler, Vector2 sizeMeters, Vector2 pixelSize)
        {
            this.horizontalOffsetMeters = horizontalOffsetMeters;
            this.verticalOffsetMeters = verticalOffsetMeters;
            this.distanceMeters = distanceMeters;
            this.rotationOffsetEuler = rotationOffsetEuler;
            this.sizeMeters = sizeMeters;
            this.pixelSize = pixelSize;
        }
    }
}
