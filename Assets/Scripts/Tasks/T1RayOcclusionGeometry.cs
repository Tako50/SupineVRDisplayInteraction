using System.Collections.Generic;
using UnityEngine;

public enum RayOcclusionResult
{
    Clear,
    Occluded,
    Ambiguous
}

public struct DisplayPlane
{
    public Vector3 Center;
    public Vector3 Normal;
    public Vector3 U;
    public Vector3 V;
    public float Width;
    public float Height;

    public float HalfWidth => Width * 0.5f;
    public float HalfHeight => Height * 0.5f;

    public DisplayPlane(Vector3 center, Vector3 normal, Vector3 u, Vector3 v, float width, float height)
    {
        Center = center;
        Normal = DisplayGeometry.NormalizeOrFallback(normal, Vector3.back);
        U = DisplayGeometry.NormalizeOrFallback(u, Vector3.right);
        V = DisplayGeometry.NormalizeOrFallback(v, Vector3.up);
        Width = Mathf.Max(0f, width);
        Height = Mathf.Max(0f, height);
    }

    public Vector3 LocalToWorld(Vector2 local)
    {
        return Center + U * local.x + V * local.y;
    }

    public Vector2 WorldToLocal(Vector3 world)
    {
        Vector3 delta = world - Center;
        return new Vector2(Vector3.Dot(delta, U), Vector3.Dot(delta, V));
    }

    public Vector3[] GetCorners()
    {
        float a = HalfWidth;
        float b = HalfHeight;
        return new[]
        {
            Center + U * -a + V * -b,
            Center + U * a + V * -b,
            Center + U * a + V * b,
            Center + U * -a + V * b
        };
    }

}

public struct T1RayOcclusionLayout
{
    public DisplayPlane D1;
    public DisplayPlane D2;
    public Vector3 Eye;
    public Vector3 Forward;
    public Vector3 U1;
    public Vector3 V1;
    public Vector3 HandCenter;
    public float H1;
    public float H2;
    public float Eta2Degrees;
    public float HandVerticalSign;
    public float ProjectedOcclusionRatio;
    public float SampledOcclusionRatio;
    public float VisualOcclusionRatio;
    public float VisualVerticalGapMeters;
    public bool VisualOcclusionClear;
    public bool D2CenterAngleValid;
    public bool D2BelowD1;
}

public static class T1RayOcclusionGeometry
{
    public const float AlphaHDegrees = DisplayGeometry.DefaultApparentWidthDegrees;
    public const float AlphaVDegrees = DisplayGeometry.DefaultApparentHeightDegrees;
    public const float EyeToElbowMeters = 0.40f;
    public const float ElbowToHandMeters = 0.30f;
    public const float HandSampleOffsetMeters = 0.05f;
    public const float BoundaryExclusionFraction = 0.10f;
    public const float MinimumD2VerticalSeparationDegrees = 22.5f;

    private const float ParallelEpsilon = 0.00001f;
    private const float AreaEpsilon = 0.0001f;
    private const int DefaultOcclusionSampleGrid = 31;

    public static RayOcclusionResult IsRayOccluded(Vector3 controllerPosition, Vector3 targetOnD1, DisplayPlane d2)
    {
        TryClassifyRayToDisplayRange(controllerPosition, targetOnD1, d2, out RayOcclusionResult result, out _);
        return result;
    }

    public static bool TryClassifyRayToDisplayRange(
        Vector3 controllerPosition,
        Vector3 targetOnD1,
        DisplayPlane d2,
        out RayOcclusionResult result,
        out Vector2 localOnD2)
    {
        result = RayOcclusionResult.Clear;
        localOnD2 = Vector2.zero;
        Vector3 rayDelta = targetOnD1 - controllerPosition;
        float denominator = Vector3.Dot(d2.Normal, rayDelta);
        if (Mathf.Abs(denominator) < ParallelEpsilon)
        {
            return false;
        }

        float t2 = Vector3.Dot(d2.Normal, d2.Center - controllerPosition) / denominator;
        if (t2 <= 0f || t2 >= 1f)
        {
            return false;
        }

        Vector3 q2 = controllerPosition + t2 * rayDelta;
        localOnD2 = d2.WorldToLocal(q2);
        float absX = Mathf.Abs(localOnD2.x);
        float absY = Mathf.Abs(localOnD2.y);
        float a2 = d2.HalfWidth;
        float b2 = d2.HalfHeight;

        if (absX > a2 || absY > b2)
        {
            return false;
        }

        // 境界付近は判定が揺れやすいため、安定したOccludedは内側のディスプレイ範囲内だけにする。
        float stableHalfWidth = a2 * (1f - BoundaryExclusionFraction);
        float stableHalfHeight = b2 * (1f - BoundaryExclusionFraction);
        if (absX <= stableHalfWidth && absY <= stableHalfHeight)
        {
            result = RayOcclusionResult.Occluded;
            return true;
        }

        result = RayOcclusionResult.Ambiguous;
        return true;
    }

