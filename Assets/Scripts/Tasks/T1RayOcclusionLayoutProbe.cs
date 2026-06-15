using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[DisallowMultipleComponent]
/// <summary>
/// T1のRay遮蔽配置をInspectorから試すためのデバッグProbe。
/// 既存のRaycastBaseline入力処理とは独立して、幾何モデルだけをログ出力する。
/// </summary>
public class T1RayOcclusionLayoutProbe : MonoBehaviour
{
    private struct LayoutValidation
    {
        public bool NearTarget;
        public bool VisualOcclusionAccepted;
        public bool VerticalGapAccepted;
        public bool D2CenterAngleAccepted;
        public bool D2BelowAccepted;

        public bool GeometryAccepted =>
            VisualOcclusionAccepted
            && VerticalGapAccepted
            && D2CenterAngleAccepted
            && D2BelowAccepted;
    }

    [Header("References")]
    [SerializeField] private Camera hmdCamera;
    [SerializeField] private DisplaySurface d1BackDisplay;
    [SerializeField] private DisplaySurface d2FrontDisplay;

    [Header("Layout Parameters")]
    [Tooltip("Startup/apply distance from eye E to the back display D1 center.")]
    [SerializeField] private float h1 = 1.90f;
    [Tooltip("Startup/apply distance from eye E to the front display D2 center.")]
    [SerializeField] private float h2 = 1.15f;
    [Tooltip("Startup/apply vertical angle for D2. Positive values place D2 below D1.")]
    [SerializeField] private float eta2Degrees = 25f;
    [Tooltip("Sign for the modeled controller/hand position C0. -1 places C0 below the eye in Unity's up direction.")]
    [SerializeField] private float handVerticalSign = -1f;

    [Header("Search")]
    [SerializeField] private float h1Min = 1.60f;
    [SerializeField] private float h1Max = 2.20f;
    [SerializeField] private float h1Step = 0.10f;
    [SerializeField] private float h2Min = 0.90f;
    [SerializeField] private float h2Max = 1.80f;
    [SerializeField] private float h2Step = 0.05f;
    [SerializeField] private float eta2MinDegrees = 22.5f;
    [SerializeField] private float eta2MaxDegrees = 45f;
    [SerializeField] private float eta2StepDegrees = 2.5f;
    [SerializeField] private bool requireVisualOcclusionClear = true;
    [SerializeField] private bool requireMinimumVisualVerticalGap = true;
    [SerializeField] private float minVisualVerticalGapMeters = 0.08f;
    [SerializeField] private float visualEyeSampleOffsetMeters = 0.04f;
    [SerializeField] private bool requireD2CenterAngleValid = true;
    [SerializeField] private bool requireD2BelowD1 = true;
    [SerializeField] private bool applyBestLayoutAfterSearch = false;
    [SerializeField] private bool applyOnlyWhenNearTargetRatio = true;
    [SerializeField] private int maxLoggedSearchCandidates = 5;

    [Header("Apply To Displays")]
    [SerializeField] private bool applyDisplaySize = true;
    [SerializeField] private bool applyDisplayPose = true;
    [SerializeField] private Vector2 baseCanvasPixelSize = DisplayGeometry.DefaultCanvasPixelSize;

    [Header("Debug")]
    [Tooltip("When the app starts, place D1/D2 from the current h1/h2/eta2 Inspector parameters instead of relying on saved scene transforms.")]
    [SerializeField] private bool applyCurrentLayoutOnStart = true;
    [SerializeField] private bool logOnStart = false;
    [SerializeField] private bool logHandSamples = true;
    [SerializeField] private float targetRayOcclusionRatio = 0.30f;
    [SerializeField] private float targetRatioTolerance = 0.05f;
    [SerializeField] private bool showDebugOcclusionOverlay = true;
    [SerializeField] private bool showVisualOcclusionOverlay = false;
    [SerializeField] private bool showRayOcclusionOverlay = true;
    [SerializeField] private bool showAmbiguousOverlay = true;
    [SerializeField] private int debugOverlaySamplesPerAxis = 31;
    [SerializeField] private Color visualOccludedOnD1Color = new Color(0.10f, 0.45f, 1f, 0.35f);
    [SerializeField] private Color rayOccludedOnD1Color = new Color(1f, 0.10f, 0.10f, 0.42f);
    [SerializeField] private Color rayHitOnD2Color = new Color(1f, 0.55f, 0.05f, 0.42f);
    [SerializeField] private Color ambiguousOverlayColor = new Color(1f, 1f, 0.05f, 0.32f);

