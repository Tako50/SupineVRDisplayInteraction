using System;
using UnityEngine;

public enum LayoutPreferenceCondition
{
    LeftRight,
    UpDown,
    UpDownDepth
}

[Serializable]
public class LayoutPreferenceDisplayPlacement
{
    [SerializeField] private float distanceMeters = 1.25f;
    [SerializeField] private float horizontalAngleDegrees;
    [SerializeField] private float verticalAngleDegrees;
    [SerializeField, HideInInspector] private float horizontalMeters;
    [SerializeField, HideInInspector] private float verticalMeters;
    [SerializeField] private Vector3 rotationOffsetDegrees = Vector3.zero;

    public float DistanceMeters => distanceMeters;
    public float HorizontalAngleDegrees => horizontalAngleDegrees;
    public float VerticalAngleDegrees => verticalAngleDegrees;
    public Vector3 RotationOffsetDegrees => rotationOffsetDegrees;

    public LayoutPreferenceDisplayPlacement(float distanceMeters, float horizontalAngleDegrees, float verticalAngleDegrees)
    {
        this.distanceMeters = distanceMeters;
        this.horizontalAngleDegrees = horizontalAngleDegrees;
        this.verticalAngleDegrees = verticalAngleDegrees;
        horizontalMeters = 0f;
        verticalMeters = 0f;
        rotationOffsetDegrees = Vector3.zero;
    }

    public void MigrateLegacyMetersToAnglesIfNeeded()
    {
        if (distanceMeters <= 0f)
        {
            return;
        }

        if (Mathf.Approximately(horizontalAngleDegrees, 0f) && !Mathf.Approximately(horizontalMeters, 0f))
        {
            horizontalAngleDegrees = Mathf.Atan2(horizontalMeters, distanceMeters) * Mathf.Rad2Deg;
            horizontalMeters = 0f;
        }

        if (Mathf.Approximately(verticalAngleDegrees, 0f) && !Mathf.Approximately(verticalMeters, 0f))
        {
            verticalAngleDegrees = Mathf.Atan2(verticalMeters, distanceMeters) * Mathf.Rad2Deg;
            verticalMeters = 0f;
        }
    }
}

[Serializable]
public class LayoutPreferenceLayout
{
    [SerializeField] private LayoutPreferenceCondition condition = LayoutPreferenceCondition.LeftRight;
    [SerializeField] private string conditionName = "LeftRight";
    [SerializeField] private string displayALabel = "Display A";
    [SerializeField] private string displayBLabel = "Display B";
    [SerializeField] private Vector3 conditionCenterOffsetMeters = Vector3.zero;
    [SerializeField] private LayoutPreferenceDisplayPlacement displayA =
        new LayoutPreferenceDisplayPlacement(1.25f, -0.55f, 0f);
    [SerializeField] private LayoutPreferenceDisplayPlacement displayB =
        new LayoutPreferenceDisplayPlacement(1.25f, 0.55f, 0f);

    public LayoutPreferenceCondition Condition => condition;
    public string ConditionName => conditionName;
    public string DisplayALabel => displayALabel;
    public string DisplayBLabel => displayBLabel;
    public Vector3 ConditionCenterOffsetMeters => conditionCenterOffsetMeters;
    public LayoutPreferenceDisplayPlacement DisplayA => displayA;
    public LayoutPreferenceDisplayPlacement DisplayB => displayB;

    public LayoutPreferenceLayout(
        LayoutPreferenceCondition condition,
        string conditionName,
        string displayALabel,
        string displayBLabel,
        LayoutPreferenceDisplayPlacement displayA,
        LayoutPreferenceDisplayPlacement displayB,
        Vector3 conditionCenterOffsetMeters = default)
    {
        this.condition = condition;
        this.conditionName = conditionName;
        this.displayALabel = displayALabel;
        this.displayBLabel = displayBLabel;
        this.displayA = displayA;
        this.displayB = displayB;
        this.conditionCenterOffsetMeters = conditionCenterOffsetMeters;
    }

    public void MigrateLegacyMetersToAnglesIfNeeded()
    {
        displayA?.MigrateLegacyMetersToAnglesIfNeeded();
        displayB?.MigrateLegacyMetersToAnglesIfNeeded();
    }

    public static LayoutPreferenceLayout[] CreateDefaultLayouts()
    {
        return new[]
        {
            new LayoutPreferenceLayout(
                LayoutPreferenceCondition.LeftRight,
                "LeftRight",
                "Display A / Left",
                "Display B / Right",
                new LayoutPreferenceDisplayPlacement(1.25f, -22f, 0f),
                new LayoutPreferenceDisplayPlacement(1.25f, 22f, 0f),
                new Vector3(0f, 0.18f, 0f)),
            new LayoutPreferenceLayout(
                LayoutPreferenceCondition.UpDown,
                "UpDown",
                "Display A / Lower",
                "Display B / Upper",
                new LayoutPreferenceDisplayPlacement(1.25f, 0f, -18f),
                new LayoutPreferenceDisplayPlacement(1.25f, 0f, 18f),
                new Vector3(0f, 0.18f, 0f)),
            new LayoutPreferenceLayout(
                LayoutPreferenceCondition.UpDownDepth,
                "UpDownDepth",
                "Display A / Lower Front",
                "Display B / Upper Back",
                new LayoutPreferenceDisplayPlacement(1.15f, 0f, -17f),
                new LayoutPreferenceDisplayPlacement(1.55f, 0f, 12f),
                new Vector3(0f, 0.18f, 0f))
        };
    }
}
