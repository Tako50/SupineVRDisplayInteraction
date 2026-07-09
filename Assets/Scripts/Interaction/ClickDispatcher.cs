using UnityEngine;

[DisallowMultipleComponent]
/// <summary>
/// Aボタンをクリックへ変換する。トリガー単押しはクリックに使わない。
/// 既定のDirectScrollAndSeekではBaselineのtrigger+RayとExplicitのstick押し込みラッチ+stickでWeb UI dragする。
/// BaselineはRay、ExplicitDisplayFocusはフォーカス表示上の仮想カーソルをポインター位置に使う。
/// </summary>
public class ClickDispatcher : MonoBehaviour
{
    private enum WebViewPointerActivation
    {
        None,
        Submit,
        Stick,
        Trigger,
        ThumbstickClick
    }

    private enum WebViewStickGestureAxis
    {
        None,
        Horizontal,
        Vertical
    }

    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private RaycastPointer raycastPointer;
    [SerializeField] private FocusManager focusManager;
    [SerializeField] private VirtualCursorController virtualCursorController;
    [SerializeField] private GazeProvider gazeProvider;
    [SerializeField] private FocusPointingTaskManager focusPointingTaskManager;
    [SerializeField] private WebViewSessionManager webViewSessionManager;
    [SerializeField] private VRTaskMenuManager vrTaskMenuManager;
    [SerializeField] private Logger logger;
    [Tooltip("Legacy WebView input only: holding A sends pointer down/move/up.")]
    [SerializeField] private bool enableWebViewPointerDrag = true;
    [Header("WebView Stick Touch Gesture")]
    [Range(0.01f, 0.95f)]
    [SerializeField] private float webViewStickGestureDeadzone = 0.12f;
    [Min(0.01f)]
    [Tooltip("Web UI drag speed in display canvas pixels per second at full stick deflection.")]
    [SerializeField] private float webViewStickGestureSpeedPixelsPerSecond = 260f;
    [Tooltip("RaycastBaseline keeps a touch down briefly while the stick crosses neutral so horizontal drags can reverse direction.")]
    [Min(0f)]
    [SerializeField] private float webViewRayNeutralReleaseDelay = 0.12f;
    [Tooltip("Vertical page gestures are re-clutched before the hidden touch reaches this distance from a display edge.")]
    [Range(0.01f, 0.2f)]
    [SerializeField] private float webViewVerticalGestureEdgeMargin = 0.08f;
    [Tooltip("After a vertical re-clutch, the hidden touch restarts this far from the opposite edge. The visible cursor does not move.")]
    [Range(0.15f, 0.45f)]
    [SerializeField] private float webViewVerticalGestureReentry = 0.28f;
    [SerializeField] private bool invertWebViewStickGestureX = false;
    [SerializeField] private bool invertWebViewStickGestureY = false;
    [Header("WebView Direct Drag")]
    [Tooltip("Minimum pointer movement in display canvas pixels before a latched Web UI drag sends pointer down. This prevents trigger or stick-click taps from becoming Web clicks.")]
    [Min(0f)]
    [SerializeField] private float webViewDirectDragStartThresholdPixels = 16f;
    [Tooltip("ExplicitDisplayFocus only: after starting Web UI drag with a stick click, release pointer up after the stick stays neutral this long. Press stick click again to release immediately.")]
    [Min(0f)]
    [SerializeField] private float webViewThumbstickClickDragNeutralReleaseDelay = 0.45f;

    public string LastClickResult { get; private set; } = "None";

    private TLabWebViewDisplayBridge activeWebViewDragBridge;
    private DisplaySurface activeWebViewDragDisplay;
    private Vector2 activeWebViewDragLastNormalized;
    private Vector2 activeWebViewDragAnchorNormalized;
    private InteractionCondition activeWebViewDragCondition;
    private WebViewPointerActivation activeWebViewPointerActivation;
    private WebViewStickGestureAxis activeWebViewStickGestureAxis;
    private float webViewRayNeutralStartTime = -1f;
    private float webViewThumbstickClickNeutralStartTime = -1f;
    private TLabWebViewDisplayBridge pendingWebViewDragBridge;
    private DisplaySurface pendingWebViewDragDisplay;
    private Vector2 pendingWebViewDragStartNormalized;
    private WebViewPointerActivation pendingWebViewDragActivation;
    private InteractionCondition pendingWebViewDragCondition;
    private float pendingWebViewThumbstickNeutralStartTime = -1f;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();