    private const bool ShowReferencePointGizmos = true;
    private const float ReferencePointGizmoRadius = 0.035f;
    private static readonly Color EyeGizmoColor = new Color(0.10f, 0.80f, 1f, 1f);
    private static readonly Color HandCenterGizmoColor = new Color(1f, 0.25f, 0.10f, 1f);
    private static readonly List<XRInputSubsystem> InputSubsystems = new List<XRInputSubsystem>();

    public T1RayOcclusionLayout LastLayout { get; private set; }
    public T1RayOcclusionLayout LastSearchBestLayout { get; private set; }

    private void OnEnable()
    {
        SubscribeToXrRecenterEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromXrRecenterEvents();
    }

    private IEnumerator Start()
    {
        if (Application.isPlaying && applyCurrentLayoutOnStart)
        {
            // 他のManagerのStart処理がプリセット配置を触った後に、T1配置を最後に再適用する。
            yield return null;
            LastLayout = BuildCurrentLayout();
            ApplyLayoutToDisplays(LastLayout);
            Debug.Log($"{T1RayOcclusionGeometry.FormatLayoutDebug(LastLayout)}, appliedToDisplays=True, reason=start layout");
        }

        if (logOnStart)
        {
            LogCurrentLayout();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && Application.isPlaying && applyCurrentLayoutOnStart)
        {
            StartCoroutine(ReapplyCurrentLayoutAfterFrame("application focus"));
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused && Application.isPlaying && applyCurrentLayoutOnStart)
        {
            StartCoroutine(ReapplyCurrentLayoutAfterFrame("application resume"));
        }
    }

    private void OnDrawGizmos()
    {
        if (!ShowReferencePointGizmos)
        {
            return;
        }

        T1RayOcclusionLayout layout = BuildCurrentLayout();
        DrawReferencePointGizmos(layout);
    }

    [ContextMenu("Search Best T1 Ray Occlusion Layout")]
    public void SearchBestLayout()
    {
        if (!TrySearchBestLayout(out T1RayOcclusionLayout bestLayout, out float bestScore, out int evaluatedCount, out int acceptedCount))
        {
            Debug.LogWarning($"[T1RayOcclusion] search found no valid layout. evaluated={evaluatedCount}, accepted={acceptedCount}");
            return;
        }

        LastSearchBestLayout = bestLayout;
        h1 = bestLayout.H1;
        h2 = bestLayout.H2;
        eta2Degrees = bestLayout.Eta2Degrees;
        LastLayout = bestLayout;

        bool nearTarget = T1RayOcclusionGeometry.IsNearTargetRayOcclusionRatio(
            bestLayout.SampledOcclusionRatio,
            targetRayOcclusionRatio,
            targetRatioTolerance);

        Debug.Log(
            $"{T1RayOcclusionGeometry.FormatLayoutDebug(bestLayout)}, " +
            $"nearTarget30Percent={nearTarget}, searchScore={bestScore:0.000}, evaluated={evaluatedCount}, accepted={acceptedCount}");

        if (applyBestLayoutAfterSearch)
        {
            ApplyLayoutToDisplaysIfAllowed(bestLayout, "best layout");
        }
    }

