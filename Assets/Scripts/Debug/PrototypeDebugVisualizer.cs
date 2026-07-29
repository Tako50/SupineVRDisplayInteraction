using UnityEngine;
using UnityEngine.UI;

public enum RayVisualizationMode
{
    ConditionBased,
    ControllerOnly,
    GazeOnly,
    Both,
    Hidden
}

[DisallowMultipleComponent]
/// <summary>
/// 開発用のRay・候補・フォーカス・条件表示を描画する。
/// 実験ロジックには影響せず、Editor/実機で状態を確認するためだけに使う。
/// </summary>
public class PrototypeDebugVisualizer : MonoBehaviour
{
    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private GazeProvider gazeProvider;
    [SerializeField] private EyeTrackingRayAdapter eyeTrackingAdapter;
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private FocusManager focusManager;
    [SerializeField] private VirtualCursorController virtualCursorController;
    [SerializeField] private ClickDispatcher clickDispatcher;
    [SerializeField] private ScrollController scrollController;
    [SerializeField] private ExperimentManager experimentManager;
    [SerializeField] private EditorDebugInputProvider debugInputProvider;
    [SerializeField] private FocusPointingTaskManager focusPointingTaskManager;
    [SerializeField] private RaycastPointer raycastPointer;
    [SerializeField] private Transform controllerRaySource;
    [SerializeField] private float rayLength = 4f;
    [SerializeField] private float hitPointRadius = 0.01f;
    [SerializeField] private RayVisualizationMode rayVisualizationMode = RayVisualizationMode.Hidden;
    [SerializeField] private bool showGazeRayInGame = true;
    [SerializeField] private bool showGazeRayOnlyWhileGripHeld = true;
    [SerializeField] private float gazeRayStartOffset = 0.35f;
    [SerializeField] private float gazeRayStartWidth = 0.0035f;
    [SerializeField] private float gazeRayEndWidth = 0.001f;
    [SerializeField] private Color gazeRayColor = Color.magenta;
    [SerializeField] private LineRenderer gazeRayLine;
    [SerializeField] private Transform gazeHitMarker;
    [SerializeField] private bool showOverlay = true;
    [SerializeField] private bool useUnityUiOverlay = true;
    [SerializeField] private Text overlayText;
    [SerializeField] private bool showWorldConditionLabel = true;
    [SerializeField] private string worldConditionLabelAnchorDisplayId = "Display_B_Back";
    [SerializeField] private Vector2 worldConditionLabelNormalizedAnchor = new Vector2(0.5f, 1.60f);
    [SerializeField] private float worldConditionLabelForwardOffset = -0.01f;
    [SerializeField] private Vector2 worldConditionLabelSize = new Vector2(520f, 72f);
    [SerializeField] private float worldConditionLabelScale = 0.0015f;
    [SerializeField] private Text worldConditionLabel;

    public RayVisualizationMode CurrentRayVisualizationMode => rayVisualizationMode;

    private void Awake()
    {
        ResolveReferences();
        EnsureGazeRayVisuals();
        EnsureOverlayText();
        EnsureWorldConditionLabel();
    }

    private void Update()
    {
        ResolveReferences();
        UpdateRaycastPointerVisibility();
        UpdateGazeRayVisuals();
        UpdateUnityUiOverlay();
        UpdateWorldConditionLabel();
    }

    private void OnDrawGizmos()
    {
        ResolveReferences();

        if (displayManager == null)
        {
            return;
        }

        Ray controllerRay = GetControllerRay();
        float controllerRayLength = raycastPointer != null ? raycastPointer.VisibleRayLengthMeters : rayLength;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(controllerRay.origin, controllerRay.origin + controllerRay.direction * controllerRayLength);
        if (displayManager.TryGetForemostHit(controllerRay, out DisplayHit controllerHit))
        {
            Gizmos.DrawSphere(controllerHit.Hit.point, hitPointRadius);
        }

        if (gazeProvider != null && gazeProvider.TryGetValidGazeRay(out Ray gazeRay))
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(gazeRay.origin, gazeRay.origin + gazeRay.direction * rayLength);
            DisplayHit[] gazeHits = displayManager.GetDisplayHitsAll(gazeRay);
            for (int i = 0; i < gazeHits.Length; i++)
            {
                Gizmos.color = i == 0 ? Color.magenta : Color.yellow;
                Gizmos.DrawWireSphere(gazeHits[i].Hit.point, hitPointRadius * (1.4f + i * 0.25f));
            }
        }