    public static Vector3 ComputeHandCenter(Vector3 eye, Vector3 forward, Vector3 d1Vertical)
    {
        return ComputeHandCenter(eye, forward, d1Vertical, 1f);
    }

    public static Vector3 ComputeHandCenter(Vector3 eye, Vector3 forward, Vector3 d1Vertical, float verticalSign)
    {
        Vector3 f = DisplayGeometry.NormalizeOrFallback(forward, Vector3.forward);
        Vector3 v1 = DisplayGeometry.NormalizeOrFallback(Vector3.ProjectOnPlane(d1Vertical, f), Vector3.up);
        float signedDirection = verticalSign >= 0f ? 1f : -1f;

        // 目-肘の線分はD1と平行に置く。Unityのv1が上向きの場合、signedDirection=-1で下側の腕姿勢を試せる。
        Vector3 elbow = eye + signedDirection * EyeToElbowMeters * v1;

        // 肘から手へはD1方向へ伸ばすため、目方向ベクトルと手方向ベクトルは直交する。
        return elbow + ElbowToHandMeters * f;
    }

    public static Vector3[] GenerateHandSamples(Vector3 handCenter, Vector3 u1, Vector3 v1, Vector3 forward)
    {
        return GenerateHandSamples(handCenter, u1, v1, forward, false);
    }

    public static Vector3[] GenerateHandSamples(Vector3 handCenter, Vector3 u1, Vector3 v1, Vector3 forward, bool includeCombinations)
    {
        Vector3 u = DisplayGeometry.NormalizeOrFallback(u1, Vector3.right);
        Vector3 v = DisplayGeometry.NormalizeOrFallback(v1, Vector3.up);
        Vector3 f = DisplayGeometry.NormalizeOrFallback(forward, Vector3.forward);
        float offset = HandSampleOffsetMeters;
        if (includeCombinations)
        {
            List<Vector3> samples = new List<Vector3>();
            for (int iu = -1; iu <= 1; iu++)
            {
                for (int iv = -1; iv <= 1; iv++)
                {
                    for (int iforward = -1; iforward <= 1; iforward++)
                    {
                        samples.Add(handCenter + offset * (iu * u + iv * v + iforward * f));
                    }
                }
            }

            return samples.ToArray();
        }

        return new[]
        {
            handCenter,
            handCenter + offset * u,
            handCenter - offset * u,
            handCenter + offset * v,
            handCenter - offset * v,
            handCenter + offset * f,
            handCenter - offset * f
        };
    }

    public static float ComputeProjectedOcclusionRatio(Vector3 controllerPosition, DisplayPlane d1, DisplayPlane d2)
    {
        List<Vector2> projectedCorners = ProjectPlaneCornersToD1(controllerPosition, d1, d2);
        if (projectedCorners.Count < 3)
        {
            return 0f;
        }

        List<Vector2> clipped = ClipPolygonToDisplayRange(projectedCorners, d1.HalfWidth, d1.HalfHeight);
        float intersectionArea = Mathf.Abs(ComputePolygonArea(clipped));
        float d1Area = Mathf.Max(AreaEpsilon, d1.Width * d1.Height);
        return Mathf.Clamp01(intersectionArea / d1Area);
    }

    public static float ComputeProjectedVerticalGapMeters(Vector3 rayOrigin, DisplayPlane d1, DisplayPlane d2)
    {
        List<Vector2> projectedCorners = ProjectPlaneCornersToD1(rayOrigin, d1, d2);
        if (projectedCorners.Count == 0)
        {
            return 0f;
        }

        float minY = projectedCorners[0].y;
        float maxY = projectedCorners[0].y;
        for (int i = 1; i < projectedCorners.Count; i++)
        {
            minY = Mathf.Min(minY, projectedCorners[i].y);
            maxY = Mathf.Max(maxY, projectedCorners[i].y);
        }

        float gapBelow = -d1.HalfHeight - maxY;
        float gapAbove = minY - d1.HalfHeight;
        if (gapBelow >= 0f)
        {
            return gapBelow;
        }

        if (gapAbove >= 0f)
        {
            return gapAbove;
        }

        // 負値は目から見た投影がD1と重なっている量を示す。
        return Mathf.Max(gapBelow, gapAbove);
    }