        if (inputManager == null || displayManager == null)
        {
            CancelWebViewPointerDrag();
            return;
        }

        if (inputManager.SecondaryButtonPressed)
        {
            CancelWebViewPointerDrag();
            return;
        }

        bool useStickTouchGesture = UsesStickTouchGestureMode();
        bool useDirectScrollAndSeek = UsesDirectScrollAndSeekMode();
        if (UpdateWebViewPointerInteraction(useStickTouchGesture, useDirectScrollAndSeek))
        {
            return;
        }

        bool submitRequested = ShouldDispatchClickThisFrame();
        if (!submitRequested)
        {
            return;
        }

        if (vrTaskMenuManager != null && TryGetWorldMenuRay(out Ray menuRay) && vrTaskMenuManager.TryHandleWorldClick(menuRay))
        {
            LastClickResult = "WorldMenu menu=True";
            return;
        }

        if (TryHandleDisplayMenuClick())
        {
            return;
        }

        if (raycastPointer != null
            && displayManager.TryGetForemostHit(raycastPointer.CurrentRay, out DisplayHit taskControlHit)
            && TryHandleTaskControlClick(taskControlHit.DisplayId, taskControlHit.Normalized))
        {
            LastClickResult = $"{taskControlHit.DisplayId} taskControl=True";
            return;
        }

        if (inputManager.CurrentCondition == InteractionCondition.RaycastBaseline)
        {
            DispatchRaycastBaselineClick();
            return;
        }