    [ContextMenu("Search Best And Apply To Displays")]
    public void SearchBestAndApplyToDisplays()
    {
        bool previousApply = applyBestLayoutAfterSearch;
        applyBestLayoutAfterSearch = true;
        SearchBestLayout();
        applyBestLayoutAfterSearch = previousApply;
    }

    [ContextMenu("Apply Current T1 Layout To Displays")]
    public void ApplyCurrentLayoutToDisplays()
    {
        LastLayout = BuildCurrentLayout();
        ApplyLayoutToDisplays(LastLayout);
        LogManualLayoutValidationWarnings(LastLayout);
        Debug.Log($"{T1RayOcclusionGeometry.FormatLayoutDebug(LastLayout)}, appliedToDisplays=True, validationRequired=False");
    }

    [ContextMenu("Log T1 Ray Occlusion Layout")]
    public void LogCurrentLayout()
    {
        LastLayout = BuildCurrentLayout();
        bool nearTarget = T1RayOcclusionGeometry.IsNearTargetRayOcclusionRatio(
            LastLayout.SampledOcclusionRatio,
            targetRayOcclusionRatio,
            targetRatioTolerance);

        Debug.Log($"{T1RayOcclusionGeometry.FormatLayoutDebug(LastLayout)}, nearTarget30Percent={nearTarget}");
        RefreshOcclusionDebugOverlay(LastLayout);

        if (logHandSamples)
        {
            LogSampleRayClassifications(LastLayout);
        }
    }

    public T1RayOcclusionLayout BuildCurrentLayout()
    {
        DisplayAnchorFrame eyeFrame = DisplayAnchorFrame.FromTransform(ResolveEyeTransform());

        return T1RayOcclusionGeometry.BuildLayout(
            eyeFrame.Position,
            eyeFrame.Forward,
            eyeFrame.Right,
            eyeFrame.Up,
            h1,
            h2,
            eta2Degrees,
            handVerticalSign,
            visualEyeSampleOffsetMeters);
    }

    public bool TrySearchBestLayout(
        out T1RayOcclusionLayout bestLayout,
        out float bestScore,
        out int evaluatedCount,
        out int acceptedCount)
    {
        bestLayout = default;
        bestScore = float.PositiveInfinity;
        evaluatedCount = 0;
        acceptedCount = 0;

        DisplayAnchorFrame eyeFrame = DisplayAnchorFrame.FromTransform(ResolveEyeTransform());

        float clampedH1Step = Mathf.Max(0.001f, h1Step);
        float clampedH2Step = Mathf.Max(0.001f, h2Step);
        float clampedEtaStep = Mathf.Max(0.001f, Mathf.Abs(eta2StepDegrees));
        int loggedCandidates = 0;

        for (float candidateH1 = h1Min; candidateH1 <= h1Max + 0.0001f; candidateH1 += clampedH1Step)
        {
            for (float candidateH2 = h2Min; candidateH2 <= h2Max + 0.0001f; candidateH2 += clampedH2Step)
            {
                if (candidateH2 >= candidateH1)
                {
                    continue;
                }

                for (float candidateEta = eta2MinDegrees; candidateEta <= eta2MaxDegrees + 0.0001f; candidateEta += clampedEtaStep)
                {
                    evaluatedCount++;
                    T1RayOcclusionLayout candidate = T1RayOcclusionGeometry.BuildLayout(
                        eyeFrame.Position,
                        eyeFrame.Forward,
                        eyeFrame.Right,
                        eyeFrame.Up,
                        candidateH1,
                        candidateH2,
                        candidateEta,
                        handVerticalSign,
                        visualEyeSampleOffsetMeters);

                    LayoutValidation validation = EvaluateLayout(candidate);
                    if (!validation.GeometryAccepted)
                    {
                        continue;
                    }

                    acceptedCount++;
                    float score = Mathf.Abs(candidate.SampledOcclusionRatio - targetRayOcclusionRatio);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestLayout = candidate;

                        if (loggedCandidates < maxLoggedSearchCandidates)
                        {
                            loggedCandidates++;
                            Debug.Log($"{T1RayOcclusionGeometry.FormatLayoutDebug(candidate)}, searchScore={score:0.000}, candidateRankLog={loggedCandidates}");
                        }
                    }
                }
            }
        }