    public static float ComputeSampledOcclusionRatio(Vector3 controllerPosition, DisplayPlane d1, DisplayPlane d2)
    {
        return ComputeSampledOcclusionRatio(controllerPosition, d1, d2, DefaultOcclusionSampleGrid);
    }

    public static float ComputeSampledOcclusionRatio(Vector3 controllerPosition, DisplayPlane d1, DisplayPlane d2, int samplesPerAxis)
    {
        return ComputeSampledOcclusionRatio(controllerPosition, d1, d2, samplesPerAxis, false);
    }

    public static float ComputeSampledDisplayRangeIntersectionRatio(Vector3 rayOrigin, DisplayPlane d1, DisplayPlane d2)
    {
        return ComputeSampledOcclusionRatio(rayOrigin, d1, d2, DefaultOcclusionSampleGrid, true);
    }

    public static float ComputeSampledDisplayRangeIntersectionRatio(Vector3 rayOrigin, DisplayPlane d1, DisplayPlane d2, int samplesPerAxis)
    {
        return ComputeSampledOcclusionRatio(rayOrigin, d1, d2, samplesPerAxis, true);
    }

    private static float ComputeSampledOcclusionRatio(
        Vector3 rayOrigin,
        DisplayPlane d1,
        DisplayPlane d2,
        int samplesPerAxis,
        bool countAmbiguousAsOccluded)
    {
        int sampleCount = Mathf.Max(3, samplesPerAxis);
        int occluded = 0;
        int total = 0;

        for (int y = 0; y < sampleCount; y++)
        {
            float normalizedY = (y + 0.5f) / sampleCount;
            float localY = Mathf.Lerp(-d1.HalfHeight, d1.HalfHeight, normalizedY);
            for (int x = 0; x < sampleCount; x++)
            {
                float normalizedX = (x + 0.5f) / sampleCount;
                float localX = Mathf.Lerp(-d1.HalfWidth, d1.HalfWidth, normalizedX);
                Vector3 target = d1.LocalToWorld(new Vector2(localX, localY));
                TryClassifyRayToDisplayRange(rayOrigin, target, d2, out RayOcclusionResult result, out _);
                if (result == RayOcclusionResult.Occluded || (countAmbiguousAsOccluded && result == RayOcclusionResult.Ambiguous))
                {
                    occluded++;
                }

                total++;
            }
        }

        return total > 0 ? occluded / (float)total : 0f;
    }

    public static T1RayOcclusionLayout BuildLayout(
        Vector3 eye,
        Vector3 forward,
        Vector3 d1Horizontal,
        Vector3 d1Vertical,
        float h1,
        float h2,
        float eta2Degrees)
    {
        return BuildLayout(eye, forward, d1Horizontal, d1Vertical, h1, h2, eta2Degrees, 1f, 0f);
    }

    public static T1RayOcclusionLayout BuildLayout(
        Vector3 eye,
        Vector3 forward,
        Vector3 d1Horizontal,
        Vector3 d1Vertical,
        float h1,
        float h2,
        float eta2Degrees,
        float handVerticalSign)
    {
        return BuildLayout(eye, forward, d1Horizontal, d1Vertical, h1, h2, eta2Degrees, handVerticalSign, 0f);
    }