        DisplaySurface focused = displayManager.FocusedDisplay;
        if (focused != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(focused.transform.position, new Vector3(focused.PhysicalSizeMeters.x, focused.PhysicalSizeMeters.y, 0.02f));
        }
    }

    private void OnGUI()
    {
        if (!showOverlay || useUnityUiOverlay || displayManager == null)
        {
            return;
        }

        GUI.Box(new Rect(12f, 12f, 540f, 312f), "Explicit Display Focus Debug");
        GUI.Label(new Rect(24f, 40f, 500f, 270f), BuildOverlayText());
    }

    private void ResolveReferences()
    {
        if (inputManager == null)
        {
            inputManager = PrototypeInputManager.Instance != null ? PrototypeInputManager.Instance : FindObjectOfType<PrototypeInputManager>();
        }

        if (displayManager == null)
        {
            displayManager = DisplayManager.Instance != null ? DisplayManager.Instance : FindObjectOfType<DisplayManager>();
        }

        if (gazeProvider == null)
        {
            gazeProvider = FindObjectOfType<GazeProvider>();
        }

        if (eyeTrackingAdapter == null)
        {
            eyeTrackingAdapter = FindObjectOfType<EyeTrackingRayAdapter>();
        }

        if (focusManager == null)
        {
            focusManager = FindObjectOfType<FocusManager>();
        }

        if (virtualCursorController == null)
        {
            virtualCursorController = FindObjectOfType<VirtualCursorController>();
        }

        if (clickDispatcher == null)
        {
            clickDispatcher = FindObjectOfType<ClickDispatcher>();
        }

        if (scrollController == null)
        {
            scrollController = FindObjectOfType<ScrollController>();
        }

        if (experimentManager == null)
        {
            experimentManager = FindObjectOfType<ExperimentManager>();
        }

        if (debugInputProvider == null)
        {
            debugInputProvider = FindObjectOfType<EditorDebugInputProvider>();
        }

        if (focusPointingTaskManager == null)
        {
            focusPointingTaskManager = FindObjectOfType<FocusPointingTaskManager>();
        }

        if (raycastPointer == null)
        {
            raycastPointer = FindObjectOfType<RaycastPointer>();
        }
    }

    private void EnsureOverlayText()
    {
        if (!showOverlay || !useUnityUiOverlay || overlayText != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("Prototype_DebugOverlayCanvas");
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        Camera overlayCamera = Camera.main;
        canvas.renderMode = overlayCamera != null
            ? RenderMode.ScreenSpaceCamera
            : RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = overlayCamera;
        if (overlayCamera != null)
        {
            canvas.planeDistance = Mathf.Max(overlayCamera.nearClipPlane + 0.01f, 0.5f);
        }
        canvas.sortingOrder = 1000;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject textObject = new GameObject("DebugOverlayText", typeof(RectTransform));
        textObject.transform.SetParent(canvasObject.transform, false);
        overlayText = textObject.AddComponent<Text>();
        overlayText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        overlayText.fontSize = 16;
        overlayText.alignment = TextAnchor.UpperLeft;
        overlayText.color = Color.white;

        RectTransform rectTransform = overlayText.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = new Vector2(16f, -16f);
        rectTransform.sizeDelta = new Vector2(620f, 320f);
    }

    private void EnsureGazeRayVisuals()
    {
        if (gazeRayLine == null)
        {
            Transform existingLine = transform.Find("Debug_GazeRayLine");
            if (existingLine != null)
            {
                gazeRayLine = existingLine.GetComponent<LineRenderer>();
            }
        }

        if (gazeRayLine == null)
        {
            GameObject lineObject = new GameObject("Debug_GazeRayLine");
            lineObject.transform.SetParent(transform, false);
            gazeRayLine = lineObject.AddComponent<LineRenderer>();
        }

        gazeRayLine.useWorldSpace = true;
        gazeRayLine.positionCount = 2;
        gazeRayLine.startWidth = gazeRayStartWidth;
        gazeRayLine.endWidth = gazeRayEndWidth;
        gazeRayLine.material = new Material(Shader.Find("Sprites/Default"));
        gazeRayLine.startColor = gazeRayColor;
        gazeRayLine.endColor = gazeRayColor;
        gazeRayLine.enabled = false;

        if (gazeHitMarker == null)
        {
            Transform existingMarker = transform.Find("Debug_GazeHitMarker");
            if (existingMarker != null)
            {
                gazeHitMarker = existingMarker;
            }
        }

        if (gazeHitMarker == null)
        {
            GameObject markerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            markerObject.name = "Debug_GazeHitMarker";
            markerObject.transform.SetParent(transform, false);
            gazeHitMarker = markerObject.transform;

            Collider markerCollider = markerObject.GetComponent<Collider>();
            if (markerCollider != null)
            {
                markerCollider.enabled = false;
            }

            Renderer markerRenderer = markerObject.GetComponent<Renderer>();
            if (markerRenderer != null)
            {
                markerRenderer.material = new Material(Shader.Find("Sprites/Default"));
                markerRenderer.material.color = gazeRayColor;
            }
        }

        gazeHitMarker.localScale = Vector3.one * hitPointRadius * 2f;
        gazeHitMarker.gameObject.SetActive(false);
    }

    private void UpdateGazeRayVisuals()
    {
        EnsureGazeRayVisuals();

        if (!ShouldShowGazeRay() || gazeProvider == null || displayManager == null)
        {
            SetGazeRayVisible(false);
            return;
        }

        if (!gazeProvider.TryGetValidGazeRay(out Ray gazeRay))
        {
            SetGazeRayVisible(false);
            return;
        }

        Vector3 lineStart = gazeRay.origin + gazeRay.direction * Mathf.Max(0f, gazeRayStartOffset);
        Vector3 lineEnd = gazeRay.origin + gazeRay.direction * rayLength;
        bool hasHit = displayManager.TryGetForemostHit(gazeRay, out DisplayHit gazeHit);
        if (hasHit)
        {
            lineEnd = gazeHit.Hit.point;
        }

        gazeRayLine.SetPosition(0, lineStart);
        gazeRayLine.SetPosition(1, lineEnd);
        gazeRayLine.startColor = gazeRayColor;
        gazeRayLine.endColor = gazeRayColor;
        gazeRayLine.startWidth = gazeRayStartWidth;
        gazeRayLine.endWidth = gazeRayEndWidth;
        gazeRayLine.enabled = true;

        if (gazeHitMarker != null)
        {
            gazeHitMarker.position = lineEnd;
            gazeHitMarker.localScale = Vector3.one * hitPointRadius * 2f;
            gazeHitMarker.gameObject.SetActive(hasHit);
        }
    }

    private void SetGazeRayVisible(bool visible)
    {
        if (gazeRayLine != null)
        {
            gazeRayLine.enabled = visible;
        }

        if (gazeHitMarker != null)
        {
            gazeHitMarker.gameObject.SetActive(visible && gazeHitMarker.gameObject.activeSelf);
        }
    }

    private void UpdateRaycastPointerVisibility()
    {
        if (raycastPointer == null)
        {
            return;
        }

        if (rayVisualizationMode == RayVisualizationMode.Hidden)
        {
            return;
        }

        raycastPointer.SetRayVisualEnabled(ShouldEnableControllerRayVisual());
    }

    private bool ShouldEnableControllerRayVisual()
    {
        if (rayVisualizationMode == RayVisualizationMode.GazeOnly)
        {
            return false;
        }

        return rayVisualizationMode == RayVisualizationMode.ControllerOnly
            || rayVisualizationMode == RayVisualizationMode.Both
            || rayVisualizationMode == RayVisualizationMode.ConditionBased;
    }

    private bool ShouldShowGazeRay()
    {
        if (!showGazeRayInGame || rayVisualizationMode == RayVisualizationMode.Hidden)
        {
            return false;
        }

        bool modeAllowsGaze = rayVisualizationMode == RayVisualizationMode.GazeOnly
            || rayVisualizationMode == RayVisualizationMode.Both
            || rayVisualizationMode == RayVisualizationMode.ConditionBased;
        if (!modeAllowsGaze)
        {
            return false;
        }

        if (inputManager != null && rayVisualizationMode == RayVisualizationMode.ConditionBased)
        {
            if (inputManager.CurrentCondition != InteractionCondition.ExplicitDisplayFocus)
            {
                return false;
            }
        }

        if (showGazeRayOnlyWhileGripHeld
            && inputManager != null
            && !inputManager.GripHeld)
        {
            return false;
        }

        return true;
    }

    public void SetRayVisualizationMode(RayVisualizationMode mode)
    {
        rayVisualizationMode = mode;
        UpdateRaycastPointerVisibility();
        if (mode == RayVisualizationMode.Hidden)
        {
            SetGazeRayVisible(false);
        }
    }

    public void SetOverlayVisible(bool visible)
    {
        showOverlay = visible;
        if (!visible && overlayText != null)
        {
            overlayText.enabled = false;
        }
    }

    public void SetWorldConditionLabelVisible(bool visible)
    {
        showWorldConditionLabel = visible;
        if (!visible && worldConditionLabel != null)
        {
            worldConditionLabel.transform.parent.gameObject.SetActive(false);
        }
    }

    public void SetGazeRayOnlyWhileGripHeld(bool onlyWhileGripHeld)
    {
        showGazeRayOnlyWhileGripHeld = onlyWhileGripHeld;
    }

    private void UpdateUnityUiOverlay()
    {
        if (!showOverlay || !useUnityUiOverlay)
        {
            if (overlayText != null)
            {
                overlayText.enabled = false;
            }

            return;
        }

        EnsureOverlayText();
        if (overlayText == null)
        {
            return;
        }

        overlayText.enabled = true;
        overlayText.text = BuildOverlayText();
    }

    private void EnsureWorldConditionLabel()
    {
        if (!showWorldConditionLabel || worldConditionLabel != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("Prototype_WorldConditionLabel");
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 1001;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = worldConditionLabelSize;

        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform));
        backgroundObject.transform.SetParent(canvasObject.transform, false);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        Image background = backgroundObject.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.55f);

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(canvasObject.transform, false);
        worldConditionLabel = textObject.AddComponent<Text>();
        worldConditionLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        worldConditionLabel.fontSize = 26;
        worldConditionLabel.fontStyle = FontStyle.Bold;
        worldConditionLabel.alignment = TextAnchor.MiddleCenter;
        worldConditionLabel.color = Color.white;

        RectTransform textRect = worldConditionLabel.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 4f);
        textRect.offsetMax = new Vector2(-12f, -4f);
    }

    private void UpdateWorldConditionLabel()
    {
        if (!showWorldConditionLabel)
        {
            if (worldConditionLabel != null)
            {
                worldConditionLabel.transform.parent.gameObject.SetActive(false);
            }

            return;
        }

        EnsureWorldConditionLabel();
        if (worldConditionLabel == null)
        {
            return;
        }

        Transform labelTransform = worldConditionLabel.transform.parent;
        labelTransform.gameObject.SetActive(true);
        Vector3 labelPosition = GetWorldConditionLabelPosition(out Quaternion labelRotation);
        labelTransform.rotation = labelRotation;
        labelTransform.localScale = Vector3.one * worldConditionLabelScale;

        string condition = inputManager != null ? inputManager.CurrentCondition.ToString() : "Unknown";
        string phase = focusPointingTaskManager != null ? focusPointingTaskManager.CurrentPhase.ToString() : "Dev";
        string progress = focusPointingTaskManager != null
            ? focusPointingTaskManager.MainSetProgressText
            : string.Empty;
        bool hasProgress = !string.IsNullOrEmpty(progress);
        RectTransform labelRect = labelTransform as RectTransform;
        if (labelRect != null)
        {
            const float progressLineHeight = 48f;
            labelRect.sizeDelta = new Vector2(
                worldConditionLabelSize.x,
                worldConditionLabelSize.y + (hasProgress ? progressLineHeight : 0f));
            if (hasProgress)
            {
                labelPosition -= labelRotation * Vector3.up
                    * (progressLineHeight * worldConditionLabelScale * 0.5f);
            }
        }

        labelTransform.position = labelPosition;
        worldConditionLabel.text = hasProgress
            ? $"{phase} | {condition}\n{progress}"
            : $"{phase} | {condition}";
    }

    private Vector3 GetWorldConditionLabelPosition(out Quaternion rotation)
    {
        DisplaySurface anchorDisplay = FindWorldConditionLabelAnchorDisplay();
        if (anchorDisplay != null)
        {
            rotation = anchorDisplay.transform.rotation;
            Vector2 size = anchorDisplay.PhysicalSizeMeters;
            Vector2 anchor = worldConditionLabelNormalizedAnchor;
            Vector3 localPosition = new Vector3(
                (anchor.x - 0.5f) * size.x,
                (anchor.y - 0.5f) * size.y,
                worldConditionLabelForwardOffset);
            return anchorDisplay.transform.TransformPoint(localPosition);
        }

        Camera camera = Camera.main;
        if (camera != null)
        {
            rotation = camera.transform.rotation;
            return camera.transform.position
                + camera.transform.forward * 1.2f
                + camera.transform.up * 0.45f;
        }

        rotation = transform.rotation;
        return transform.position + Vector3.up * 0.45f;
    }

    private DisplaySurface FindWorldConditionLabelAnchorDisplay()
    {
        if (displayManager == null || displayManager.Displays == null)
        {
            return null;
        }

        DisplaySurface fallback = null;
        for (int i = 0; i < displayManager.Displays.Length; i++)
        {
            DisplaySurface display = displayManager.Displays[i];
            if (display == null)
            {
                continue;
            }

            fallback = fallback == null ? display : fallback;
            if (display.name == worldConditionLabelAnchorDisplayId)
            {
                return display;
            }
        }

        return fallback;
    }

    private string BuildOverlayText()
    {
        if (displayManager == null)
        {
            return "Explicit Display Focus Debug\nDisplayManager: missing";
        }

        Ray controllerRay = GetControllerRay();
        bool hasRayHit = displayManager.TryGetForemostHit(controllerRay, out DisplayHit rayHit);
        DisplayHit gazeHit = default;
        Ray gazeRay = default;
        bool gazeValid = gazeProvider != null && gazeProvider.TryGetValidGazeRay(out gazeRay);
        bool hasGazeHit = gazeValid && displayManager.TryGetForemostHit(gazeRay, out gazeHit);
        string condition = inputManager != null ? inputManager.CurrentCondition.ToString() : "Unknown";
        string focusState = focusManager != null ? focusManager.CurrentState.ToString() : "Unknown";
        string candidates = focusManager != null ? focusManager.CurrentCandidateIds : "None";
        string focusedDisplay = displayManager.FocusedDisplay != null ? displayManager.FocusedDisplay.name : "None";
        string cursorPosition = virtualCursorController != null ? Format(virtualCursorController.NormalizedPosition) : "(n/a)";
        string layout = experimentManager != null ? experimentManager.CurrentLayout.ToString() : "Unknown";
        string configuredGazeSource = gazeProvider != null ? gazeProvider.ConfiguredGazeSource.ToString() : "Unknown";
        string activeGazeSource = gazeProvider != null ? gazeProvider.CurrentGazeSource.ToString() : "Unknown";
        string eyeTracking = eyeTrackingAdapter != null
            ? $"{eyeTrackingAdapter.IsEyeTrackingAvailable} permission={eyeTrackingAdapter.HasEyeTrackingPermission} device={eyeTrackingAdapter.LastDeviceName} tracked={eyeTrackingAdapter.LastIsTrackedValue:0.0} delta={eyeTrackingAdapter.LastForwardDeltaDegrees:0.00}"
            : "No adapter";
        string debugInput = debugInputProvider != null && debugInputProvider.IsEnabled ? "Enabled" : "Disabled";
        string rightTrigger = inputManager != null
            ? $"value={inputManager.TriggerValue:0.00} held={inputManager.TriggerHeld} pressed={inputManager.TriggerPressed} released={inputManager.TriggerReleased}"
            : "No input manager";
        string lastClick = clickDispatcher != null ? clickDispatcher.LastClickResult : "None";
        string lastScroll = scrollController != null
            ? $"{scrollController.LastScrollDisplayId} {scrollController.LastScrollAmount:0.000}"
            : "None";
        string controllerRayVisual = raycastPointer != null
            ? $"{(raycastPointer.RayVisualEnabled ? "ON" : "OFF")} {raycastPointer.RayLengthLevel} {raycastPointer.VisibleRayLengthMeters:0.00}m"
            : "No RaycastPointer";
        string gazeRayState = raycastPointer != null
            ? $"selected={(raycastPointer.GazeSelectedDisplay != null ? raycastPointer.GazeSelectedDisplay.name : "None")} valid={raycastPointer.GazeValid} pointerValid={raycastPointer.PointerValid} penetrated={raycastPointer.PenetratedDisplayIds}"
            : "No RaycastPointer";
        string trial = focusPointingTaskManager != null && focusPointingTaskManager.IsRunning
            ? $"{focusPointingTaskManager.CurrentTrialIndex} target={focusPointingTaskManager.CurrentTargetDisplayId} {Format(focusPointingTaskManager.CurrentTargetNormalizedPosition)} size={focusPointingTaskManager.CurrentTargetSizeNormalized:0.000}"
            : "None";

        return
            "Explicit Display Focus Debug\n" +
            $"Condition: {condition}\n" +
            $"Ray mode: {rayVisualizationMode}\n" +
            $"Controller ray visual: {controllerRayVisual}\n" +
            $"GazeRay: {gazeRayState}\n" +
            $"Gaze source: configured={configuredGazeSource}, active={activeGazeSource}\n" +
            $"Eye tracking: {eyeTracking}\n" +
            $"Focus state: {focusState}\n" +
            $"Focused display: {focusedDisplay}\n" +
            $"Gaze candidates: {candidates} ({BuildExplicitCandidateMode()})\n" +
            $"Gaze hit: {(hasGazeHit ? gazeHit.DisplayId + " " + Format(gazeHit.Normalized) : "None")}\n" +
            $"Controller hit: {(hasRayHit ? rayHit.DisplayId + " " + Format(rayHit.Normalized) : "None")}\n" +
            $"Cursor normalized: {cursorPosition}\n" +
            $"Layout: {layout}\n" +
            $"Debug input: {debugInput}\n" +
            $"Right trigger: {rightTrigger}\n" +
            $"Last click: {lastClick}\n" +
            $"Last scroll: {lastScroll}\n" +
            $"FocusPointing trial: {trial}";
    }

    private Ray GetControllerRay()
    {
        Transform source = controllerRaySource;
        if (source == null && Camera.main != null)
        {
            source = Camera.main.transform;
        }

        return source != null
            ? new Ray(source.position, source.forward)
            : new Ray(transform.position, transform.forward);
    }

    private static string Format(Vector2 value)
    {
        return $"({value.x:0.000}, {value.y:0.000})";
    }

    private string BuildExplicitCandidateMode()
    {
        if (focusManager == null)
        {
            return "Unknown";
        }

        if (!focusManager.HasValidGazeThisFrame)
        {
            return "invalid gaze: held";
        }

        return focusManager.CurrentCandidateUsesOffDisplaySnap
            ? $"off-display snap {focusManager.CurrentCandidateAngularDistanceDegrees:0.00}deg"
            : "direct";
    }
}
