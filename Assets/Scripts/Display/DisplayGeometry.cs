using UnityEngine;

public struct DisplayAnchorFrame
{
    public Vector3 Position;
    public Vector3 Forward;
    public Vector3 Right;
    public Vector3 Up;

    public static DisplayAnchorFrame FromTransform(Transform anchor)
    {
        if (anchor == null)
        {
            return new DisplayAnchorFrame
            {
                Position = Vector3.zero,
                Forward = Vector3.forward,
                Right = Vector3.right,
                Up = Vector3.up
            };
        }

        Vector3 forward = DisplayGeometry.NormalizeOrFallback(anchor.forward, Vector3.forward);
        Vector3 up = DisplayGeometry.NormalizeOrFallback(anchor.up, Vector3.up);
        Vector3 right = DisplayGeometry.NormalizeOrFallback(Vector3.Cross(up, forward), anchor.right);
        up = DisplayGeometry.NormalizeOrFallback(Vector3.Cross(forward, right), Vector3.up);

        return new DisplayAnchorFrame
        {
            Position = anchor.position,
            Forward = forward,
            Right = right,
            Up = up
        };
    }

    public Vector3 TransformOffset(Vector3 localOffset)
    {
        return Position
            + Right * localOffset.x
            + Up * localOffset.y
            + Forward * localOffset.z;
    }
}

/// <summary>
/// Display配置で共有する、状態を持たない幾何計算。
/// </summary>
public static class DisplayGeometry
{
    public const float DefaultApparentWidthDegrees = 40f;
    public const float DefaultApparentHeightDegrees = 22.5f;

    public static readonly Vector2 DefaultPhysicalSizeMeters = new Vector2(0.8f, 0.45f);
    public static readonly Vector2 DefaultCanvasPixelSize = new Vector2(800f, 450f);
    public static readonly Vector2 DefaultAspectRatio = new Vector2(16f, 9f);

    public static Vector2 ComputePhysicalSize(
        float distanceMeters,
        float apparentWidthDegrees,
        float apparentHeightDegrees,
        bool preserveAspectRatio = false,
        Vector2 aspectRatio = default)
    {
        float distance = Mathf.Max(0.01f, distanceMeters);
        float widthDegrees = Mathf.Max(0.1f, apparentWidthDegrees);
        float heightDegrees = Mathf.Max(0.1f, apparentHeightDegrees);
        float width = 2f * distance * Mathf.Tan(widthDegrees * 0.5f * Mathf.Deg2Rad);
        float height = 2f * distance * Mathf.Tan(heightDegrees * 0.5f * Mathf.Deg2Rad);

        if (preserveAspectRatio && aspectRatio.x > 0f && aspectRatio.y > 0f)
        {
            height = width * aspectRatio.y / aspectRatio.x;
        }

        return new Vector2(width, height);
    }

    public static Vector3 ComputeAngularDirection(
        Vector3 forward,
        Vector3 right,
        Vector3 up,
        float horizontalAngleDegrees,
        float verticalAngleDegrees)
    {
        Vector3 normalizedForward = NormalizeOrFallback(forward, Vector3.forward);
        Vector3 normalizedRight = NormalizeOrFallback(right, Vector3.right);
        Vector3 normalizedUp = NormalizeOrFallback(up, Vector3.up);
        float horizontal = Mathf.Tan(horizontalAngleDegrees * Mathf.Deg2Rad);
        float vertical = Mathf.Tan(verticalAngleDegrees * Mathf.Deg2Rad);
        return NormalizeOrFallback(
            normalizedForward + normalizedRight * horizontal + normalizedUp * vertical,
            normalizedForward);
    }

    public static void ComputeDisplayPose(
        DisplayAnchorFrame anchor,
        float distanceMeters,
        float horizontalAngleDegrees,
        float verticalAngleDegrees,
        Vector3 rotationOffsetDegrees,
        Vector3 anchorOffsetMeters,
        out Vector3 position,
        out Quaternion rotation)
    {
        Vector3 viewDirection = ComputeAngularDirection(
            anchor.Forward,
            anchor.Right,
            anchor.Up,
            horizontalAngleDegrees,
            verticalAngleDegrees);
        position = anchor.TransformOffset(anchorOffsetMeters)
            + viewDirection * Mathf.Max(0.01f, distanceMeters);
        rotation = Quaternion.LookRotation(viewDirection, anchor.Up)
            * Quaternion.Euler(rotationOffsetDegrees);
    }

    public static Vector2 ValidPixelSize(Vector2 pixelSize)
    {
        return pixelSize.x > 0f && pixelSize.y > 0f
            ? pixelSize
            : DefaultCanvasPixelSize;
    }

    public static Vector3 NormalizeOrFallback(Vector3 value, Vector3 fallback)
    {
        return value.sqrMagnitude > 0.000001f ? value.normalized : fallback.normalized;
    }
}