    public static T1RayOcclusionLayout BuildLayout(
        Vector3 eye,
        Vector3 forward,
        Vector3 d1Horizontal,
        Vector3 d1Vertical,
        float h1,
        float h2,
        float eta2Degrees,
        float handVerticalSign,
        float visualEyeSampleOffsetMeters)
    {
        Vector3 f = DisplayGeometry.NormalizeOrFallback(forward, Vector3.forward);
        Vector3 v1 = DisplayGeometry.NormalizeOrFallback(Vector3.ProjectOnPlane(d1Vertical, f), Vector3.up);
        Vector3 u1 = DisplayGeometry.NormalizeOrFallback(Vector3.Cross(v1, f), d1Horizontal);
        v1 = DisplayGeometry.NormalizeOrFallback(Vector3.Cross(f, u1), v1);
        h1 = Mathf.Max(0.01f, h1);
        h2 = Mathf.Max(0.01f, h2);

        Vector2 d1Size = DisplayGeometry.ComputePhysicalSize(h1, AlphaHDegrees, AlphaVDegrees);
        Vector2 d2Size = DisplayGeometry.ComputePhysicalSize(h2, AlphaHDegrees, AlphaVDegrees);

        DisplayPlane d1 = new DisplayPlane(
            eye + h1 * f,
            -f,
            u1,
            v1,
            d1Size.x,
            d1Size.y);

        float etaRadians = eta2Degrees * Mathf.Deg2Rad;
        // eta2 > 0 はD2をD1より下方向へ離す。Scene viewでの調整と直感を合わせる。
        Vector3 d2Direction = DisplayGeometry.NormalizeOrFallback(
            Mathf.Cos(etaRadians) * f - Mathf.Sin(etaRadians) * v1,
            f);
        Vector3 d2Normal = -d2Direction;
        Vector3 u2 = u1;
        Vector3 v2 = DisplayGeometry.NormalizeOrFallback(Vector3.Cross(u2, d2Normal), v1);
        DisplayPlane d2 = new DisplayPlane(
            eye + h2 * d2Direction,
            d2Normal,
            u2,
            v2,
            d2Size.x,
            d2Size.y);

        float signedHandVertical = handVerticalSign >= 0f ? 1f : -1f;
        Vector3 handCenter = ComputeHandCenter(eye, f, v1, signedHandVertical);
        Vector3[] visualEyeSamples = GenerateVisualEyeSamples(eye, u1, v1, visualEyeSampleOffsetMeters);
        float visualOcclusionRatio = ComputeMaxSampledVisualOcclusionRatio(visualEyeSamples, d1, d2);
        float visualVerticalGapMeters = ComputeMinProjectedVerticalGapMeters(visualEyeSamples, d1, d2);
        float projectedOcclusionRatio = ComputeProjectedOcclusionRatio(handCenter, d1, d2);
        float sampledOcclusionRatio = ComputeSampledOcclusionRatio(handCenter, d1, d2);

        return new T1RayOcclusionLayout
        {
            D1 = d1,
            D2 = d2,
            Eye = eye,
            Forward = f,
            U1 = u1,
            V1 = v1,
            HandCenter = handCenter,
            H1 = h1,
            H2 = h2,
            Eta2Degrees = eta2Degrees,
            HandVerticalSign = signedHandVertical,
            ProjectedOcclusionRatio = projectedOcclusionRatio,
            SampledOcclusionRatio = sampledOcclusionRatio,
            VisualOcclusionRatio = visualOcclusionRatio,
            VisualVerticalGapMeters = visualVerticalGapMeters,
            VisualOcclusionClear = visualOcclusionRatio <= AreaEpsilon,
            D2CenterAngleValid = Mathf.Abs(eta2Degrees) >= MinimumD2VerticalSeparationDegrees,
            D2BelowD1 = Vector3.Dot(d2.Center - d1.Center, v1) < 0f
        };
    }

    public static bool IsNearTargetRayOcclusionRatio(float ratio, float targetRatio = 0.30f, float tolerance = 0.05f)
    {
        return Mathf.Abs(ratio - targetRatio) <= tolerance;
    }

    public static string FormatLayoutDebug(T1RayOcclusionLayout layout)
    {
        return
            $"[T1RayOcclusion] h1={layout.H1:0.000}, h2={layout.H2:0.000}, eta2={layout.Eta2Degrees:0.000}, " +
            $"D1Size=({layout.D1.Width:0.000}, {layout.D1.Height:0.000}), " +
            $"D2Size=({layout.D2.Width:0.000}, {layout.D2.Height:0.000}), " +
            $"C0={FormatVector(layout.HandCenter)}, " +
            $"handVerticalSign={layout.HandVerticalSign:0}, " +
            $"projectedOcclusionRatio={layout.ProjectedOcclusionRatio:0.000}, " +
            $"sampledOcclusionRatio={layout.SampledOcclusionRatio:0.000}, " +
            $"visualOcclusionRatio={layout.VisualOcclusionRatio:0.000}, " +
            $"visualVerticalGapMeters={layout.VisualVerticalGapMeters:0.000}, " +
            $"visualOcclusionClear={layout.VisualOcclusionClear}, " +
            $"d2CenterAngleValid={layout.D2CenterAngleValid}, " +
            $"d2BelowD1={layout.D2BelowD1}";
    }