        return !float.IsInfinity(bestScore);
    }

    public void ApplyLayoutToDisplays(T1RayOcclusionLayout layout)
    {
        ResolveDisplayReferences();
        DisablePresetLayoutReapply();
        ApplyPlaneToDisplay(d1BackDisplay, layout.D1);
        ApplyPlaneToDisplay(d2FrontDisplay, layout.D2);
        RefreshOcclusionDebugOverlay(layout);
        MarkAppliedLayoutDirty();
    }

    private IEnumerator ReapplyCurrentLayoutAfterFrame(string reason)
    {
        yield return null;
        LastLayout = BuildCurrentLayout();
        ApplyLayoutToDisplays(LastLayout);
        Debug.Log($"{T1RayOcclusionGeometry.FormatLayoutDebug(LastLayout)}, appliedToDisplays=True, reason={reason}");
    }

    private void SubscribeToXrRecenterEvents()
    {
        if (!Application.isPlaying)
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
        if (!Application.isPlaying || !applyCurrentLayoutOnStart)
        {
            return;
        }

        StartCoroutine(ReapplyCurrentLayoutAfterFrame("XR recenter"));
    }

    [ContextMenu("Refresh T1 Ray Occlusion Overlay")]
    public void RefreshCurrentOcclusionDebugOverlay()
    {
        LastLayout = BuildCurrentLayout();
        RefreshOcclusionDebugOverlay(LastLayout);
    }

    [ContextMenu("Clear T1 Ray Occlusion Overlay")]
    public void ClearOcclusionDebugOverlay()
    {
        ResolveDisplayReferences();
        ClearOverlay(d1BackDisplay, "T1_VisualOcclusionOverlay");
        ClearOverlay(d1BackDisplay, "T1_RayOcclusionOverlay");
        ClearOverlay(d2FrontDisplay, "T1_VisualHitOverlay");
        ClearOverlay(d2FrontDisplay, "T1_RayHitOverlay");
    }

#if UNITY_EDITOR
    [ContextMenu("Save Scene With Current T1 Layout")]
    public void SaveSceneWithCurrentLayout()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[T1RayOcclusion] Play Mode changes cannot be saved to the scene. Exit Play Mode, apply the layout, then save.");
            return;
        }

        MarkAppliedLayoutDirty();
        bool saved = EditorSceneManager.SaveScene(gameObject.scene);
        Debug.Log($"[T1RayOcclusion] scene save requested. saved={saved}, scene={gameObject.scene.path}");
    }