        if (inputManager.CurrentCondition == InteractionCondition.ExplicitDisplayFocus)
        {
            DispatchExplicitFocusClick();
        }
    }

    private void OnDisable()
    {
        CancelWebViewPointerDrag();
    }

    private bool UpdateWebViewPointerInteraction(
        bool useStickTouchGesture,
        bool useDirectScrollAndSeek)
    {
        if (!useDirectScrollAndSeek)
        {
            ClearPendingDirectWebViewDrag();
        }

        if (useStickTouchGesture)
        {
            return UpdateWebViewStickTouchGesture();
        }

        if (useDirectScrollAndSeek)
        {
            return UpdateDirectWebViewPointerDrag();
        }

        return UpdateLegacyWebViewSubmitDrag();
    }

    private bool UpdateDirectWebViewPointerDrag()
    {
        if (!enableWebViewPointerDrag)
        {
            CancelWebViewPointerDrag();
            return false;
        }

        if (activeWebViewDragBridge != null)
        {
            if (!IsWebViewDragContextValid(
                    activeWebViewDragBridge,
                    activeWebViewDragDisplay,
                    activeWebViewDragCondition)
                || ShouldEndDirectWebViewPointerDrag())
            {
                EndActiveWebViewPointerDrag();
                return true;
            }

            UpdateActiveWebViewPointerDrag();
            return true;
        }

        if (pendingWebViewDragBridge != null)
        {
            return UpdatePendingDirectWebViewDrag();
        }

        if (inputManager.CurrentCondition == InteractionCondition.RaycastBaseline
            && inputManager.TriggerPressed
            && TryLatchDirectWebViewDrag(WebViewPointerActivation.Trigger))
        {
            return true;
        }

        if (inputManager.CurrentCondition == InteractionCondition.ExplicitDisplayFocus
            && inputManager.StickClickPressed
            && !inputManager.TriggerHeld
            && TryLatchDirectWebViewDrag(WebViewPointerActivation.ThumbstickClick))
        {
            return true;
        }

        return false;
    }

    private bool UpdatePendingDirectWebViewDrag()
    {
        if (!IsWebViewDragContextValid(
                pendingWebViewDragBridge,
                pendingWebViewDragDisplay,
                pendingWebViewDragCondition))
        {
            ClearPendingDirectWebViewDrag();
            return true;
        }

        if (pendingWebViewDragActivation == WebViewPointerActivation.Trigger)
        {
            if (!inputManager.TriggerHeld)
            {
                ClearPendingDirectWebViewDrag();
                return true;
            }
        }
        else if (pendingWebViewDragActivation == WebViewPointerActivation.ThumbstickClick)
        {
            if (inputManager.StickClickPressed)
            {
                ClearPendingDirectWebViewDrag();
                return true;
            }

            if (inputManager.StickClickHeld
                || inputManager.Stick.magnitude >= webViewStickGestureDeadzone)
            {
                pendingWebViewThumbstickNeutralStartTime = -1f;
            }
            else if (pendingWebViewThumbstickNeutralStartTime < 0f)
            {
                pendingWebViewThumbstickNeutralStartTime = Time.unscaledTime;
            }
            else if (Time.unscaledTime - pendingWebViewThumbstickNeutralStartTime
                     >= Mathf.Max(0f, webViewThumbstickClickDragNeutralReleaseDelay))
            {
                ClearPendingDirectWebViewDrag();
                return true;
            }
        }

        Vector2 currentNormalized = GetWebViewDragPosition(
            pendingWebViewDragDisplay,
            pendingWebViewDragStartNormalized,
            pendingWebViewDragCondition);
        if (!HasReachedDirectDragStartThreshold(
                pendingWebViewDragDisplay,
                pendingWebViewDragStartNormalized,
                currentNormalized))
        {
            return true;
        }

        TLabWebViewDisplayBridge bridge = pendingWebViewDragBridge;
        DisplaySurface display = pendingWebViewDragDisplay;
        Vector2 startNormalized = pendingWebViewDragStartNormalized;
        WebViewPointerActivation activation = pendingWebViewDragActivation;
        ClearPendingDirectWebViewDrag();

        if (bridge == null || !bridge.TryPointerDown(startNormalized))
        {
            return true;
        }

        ActivateWebViewDrag(bridge, display, startNormalized, activation);
        UpdateActiveWebViewPointerDrag(currentNormalized);
        LastClickResult = $"{display.name} webViewDragStarted=True activation={activation}";
        return true;
    }

    private bool HasReachedDirectDragStartThreshold(
        DisplaySurface display,
        Vector2 startNormalized,
        Vector2 currentNormalized)
    {
        Vector2 canvasSize = display != null ? display.CanvasPixelSize : Vector2.one;
        canvasSize.x = Mathf.Max(1f, canvasSize.x);
        canvasSize.y = Mathf.Max(1f, canvasSize.y);
        Vector2 deltaPixels = Vector2.Scale(currentNormalized - startNormalized, canvasSize);
        return deltaPixels.magnitude >= Mathf.Max(0f, webViewDirectDragStartThresholdPixels);
    }

    private bool ShouldEndDirectWebViewPointerDrag()
    {
        if (inputManager == null)
        {
            return true;
        }

        switch (activeWebViewPointerActivation)
        {
            case WebViewPointerActivation.Trigger:
                return !inputManager.TriggerHeld;
            case WebViewPointerActivation.ThumbstickClick:
                return ShouldEndThumbstickClickWebViewDrag();
            default:
                return false;
        }
    }

    private bool ShouldEndThumbstickClickWebViewDrag()
    {
        if (inputManager.StickClickPressed)
        {
            webViewThumbstickClickNeutralStartTime = -1f;
            return true;
        }

        if (inputManager.Stick.magnitude >= webViewStickGestureDeadzone)
        {
            webViewThumbstickClickNeutralStartTime = -1f;
            return false;
        }

        if (webViewThumbstickClickNeutralStartTime < 0f)
        {
            webViewThumbstickClickNeutralStartTime = Time.unscaledTime;
            return false;
        }

        return Time.unscaledTime - webViewThumbstickClickNeutralStartTime
            >= Mathf.Max(0f, webViewThumbstickClickDragNeutralReleaseDelay);
    }

    private bool UpdateLegacyWebViewSubmitDrag()
    {
        if (!enableWebViewPointerDrag)
        {
            CancelWebViewPointerDrag();
            return false;
        }

        if (activeWebViewDragBridge != null)
        {
            if (!IsWebViewDragContextValid(
                activeWebViewDragBridge,
                activeWebViewDragDisplay,
                activeWebViewDragCondition))
            {
                EndActiveWebViewPointerDrag();
                return true;
            }

            if (inputManager.SubmitHeld)
            {
                UpdateActiveWebViewPointerDrag();
                return true;
            }

            EndActiveWebViewPointerDrag();
            return true;
        }

        if (inputManager.SubmitPressed && TryBeginSubmitWebViewDrag())
        {
            return true;
        }

        return false;
    }

    private bool UpdateWebViewStickTouchGesture()
    {
        if (activeWebViewPointerActivation == WebViewPointerActivation.Submit)
        {
            CancelWebViewPointerDrag();
            return true;
        }

        if (activeWebViewDragBridge != null)
        {
            if (!IsWebViewDragContextValid(
                    activeWebViewDragBridge,
                    activeWebViewDragDisplay,
                    activeWebViewDragCondition)
                || ShouldEndActiveWebViewStickGesture()
                || inputManager.SubmitHeld)
            {
                EndActiveWebViewPointerDrag();
                return true;
            }

            UpdateActiveWebViewStickTouchGesture();
            return true;
        }

        bool gestureRequested = IsWebViewStickGestureRequested();
        if (!gestureRequested || inputManager.SubmitHeld)
        {
            return false;
        }

        return TryBeginWebViewStickTouchGesture();
    }

    private bool TryBeginWebViewStickTouchGesture()
    {
        if (!TryGetCurrentWebViewPointerTarget(out DisplaySurface display, out Vector2 normalized))
        {
            return false;
        }

        TLabWebViewDisplayBridge webViewBridge = GetWebViewBridge(display);
        if (webViewBridge == null || !webViewBridge.TryPointerDown(normalized))
        {
            return false;
        }

        ActivateWebViewDrag(webViewBridge, display, normalized, WebViewPointerActivation.Stick);
        LastClickResult = $"{display.name} webViewPointerDown=True activation=Stick";
        UpdateActiveWebViewStickTouchGesture();
        return true;
    }

    private void UpdateActiveWebViewStickTouchGesture()
    {
        Vector2 stick = inputManager.Stick;
        if (stick.magnitude < webViewStickGestureDeadzone)
        {
            return;
        }

        if (invertWebViewStickGestureX)
        {
            stick.x *= -1f;
        }

        if (invertWebViewStickGestureY)
        {
            stick.y *= -1f;
        }

        if (activeWebViewStickGestureAxis == WebViewStickGestureAxis.None)
        {
            activeWebViewStickGestureAxis = Mathf.Abs(stick.x) > Mathf.Abs(stick.y)
                ? WebViewStickGestureAxis.Horizontal
                : WebViewStickGestureAxis.Vertical;
        }

        Vector2 canvasSize = activeWebViewDragDisplay != null
            ? activeWebViewDragDisplay.CanvasPixelSize
            : Vector2.one;
        canvasSize.x = Mathf.Max(1f, canvasSize.x);
        canvasSize.y = Mathf.Max(1f, canvasSize.y);

        Vector2 deltaPixels = stick * webViewStickGestureSpeedPixelsPerSecond * Time.deltaTime;
        Vector2 delta = new Vector2(deltaPixels.x / canvasSize.x, deltaPixels.y / canvasSize.y);
        if (activeWebViewStickGestureAxis == WebViewStickGestureAxis.Horizontal)
        {
            delta.y = 0f;
        }
        else
        {
            delta.x = 0f;
            if (!ReClutchVerticalWebViewGestureIfNeeded(delta.y))
            {
                CancelWebViewPointerDrag();
                return;
            }
        }

        Vector2 normalized = activeWebViewDragLastNormalized + delta;
        normalized.x = Mathf.Clamp01(normalized.x);
        normalized.y = Mathf.Clamp01(normalized.y);
        UpdateActiveWebViewPointerDrag(normalized);
        LastClickResult = $"{GetActiveWebViewDragDisplayId()} webViewGesture=True axis={activeWebViewStickGestureAxis}";
    }

    private bool ReClutchVerticalWebViewGestureIfNeeded(float verticalDelta)
    {
        if (Mathf.Approximately(verticalDelta, 0f) || activeWebViewDragBridge == null)
        {
            return true;
        }

        float edgeMargin = Mathf.Clamp(webViewVerticalGestureEdgeMargin, 0.01f, 0.2f);
        float nextY = activeWebViewDragLastNormalized.y + verticalDelta;
        bool reachesEdge = verticalDelta > 0f
            ? nextY >= 1f - edgeMargin
            : nextY <= edgeMargin;
        if (!reachesEdge)
        {
            return true;
        }

        activeWebViewDragBridge.TryPointerUp(activeWebViewDragLastNormalized);

        float reentry = Mathf.Clamp(webViewVerticalGestureReentry, edgeMargin + 0.01f, 0.49f);
        Vector2 restartNormalized = new Vector2(
            Mathf.Clamp(activeWebViewDragAnchorNormalized.x, edgeMargin, 1f - edgeMargin),
            verticalDelta > 0f ? reentry : 1f - reentry);

        bool restarted = activeWebViewDragBridge.TryPointerDown(restartNormalized);
        if (restarted)
        {
            activeWebViewDragLastNormalized = restartNormalized;
        }

        return restarted;
    }

    private bool IsWebViewStickGestureRequested()
    {
        if (inputManager == null || inputManager.Stick.magnitude < webViewStickGestureDeadzone)
        {
            return false;
        }

        if (inputManager.CurrentCondition == InteractionCondition.RaycastBaseline)
        {
            return true;
        }

        return inputManager.CurrentCondition == InteractionCondition.ExplicitDisplayFocus
            && inputManager.TriggerHeld;
    }

    private bool ShouldEndActiveWebViewStickGesture()
    {
        if (inputManager == null)
        {
            return true;
        }

        if (activeWebViewDragCondition == InteractionCondition.ExplicitDisplayFocus)
        {
            webViewRayNeutralStartTime = -1f;
            return !inputManager.TriggerHeld;
        }

        if (inputManager.Stick.magnitude >= webViewStickGestureDeadzone)
        {
            webViewRayNeutralStartTime = -1f;
            return false;
        }

        if (webViewRayNeutralStartTime < 0f)
        {
            webViewRayNeutralStartTime = Time.unscaledTime;
            return false;
        }

        return Time.unscaledTime - webViewRayNeutralStartTime
            >= Mathf.Max(0f, webViewRayNeutralReleaseDelay);
    }

    private bool TryBeginSubmitWebViewDrag()
    {
        if (!TryGetCurrentWebViewPointerTarget(out DisplaySurface display, out Vector2 normalized))
        {
            return false;
        }

        TLabWebViewDisplayBridge webViewBridge = GetWebViewBridge(display);
        if (webViewBridge == null || !webViewBridge.TryPointerDown(normalized))
        {
            return false;
        }

        ActivateWebViewDrag(webViewBridge, display, normalized, WebViewPointerActivation.Submit);
        LastClickResult = $"{display.name} webViewPointerDown=True activation=Submit";
        return true;
    }

    private bool TryLatchDirectWebViewDrag(WebViewPointerActivation activation)
    {
        if (!TryGetCurrentWebViewPointerTarget(out DisplaySurface display, out Vector2 normalized))
        {
            return false;
        }

        TLabWebViewDisplayBridge webViewBridge = GetWebViewBridge(display);
        if (webViewBridge == null)
        {
            return false;
        }

        pendingWebViewDragBridge = webViewBridge;
        pendingWebViewDragDisplay = display;
        pendingWebViewDragStartNormalized = normalized;
        pendingWebViewDragActivation = activation;
        pendingWebViewDragCondition = inputManager.CurrentCondition;
        pendingWebViewThumbstickNeutralStartTime = -1f;
        LastClickResult = $"{display.name} webViewDragLatched=True activation={activation}";
        return true;
    }

    private void ActivateWebViewDrag(
        TLabWebViewDisplayBridge bridge,
        DisplaySurface display,
        Vector2 normalized,
        WebViewPointerActivation activation)
    {
        activeWebViewDragBridge = bridge;
        activeWebViewDragDisplay = display;
        activeWebViewDragLastNormalized = normalized;
        activeWebViewDragAnchorNormalized = normalized;
        activeWebViewDragCondition = inputManager.CurrentCondition;
        activeWebViewPointerActivation = activation;
        activeWebViewStickGestureAxis = WebViewStickGestureAxis.None;
        webViewRayNeutralStartTime = -1f;
        webViewThumbstickClickNeutralStartTime = -1f;
    }

    private void UpdateActiveWebViewPointerDrag()
    {
        Vector2 normalized = GetWebViewDragPosition(
            activeWebViewDragDisplay,
            activeWebViewDragLastNormalized,
            activeWebViewDragCondition);
        UpdateActiveWebViewPointerDrag(normalized);
    }

    private void UpdateActiveWebViewPointerDrag(Vector2 normalized)
    {
        if (activeWebViewDragBridge.TryPointerMove(normalized))
        {
            activeWebViewDragLastNormalized = normalized;
        }

        LastClickResult = $"{GetActiveWebViewDragDisplayId()} webViewDrag=True activation={activeWebViewPointerActivation}";
    }

    private void EndActiveWebViewPointerDrag()
    {
        if (activeWebViewDragBridge == null)
        {
            return;
        }

        WebViewPointerActivation activation = activeWebViewPointerActivation;
        Vector2 normalized = GetWebViewDragPosition(
            activeWebViewDragDisplay,
            activeWebViewDragLastNormalized,
            activeWebViewDragCondition);
        if (activeWebViewDragBridge != null)
        {
            activeWebViewDragBridge.TryPointerUp(normalized);
        }

        LastClickResult = $"{GetActiveWebViewDragDisplayId()} webViewPointerUp=True activation={activation}";
        activeWebViewDragBridge = null;
        activeWebViewDragDisplay = null;
        activeWebViewPointerActivation = WebViewPointerActivation.None;
        activeWebViewStickGestureAxis = WebViewStickGestureAxis.None;
        webViewRayNeutralStartTime = -1f;
        webViewThumbstickClickNeutralStartTime = -1f;
    }

    private Vector2 GetWebViewDragPosition(
        DisplaySurface lockedDisplay,
        Vector2 fallbackNormalized,
        InteractionCondition condition)
    {
        if (activeWebViewPointerActivation == WebViewPointerActivation.Stick)
        {
            return fallbackNormalized;
        }

        if (condition == InteractionCondition.ExplicitDisplayFocus)
        {
            if (displayManager != null
                && displayManager.FocusedDisplay == lockedDisplay
                && virtualCursorController != null)
            {
                return virtualCursorController.NormalizedPosition;
            }

            return fallbackNormalized;
        }

        if (condition == InteractionCondition.RaycastBaseline
            && lockedDisplay != null
            && raycastPointer != null)
        {
            Ray ray = raycastPointer.CurrentRay;
            if (ray.direction != Vector3.zero
                && lockedDisplay.TryRayToClampedNormalized(ray, out Vector2 clampedNormalized))
            {
                return clampedNormalized;
            }
        }

        return fallbackNormalized;
    }

    private bool TryGetCurrentWebViewPointerTarget(out DisplaySurface display, out Vector2 normalized)
    {
        display = null;
        normalized = Vector2.zero;

        if (inputManager.CurrentCondition == InteractionCondition.RaycastBaseline)
        {
            if (raycastPointer == null || displayManager == null)
            {
                return false;
            }

            if (!displayManager.TryGetForemostHit(raycastPointer.CurrentRay, out DisplayHit hit) || hit.Display == null)
            {
                return false;
            }

            display = hit.Display;
            normalized = hit.Normalized;
            return HasInputConsumingWebView(display);
        }

        if (inputManager.CurrentCondition == InteractionCondition.ExplicitDisplayFocus)
        {
            display = displayManager != null ? displayManager.FocusedDisplay : null;
            if (display == null || virtualCursorController == null || !HasInputConsumingWebView(display))
            {
                display = null;
                return false;
            }

            normalized = virtualCursorController.NormalizedPosition;
            return true;
        }

        return false;
    }

    private static bool HasInputConsumingWebView(DisplaySurface display)
    {
        return IsActiveInputWebView(GetWebViewBridge(display));
    }

    private static bool IsActiveInputWebView(TLabWebViewDisplayBridge bridge)
    {
        return bridge != null
            && bridge.ConsumeExperimentInput
            && (bridge.IsWebViewEnabled || bridge.IsReady);
    }

    private bool IsWebViewDragContextValid(
        TLabWebViewDisplayBridge bridge,
        DisplaySurface display,
        InteractionCondition condition)
    {
        if (inputManager == null
            || inputManager.CurrentCondition != condition
            || display == null
            || !IsActiveInputWebView(bridge))
        {
            return false;
        }

        return condition != InteractionCondition.ExplicitDisplayFocus
            || (displayManager != null && displayManager.FocusedDisplay == display);
    }

    private string GetActiveWebViewDragDisplayId()
    {
        return activeWebViewDragDisplay != null ? activeWebViewDragDisplay.name : "None";
    }

    private void CancelWebViewPointerDrag()
    {
        ClearPendingDirectWebViewDrag();
        EndActiveWebViewPointerDrag();
    }

    private void ClearPendingDirectWebViewDrag()
    {
        pendingWebViewDragBridge = null;
        pendingWebViewDragDisplay = null;
        pendingWebViewDragActivation = WebViewPointerActivation.None;
        pendingWebViewDragCondition = default;
        pendingWebViewThumbstickNeutralStartTime = -1f;
    }

    private bool ShouldDispatchClickThisFrame()
    {
        return inputManager.SubmitReleased;
    }

    private bool UsesStickTouchGestureMode()
    {
        return webViewSessionManager != null
            && webViewSessionManager.IsSessionRunning
            && webViewSessionManager.UsesStickTouchGesture;
    }

    private bool UsesDirectScrollAndSeekMode()
    {
        return webViewSessionManager != null
            && webViewSessionManager.IsSessionRunning
            && webViewSessionManager.UsesDirectScrollAndSeek;
    }

    private void DispatchRaycastBaselineClick()
    {
        if (!displayManager.HasCurrentRaycastHit)
        {
            LastClickResult = "None valid=False target=None";
            NotifyTaskLayer("None", Vector2.zero, false);
            return;
        }

        DisplayHit hit = displayManager.CurrentRaycastHit;
        if (vrTaskMenuManager != null && vrTaskMenuManager.TryHandleClick(hit.DisplayId, hit.Normalized))
        {
            LastClickResult = $"{hit.DisplayId} menu=True";
            return;
        }

        if (TryHandleTaskControlClick(hit.DisplayId, hit.Normalized))
        {
            LastClickResult = $"{hit.DisplayId} taskControl=True";
            return;
        }

        if (TryHandleWebViewClick(hit.Display, hit.Normalized))
        {
            LastClickResult = $"{hit.DisplayId} webView=True";
            return;
        }

        LastClickResult = hit.DisplayId;
        NotifyTaskLayer(hit.DisplayId, hit.Normalized, hit.Display != null);
        Ray ray = raycastPointer != null ? raycastPointer.CurrentRay : default;
        LogClick(hit.DisplayId, hit.Normalized, ray);
    }

    private void DispatchExplicitFocusClick()
    {
        // 明示フォーカス条件では、視線位置ではなくロック済み表示の仮想カーソル位置をクリックする。
        DisplaySurface focusedDisplay = displayManager.FocusedDisplay;
        if (focusedDisplay == null || virtualCursorController == null)
        {
            LastClickResult = "None valid=False target=None";
            NotifyTaskLayer("None", Vector2.zero, false);
            return;
        }

        Vector2 normalized = virtualCursorController.NormalizedPosition;
        if (vrTaskMenuManager != null && vrTaskMenuManager.TryHandleClick(focusedDisplay.name, normalized))
        {
            LastClickResult = $"{focusedDisplay.name} menu=True";
            return;
        }

        if (TryHandleTaskControlClick(focusedDisplay.name, normalized))
        {
            LastClickResult = $"{focusedDisplay.name} taskControl=True";
            return;
        }

        if (TryHandleWebViewClick(focusedDisplay, normalized))
        {
            LastClickResult = $"{focusedDisplay.name} webView=True";
            return;
        }

        LastClickResult = focusedDisplay.name;
        NotifyTaskLayer(focusedDisplay.name, normalized, true);
        Ray gazeRay = gazeProvider != null ? gazeProvider.GetGazeRay() : default;
        bool gazeOnDifferentDisplay = focusManager != null && focusManager.IsGazeOnDifferentDisplay(focusedDisplay);

        if (logger != null)
        {
            logger.LogExplicitClick(
                inputManager.CurrentCondition,
                gazeProvider != null ? gazeProvider.CurrentGazeSource : GazeSource.HmdForward,
                focusManager != null ? focusManager.CurrentCandidateIds : "None",
                focusedDisplay.name,
                normalized,
                gazeOnDifferentDisplay,
                gazeRay.origin,
                gazeRay.direction);
        }
        else
        {
            Debug.Log($"[ClickDispatcher] condition={inputManager.CurrentCondition}, focusedDisplay={focusedDisplay.name}, normalized={Format(normalized)}, gazeOnDifferentDisplay={gazeOnDifferentDisplay}");
        }
    }

    private void ResolveReferences()
    {
        if (inputManager == null)
        {
            inputManager = PrototypeInputManager.Instance != null
                ? PrototypeInputManager.Instance
                : FindObjectOfType<PrototypeInputManager>();
        }

        if (displayManager == null)
        {
            displayManager = DisplayManager.Instance != null
                ? DisplayManager.Instance
                : FindObjectOfType<DisplayManager>();
        }

        if (raycastPointer == null)
        {
            raycastPointer = FindObjectOfType<RaycastPointer>();
        }

        if (focusManager == null)
        {
            focusManager = FindObjectOfType<FocusManager>();
        }

        if (virtualCursorController == null)
        {
            virtualCursorController = FindObjectOfType<VirtualCursorController>();
        }

        if (gazeProvider == null)
        {
            gazeProvider = FindObjectOfType<GazeProvider>();
        }

        if (focusPointingTaskManager == null)
        {
            focusPointingTaskManager = FindObjectOfType<FocusPointingTaskManager>();
        }

        if (webViewSessionManager == null)
        {
            webViewSessionManager = FindObjectOfType<WebViewSessionManager>();
        }

        if (vrTaskMenuManager == null)
        {
            vrTaskMenuManager = FindObjectOfType<VRTaskMenuManager>();
        }

        if (logger == null)
        {
            logger = FindObjectOfType<Logger>();
        }
    }

    private void NotifyTaskLayer(string clickedDisplayId, Vector2 normalized, bool hasValidDisplay)
    {
        if (inputManager == null)
        {
            return;
        }

        if (focusPointingTaskManager != null)
        {
            focusPointingTaskManager.HandleClick(new FocusPointingClickEvent
            {
                Condition = inputManager.CurrentCondition,
                ClickedDisplayId = clickedDisplayId,
                ClickedNormalizedPosition = normalized,
                Timestamp = Time.time,
                HasValidDisplay = hasValidDisplay
            });
        }

    }

    private bool TryHandleTaskControlClick(string displayId, Vector2 normalizedPosition)
    {
        return (focusPointingTaskManager != null
                && focusPointingTaskManager.TryHandleTaskControlClick(displayId, normalizedPosition))
            || (webViewSessionManager != null
                && webViewSessionManager.TryHandleTaskControlClick(displayId, normalizedPosition));
    }

    private bool TryHandleWebViewClick(DisplaySurface display, Vector2 normalizedPosition)
    {
        if (display == null)
        {
            return false;
        }

        TLabWebViewDisplayBridge webViewBridge = GetWebViewBridge(display);
        bool handled = webViewBridge != null && webViewBridge.TryClick(normalizedPosition);
        if (handled)
        {
            webViewSessionManager?.RecordWebViewClick(display.name, normalizedPosition);
        }

        return handled;
    }

    private static TLabWebViewDisplayBridge GetWebViewBridge(DisplaySurface display)
    {
        return display != null ? display.GetComponent<TLabWebViewDisplayBridge>() : null;
    }

    private void LogClick(string displayId, Vector2 normalized, Ray ray)
    {
        if (logger != null)
        {
            logger.LogRaycastClick(inputManager.CurrentCondition, displayId, normalized, ray.origin, ray.direction);
        }
        else
        {
            Debug.Log($"[ClickDispatcher] condition={inputManager.CurrentCondition}, displayId={displayId}, normalized={Format(normalized)}");
        }
    }

    private bool TryGetWorldMenuRay(out Ray ray)
    {
        // ディスプレイ外のEnd Training等は、視線条件では視線Ray、BaselineではコントローラRayで押す。
        if (inputManager != null
            && inputManager.CurrentCondition == InteractionCondition.ExplicitDisplayFocus
            && gazeProvider != null)
        {
            ray = gazeProvider.GetGazeRay();
            return ray.direction != Vector3.zero;
        }

        if (raycastPointer != null)
        {
            ray = raycastPointer.CurrentRay;
            return ray.direction != Vector3.zero;
        }

        ray = default;
        return false;
    }

    private bool TryHandleDisplayMenuClick()
    {
        if (vrTaskMenuManager == null || raycastPointer == null || displayManager == null)
        {
            return false;
        }

        if (!displayManager.TryGetForemostHit(raycastPointer.CurrentRay, out DisplayHit menuHit))
        {
            return false;
        }

        if (!vrTaskMenuManager.TryHandleClick(menuHit.DisplayId, menuHit.Normalized))
        {
            return false;
        }

        LastClickResult = $"{menuHit.DisplayId} menu=True";
        return true;
    }

    private static string Format(Vector2 value)
    {
        return $"({value.x:0.000}, {value.y:0.000})";
    }
}