    private static Vector3[] GenerateVisualEyeSamples(Vector3 eye, Vector3 u1, Vector3 v1, float sampleOffsetMeters)
    {
        float offset = Mathf.Max(0f, sampleOffsetMeters);
        if (offset <= 0f)
        {
            return new[] { eye };
        }

        // 実機では中心カメラだけでなく左右眼/IPDと微小な頭位置ズレでも視覚遮蔽なしにしたい。
        return new[]
        {
            eye,
            eye + offset * u1,
            eye - offset * u1,
            eye + offset * v1,
            eye - offset * v1
        };
    }

    private static float ComputeMaxSampledVisualOcclusionRatio(Vector3[] rayOrigins, DisplayPlane d1, DisplayPlane d2)
    {
        float maxRatio = 0f;
        for (int i = 0; i < rayOrigins.Length; i++)
        {
            maxRatio = Mathf.Max(maxRatio, ComputeSampledDisplayRangeIntersectionRatio(rayOrigins[i], d1, d2));
        }

        return maxRatio;
    }

    private static float ComputeMinProjectedVerticalGapMeters(Vector3[] rayOrigins, DisplayPlane d1, DisplayPlane d2)
    {
        float minGap = float.PositiveInfinity;
        for (int i = 0; i < rayOrigins.Length; i++)
        {
            minGap = Mathf.Min(minGap, ComputeProjectedVerticalGapMeters(rayOrigins[i], d1, d2));
        }

        return float.IsInfinity(minGap) ? 0f : minGap;
    }

    private static List<Vector2> ProjectPlaneCornersToD1(Vector3 rayOrigin, DisplayPlane d1, DisplayPlane sourcePlane)
    {
        List<Vector2> projected = new List<Vector2>();
        Vector3[] corners = sourcePlane.GetCorners();
        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 rayDelta = corners[i] - rayOrigin;
            float denominator = Vector3.Dot(d1.Normal, rayDelta);
            if (Mathf.Abs(denominator) < ParallelEpsilon)
            {
                continue;
            }

            float t = Vector3.Dot(d1.Normal, d1.Center - rayOrigin) / denominator;
            if (t <= 0f)
            {
                continue;
            }

            Vector3 projectedWorld = rayOrigin + t * rayDelta;
            projected.Add(d1.WorldToLocal(projectedWorld));
        }

        return projected;
    }

    private static List<Vector2> ClipPolygonToDisplayRange(List<Vector2> polygon, float halfWidth, float halfHeight)
    {
        List<Vector2> clipped = polygon;
        clipped = ClipAgainstBoundary(clipped, new Vector2(1f, 0f), halfWidth);
        clipped = ClipAgainstBoundary(clipped, new Vector2(-1f, 0f), halfWidth);
        clipped = ClipAgainstBoundary(clipped, new Vector2(0f, 1f), halfHeight);
        clipped = ClipAgainstBoundary(clipped, new Vector2(0f, -1f), halfHeight);
        return clipped;
    }

    private static List<Vector2> ClipAgainstBoundary(List<Vector2> polygon, Vector2 normal, float limit)
    {
        List<Vector2> output = new List<Vector2>();
        if (polygon == null || polygon.Count == 0)
        {
            return output;
        }

        Vector2 previous = polygon[polygon.Count - 1];
        bool previousInside = Vector2.Dot(normal, previous) <= limit;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 current = polygon[i];
            bool currentInside = Vector2.Dot(normal, current) <= limit;

            if (currentInside != previousInside)
            {
                output.Add(IntersectBoundary(previous, current, normal, limit));
            }

            if (currentInside)
            {
                output.Add(current);
            }

            previous = current;
            previousInside = currentInside;
        }

        return output;
    }

    private static Vector2 IntersectBoundary(Vector2 from, Vector2 to, Vector2 normal, float limit)
    {
        Vector2 delta = to - from;
        float denominator = Vector2.Dot(normal, delta);
        if (Mathf.Abs(denominator) < ParallelEpsilon)
        {
            return to;
        }

        float t = (limit - Vector2.Dot(normal, from)) / denominator;
        return from + Mathf.Clamp01(t) * delta;
    }

    private static float ComputePolygonArea(List<Vector2> polygon)
    {
        if (polygon == null || polygon.Count < 3)
        {
            return 0f;
        }

        float area = 0f;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 current = polygon[i];
            Vector2 next = polygon[(i + 1) % polygon.Count];
            area += current.x * next.y - next.x * current.y;
        }

        return area * 0.5f;
    }

    private static string FormatVector(Vector3 value)
    {
        return $"({value.x:0.000}, {value.y:0.000}, {value.z:0.000})";
    }
}