#endif

    private void ApplyLayoutToDisplaysIfAllowed(T1RayOcclusionLayout layout, string label)
    {
        LayoutValidation validation = EvaluateLayout(layout);
        bool accepted = validation.GeometryAccepted
            && (!applyOnlyWhenNearTargetRatio || validation.NearTarget);
        if (!accepted)
        {
            LogValidationIssues(layout, validation, $"{label} was not applied", applyOnlyWhenNearTargetRatio);
            return;
        }

        ApplyLayoutToDisplays(layout);
        Debug.Log($"{T1RayOcclusionGeometry.FormatLayoutDebug(layout)}, appliedToDisplays=True");
    }

    private void LogManualLayoutValidationWarnings(T1RayOcclusionLayout layout)
    {
        LayoutValidation validation = EvaluateLayout(layout);
        LogValidationIssues(layout, validation, "current layout was applied for manual tuning", true);
    }

    private LayoutValidation EvaluateLayout(T1RayOcclusionLayout layout)
    {
        return new LayoutValidation
        {
            NearTarget = T1RayOcclusionGeometry.IsNearTargetRayOcclusionRatio(
                layout.SampledOcclusionRatio,
                targetRayOcclusionRatio,
                targetRatioTolerance),
            VisualOcclusionAccepted = !requireVisualOcclusionClear || layout.VisualOcclusionClear,
            VerticalGapAccepted = !requireMinimumVisualVerticalGap
                || layout.VisualVerticalGapMeters >= minVisualVerticalGapMeters,
            D2CenterAngleAccepted = !requireD2CenterAngleValid || layout.D2CenterAngleValid,
            D2BelowAccepted = !requireD2BelowD1 || layout.D2BelowD1
        };
    }

    private void LogValidationIssues(
        T1RayOcclusionLayout layout,
        LayoutValidation validation,
        string context,
        bool includeNearTarget)
    {
        if (includeNearTarget && !validation.NearTarget)
        {
            Debug.LogWarning(
                $"[T1RayOcclusion] {context}: nearTarget30Percent=False, " +
                $"target={targetRayOcclusionRatio:0.000}±{targetRatioTolerance:0.000}, " +
                $"actual={layout.SampledOcclusionRatio:0.000}");
        }

        if (!validation.VisualOcclusionAccepted)
        {
            Debug.LogWarning($"[T1RayOcclusion] {context}: visualOcclusionClear=False.");
        }

        if (!validation.VerticalGapAccepted)
        {
            Debug.LogWarning(
                $"[T1RayOcclusion] {context}: visualVerticalGapMeters is too small, " +
                $"required={minVisualVerticalGapMeters:0.000}, actual={layout.VisualVerticalGapMeters:0.000}");
        }

        if (!validation.D2CenterAngleAccepted)
        {
            Debug.LogWarning($"[T1RayOcclusion] {context}: d2CenterAngleValid=False.");
        }

        if (!validation.D2BelowAccepted)
        {
            Debug.LogWarning($"[T1RayOcclusion] {context}: d2BelowD1=False.");
        }
    }

    private void LogSampleRayClassifications(T1RayOcclusionLayout layout)
    {
        Vector3[] handSamples = T1RayOcclusionGeometry.GenerateHandSamples(
            layout.HandCenter,
            layout.U1,
            layout.V1,
            layout.Forward);

        Vector3 targetCenter = layout.D1.Center;
        for (int i = 0; i < handSamples.Length; i++)
        {
            RayOcclusionResult result = T1RayOcclusionGeometry.IsRayOccluded(
                handSamples[i],
                targetCenter,
                layout.D2);

            Debug.Log($"[T1RayOcclusion] sample={i}, C={Format(handSamples[i])}, target=D1Center, result={result}");
        }
    }

    private void ApplyPlaneToDisplay(DisplaySurface display, DisplayPlane plane)
    {
        if (display == null)
        {
            return;
        }

        if (applyDisplayPose)
        {
            // 既存DisplaySurfaceはtransform.forwardを目からディスプレイ中心へ向かう方向として使う。
            display.transform.SetPositionAndRotation(
                plane.Center,
                Quaternion.LookRotation(-plane.Normal, plane.V));
        }

        if (applyDisplaySize)
        {
            display.SetSize(
                new Vector2(plane.Width, plane.Height),
                ComputeCanvasPixelSize(plane));
        }
    }

    private void RefreshOcclusionDebugOverlay(T1RayOcclusionLayout layout)
    {
        if (!showDebugOcclusionOverlay)
        {
            ClearOcclusionDebugOverlay();
            return;
        }

        ResolveDisplayReferences();
        if (d1BackDisplay == null || d2FrontDisplay == null)
        {
            return;
        }

        ClearOcclusionDebugOverlay();
        RectTransform d1VisualRoot = EnsureOverlayRoot(d1BackDisplay, "T1_VisualOcclusionOverlay");
        RectTransform d1RayRoot = EnsureOverlayRoot(d1BackDisplay, "T1_RayOcclusionOverlay");
        RectTransform d2VisualRoot = EnsureOverlayRoot(d2FrontDisplay, "T1_VisualHitOverlay");
        RectTransform d2RayRoot = EnsureOverlayRoot(d2FrontDisplay, "T1_RayHitOverlay");
        if (d1RayRoot == null || d2RayRoot == null)
        {
            return;
        }

        int samplesPerAxis = Mathf.Clamp(debugOverlaySamplesPerAxis, 3, 81);
        Vector2 d1CellMeters = new Vector2(layout.D1.Width / samplesPerAxis, layout.D1.Height / samplesPerAxis);
        Vector2 d2CellMeters = new Vector2(layout.D2.Width / samplesPerAxis, layout.D2.Height / samplesPerAxis);
        int rayOccluded = 0;
        int visualOccluded = 0;
        int visualAmbiguous = 0;
        int rayAmbiguous = 0;

        for (int y = 0; y < samplesPerAxis; y++)
        {
            float normalizedY = (y + 0.5f) / samplesPerAxis;
            float localY = Mathf.Lerp(-layout.D1.HalfHeight, layout.D1.HalfHeight, normalizedY);
            for (int x = 0; x < samplesPerAxis; x++)
            {
                float normalizedX = (x + 0.5f) / samplesPerAxis;
                float localX = Mathf.Lerp(-layout.D1.HalfWidth, layout.D1.HalfWidth, normalizedX);
                Vector2 d1Local = new Vector2(localX, localY);
                Vector3 targetOnD1 = layout.D1.LocalToWorld(d1Local);

                if (showVisualOcclusionOverlay)
                {
                    AddOcclusionDebugCell(
                        layout.Eye,
                        targetOnD1,
                        layout.D2,
                        d1BackDisplay,
                        d1VisualRoot,
                        d1Local,
                        d1CellMeters,
                        layout.D1,
                        visualOccludedOnD1Color,
                        visualOccludedOnD1Color,
                        d2FrontDisplay,
                        d2VisualRoot,
                        d2CellMeters,
                        ref visualOccluded,
                        ref visualAmbiguous);
                }

                if (showRayOcclusionOverlay)
                {
                    AddOcclusionDebugCell(
                        layout.HandCenter,
                        targetOnD1,
                        layout.D2,
                        d1BackDisplay,
                        d1RayRoot,
                        d1Local,
                        d1CellMeters,
                        layout.D1,
                        rayOccludedOnD1Color,
                        rayHitOnD2Color,
                        d2FrontDisplay,
                        d2RayRoot,
                        d2CellMeters,
                        ref rayOccluded,
                        ref rayAmbiguous);
                }
            }
        }

        d1BackDisplay.BringCursorToFront();
        d2FrontDisplay.BringCursorToFront();
        Debug.Log(
            $"[T1RayOcclusion] refreshed overlay. samplesPerAxis={samplesPerAxis}, " +
            $"visualOccludedCells={visualOccluded}, visualAmbiguousCells={visualAmbiguous}, " +
            $"rayOccludedCells={rayOccluded}, rayAmbiguousCells={rayAmbiguous}");
    }

    private void AddOcclusionDebugCell(
        Vector3 rayOrigin,
        Vector3 targetOnD1,
        DisplayPlane d2,
        DisplaySurface d1Display,
        RectTransform d1OverlayRoot,
        Vector2 d1Local,
        Vector2 d1CellMeters,
        DisplayPlane d1,
        Color d1OccludedColor,
        Color d2HitColor,
        DisplaySurface d2Display,
        RectTransform d2OverlayRoot,
        Vector2 d2CellMeters,
        ref int occludedCount,
        ref int ambiguousCount)
    {
        bool intersects = T1RayOcclusionGeometry.TryClassifyRayToDisplayRange(
            rayOrigin,
            targetOnD1,
            d2,
            out RayOcclusionResult result,
            out Vector2 d2Local);

        if (!intersects || result == RayOcclusionResult.Clear)
        {
            return;
        }

        bool ambiguous = result == RayOcclusionResult.Ambiguous;
        if (ambiguous && !showAmbiguousOverlay)
        {
            return;
        }

        Color d1Color = ambiguous ? ambiguousOverlayColor : d1OccludedColor;
        Color d2Color = ambiguous ? ambiguousOverlayColor : d2HitColor;
        CreateOverlayCell(d1Display, d1OverlayRoot, d1Local, d1CellMeters, d1, d1Color);
        CreateOverlayCell(d2Display, d2OverlayRoot, d2Local, d2CellMeters, d2, d2Color);

        if (ambiguous)
        {
            ambiguousCount++;
        }
        else
        {
            occludedCount++;
        }
    }

    private RectTransform EnsureOverlayRoot(DisplaySurface display, string rootName)
    {
        if (display == null || display.WorldSpaceCanvas == null)
        {
            return null;
        }

        Transform canvasTransform = display.WorldSpaceCanvas.transform;
        Transform existing = canvasTransform.Find(rootName);
        RectTransform root = existing != null ? existing as RectTransform : null;
        if (root == null)
        {
            GameObject rootObject = new GameObject(rootName, typeof(RectTransform));
            root = rootObject.GetComponent<RectTransform>();
            root.SetParent(canvasTransform, false);
        }

        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;
        root.localRotation = Quaternion.identity;
        root.SetAsLastSibling();
        return root;
    }

    private void ClearOverlay(DisplaySurface display, string rootName)
    {
        if (display == null || display.WorldSpaceCanvas == null)
        {
            return;
        }

        Transform root = display.WorldSpaceCanvas.transform.Find(rootName);
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private void CreateOverlayCell(
        DisplaySurface display,
        RectTransform root,
        Vector2 localMeters,
        Vector2 cellMeters,
        DisplayPlane plane,
        Color color)
    {
        if (display == null || root == null)
        {
            return;
        }

        GameObject cellObject = new GameObject("Cell", typeof(RectTransform), typeof(Image));
        RectTransform cell = cellObject.GetComponent<RectTransform>();
        cell.SetParent(root, false);
        cell.anchorMin = new Vector2(0.5f, 0.5f);
        cell.anchorMax = new Vector2(0.5f, 0.5f);
        cell.pivot = new Vector2(0.5f, 0.5f);

        Vector2 pixelSize = display.GetCanvasSize();
        cell.anchoredPosition = new Vector2(
            localMeters.x / Mathf.Max(0.0001f, plane.Width) * pixelSize.x,
            localMeters.y / Mathf.Max(0.0001f, plane.Height) * pixelSize.y);
        cell.sizeDelta = new Vector2(
            cellMeters.x / Mathf.Max(0.0001f, plane.Width) * pixelSize.x,
            cellMeters.y / Mathf.Max(0.0001f, plane.Height) * pixelSize.y);

        Image image = cellObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private void DrawReferencePointGizmos(T1RayOcclusionLayout layout)
    {
        float radius = Mathf.Max(0.001f, ReferencePointGizmoRadius);

        Gizmos.color = EyeGizmoColor;
        Gizmos.DrawSphere(layout.Eye, radius);
        Gizmos.DrawLine(layout.Eye, layout.D1.Center);

        Gizmos.color = HandCenterGizmoColor;
        Gizmos.DrawSphere(layout.HandCenter, radius);
        Gizmos.DrawLine(layout.HandCenter, layout.D1.Center);

        Gizmos.color = Color.white;
        Gizmos.DrawLine(layout.Eye, layout.HandCenter);

#if UNITY_EDITOR
        Handles.color = EyeGizmoColor;
        Handles.Label(layout.Eye + Vector3.up * radius * 1.6f, $"E used\n{Format(layout.Eye)}");

        Handles.color = HandCenterGizmoColor;
        Handles.Label(layout.HandCenter + Vector3.up * radius * 1.6f, $"C0 used\n{Format(layout.HandCenter)}");
#endif
    }

    private Vector2 ComputeCanvasPixelSize(DisplayPlane plane)
    {
        Vector2 referencePixels = new Vector2(
            Mathf.Max(0.01f, baseCanvasPixelSize.x),
            Mathf.Max(0.01f, baseCanvasPixelSize.y));

        // 既定の800x450pxを0.80x0.45mの密度として、サイズ変更後もUIの画素密度を保つ。
        // DisplaySurfaceがCanvasとHitPlaneをplaneの物理サイズへ揃える。
        float widthPixels = referencePixels.x * plane.Width / DisplayGeometry.DefaultPhysicalSizeMeters.x;
        float heightPixels = referencePixels.y * plane.Height / DisplayGeometry.DefaultPhysicalSizeMeters.y;
        return new Vector2(widthPixels, heightPixels);
    }

    private Transform ResolveEyeTransform()
    {
        if (hmdCamera == null)
        {
            hmdCamera = Camera.main;
        }

        return hmdCamera != null ? hmdCamera.transform : transform;
    }

    private void ResolveDisplayReferences()
    {
        if (d1BackDisplay == null)
        {
            GameObject back = GameObject.Find("Display_B_Back");
            d1BackDisplay = back != null ? back.GetComponent<DisplaySurface>() : null;
        }

        if (d2FrontDisplay == null)
        {
            GameObject front = GameObject.Find("Display_A_Front");
            d2FrontDisplay = front != null ? front.GetComponent<DisplaySurface>() : null;
        }
    }

    private void DisablePresetLayoutReapply()
    {
        DisplayLayoutManager layoutManager = FindObjectOfType<DisplayLayoutManager>();
        if (layoutManager != null)
        {
            layoutManager.SetUseFixedSceneLayout(true);
            layoutManager.SetApplyOnStart(false);
        }

        ExperimentManager experimentManager = FindObjectOfType<ExperimentManager>();
        if (experimentManager != null)
        {
            experimentManager.SetApplyStartingLayoutOnStart(false);
        }
    }

    private void MarkAppliedLayoutDirty()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            Debug.LogWarning("[T1RayOcclusion] Layout was applied in Play Mode. Unity will not persist these scene changes after exiting Play Mode.");
            return;
        }

        if (d1BackDisplay != null)
        {
            EditorUtility.SetDirty(d1BackDisplay);
            EditorUtility.SetDirty(d1BackDisplay.transform);
            if (d1BackDisplay.WorldSpaceCanvas != null)
            {
                EditorUtility.SetDirty(d1BackDisplay.WorldSpaceCanvas);
                EditorUtility.SetDirty(d1BackDisplay.WorldSpaceCanvas.transform);
            }
        }

        if (d2FrontDisplay != null)
        {
            EditorUtility.SetDirty(d2FrontDisplay);
            EditorUtility.SetDirty(d2FrontDisplay.transform);
            if (d2FrontDisplay.WorldSpaceCanvas != null)
            {
                EditorUtility.SetDirty(d2FrontDisplay.WorldSpaceCanvas);
                EditorUtility.SetDirty(d2FrontDisplay.WorldSpaceCanvas.transform);
            }
        }

        DisplayLayoutManager layoutManager = FindObjectOfType<DisplayLayoutManager>();
        if (layoutManager != null)
        {
            EditorUtility.SetDirty(layoutManager);
        }

        ExperimentManager experimentManager = FindObjectOfType<ExperimentManager>();
        if (experimentManager != null)
        {
            EditorUtility.SetDirty(experimentManager);
        }

        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"[T1RayOcclusion] marked applied layout dirty. Save the scene before building. scene={gameObject.scene.path}");
#endif
    }

    private static string Format(Vector3 value)
    {
        return $"({value.x:0.000}, {value.y:0.000}, {value.z:0.000})";
    }
}
