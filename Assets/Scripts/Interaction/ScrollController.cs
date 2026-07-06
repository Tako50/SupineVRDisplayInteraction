using UnityEngine;

[DisallowMultipleComponent]
/// <summary>
/// スクロール入力を現在の条件に応じて配送する。
/// BaselineはRayが当たっている表示、ExplicitDisplayFocusはフォーカス済み表示だけを対象にする。
/// </summary>
public class ScrollController : MonoBehaviour
{
    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private RaycastPointer raycastPointer;
    [SerializeField] private FocusManager focusManager;
    [SerializeField] private VirtualCursorController virtualCursorController;
    [SerializeField] private GazeProvider gazeProvider;
    [SerializeField] private Logger logger;
    [SerializeField] private WebViewSessionManager webViewSessionManager;
    [SerializeField] private float scrollLogInterval = 0.12f;
    [SerializeField] private float stickScrollDeadzone = 0.05f;

    private float lastScrollLogTime;

    public float LastScrollAmount { get; private set; }
    public string LastScrollDisplayId { get; private set; } = "None";

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();

        if (inputManager == null || displayManager == null)
        {
            return;
        }

        if (inputManager.CurrentCondition == InteractionCondition.RaycastBaseline)
        {
            ScrollRaycastBaseline();
            return;
        }

        if (inputManager.CurrentCondition == InteractionCondition.ExplicitDisplayFocus)
        {
            ScrollExplicitFocus();
        }
    }

    private void ScrollRaycastBaseline()
    {
        if (!displayManager.HasCurrentRaycastHit)
        {
            return;
        }

        DisplayHit hit = displayManager.CurrentRaycastHit;
        if (hit.Display == null)
        {
            return;
        }

        Vector2 stick = inputManager.Stick;
        float stickVertical = stick.y;
        if (Mathf.Abs(stickVertical) < stickScrollDeadzone)
        {
            return;
        }

        if (UsesDirectScrollAndSeekMode() && Mathf.Abs(stickVertical) <= Mathf.Abs(stick.x))
        {
            return;
        }

        if (HasActiveInputWebView(hit.Display))
        {
            // T2 Direct mode: RaycastBaseline uses trigger hold + Ray movement for Web UI drag.
            // While trigger is held, do not layer stick scrolling on top of that pointer drag.
            if (UsesDirectScrollAndSeekMode() && inputManager.TriggerHeld)
            {
                return;
            }

            // StickTouchGestureモードではClickDispatcherがWebViewへpointer down/move/upを送る。
            // ここからScrollByを重ねず、WebView自身にscroll/dragの意味を決めさせる。
            if (UsesStickTouchGestureMode())
            {
                return;
            }

            // A保持中はPointer Dragを優先し、ページスクロールを重ねない。
            if (inputManager.SubmitHeld)
            {
                return;
            }

            if (TryHandleWebViewScroll(
                hit.Display,
                stickVertical,
                Time.deltaTime,
                hit.Normalized,
                out float webViewScrollPixels))
            {
                LastScrollAmount = webViewScrollPixels;
                LastScrollDisplayId = hit.DisplayId;
            }

            return;
        }

        float appliedScroll = hit.Display.Scroll(stickVertical, Time.deltaTime);
        if (Mathf.Approximately(appliedScroll, 0f))
        {
            return;
        }

        LastScrollAmount = appliedScroll;
        LastScrollDisplayId = hit.DisplayId;

        if (Time.time - lastScrollLogTime >= scrollLogInterval)
        {
            lastScrollLogTime = Time.time;
            Ray ray = raycastPointer != null ? raycastPointer.CurrentRay : default;
            if (logger != null)
            {
                logger.LogRaycastScroll(
                    inputManager.CurrentCondition,
                    hit.DisplayId,
                    hit.Normalized,
                    appliedScroll,
                    ray.origin,
                    ray.direction);
            }
            else
            {
                Debug.Log($"[ScrollController] condition={inputManager.CurrentCondition}, displayId={hit.DisplayId}, normalized={Format(hit.Normalized)}, scrollAmount={appliedScroll:0.000}");
            }
        }
    }

    private void ScrollExplicitFocus()
    {
        DisplaySurface focusedDisplay = displayManager.FocusedDisplay;
        if (focusedDisplay == null)
        {
            return;
        }

        // A保持中とstick押し込み中は汎用ドラッグを優先する。
        // トリガー＋上下は表示種類によらず相対スクロールに使う。
        if (!inputManager.TriggerHeld || inputManager.SubmitHeld || inputManager.StickClickHeld)
        {
            return;
        }

        Vector2 stick = inputManager.Stick;
        float stickVertical = stick.y;
        if (Mathf.Abs(stickVertical) < stickScrollDeadzone)
        {
            return;
        }

        if (UsesDirectScrollAndSeekMode() && Mathf.Abs(stickVertical) <= Mathf.Abs(stick.x))
        {
            return;
        }

        Vector2 normalized = virtualCursorController != null
            ? virtualCursorController.NormalizedPosition
            : new Vector2(0.5f, 0.5f);

        if (HasActiveInputWebView(focusedDisplay))
        {
            if (UsesStickTouchGestureMode())
            {
                return;
            }

            if (TryHandleWebViewScroll(
                focusedDisplay,
                stickVertical,
                Time.deltaTime,
                normalized,
                out float webViewScrollPixels))
            {
                LastScrollAmount = webViewScrollPixels;
                LastScrollDisplayId = focusedDisplay.name;
            }

            return;
        }

        float appliedScroll = focusedDisplay.Scroll(stickVertical, Time.deltaTime);
        if (Mathf.Approximately(appliedScroll, 0f))
        {
            return;
        }

        LastScrollAmount = appliedScroll;
        LastScrollDisplayId = focusedDisplay.name;

        if (Time.time - lastScrollLogTime >= scrollLogInterval)
        {
            lastScrollLogTime = Time.time;
            Ray gazeRay = gazeProvider != null ? gazeProvider.GetGazeRay() : default;
            bool gazeOnDifferentDisplay = focusManager != null && focusManager.IsGazeOnDifferentDisplay(focusedDisplay);
            if (logger != null)
            {
                logger.LogExplicitScroll(
                    inputManager.CurrentCondition,
                    gazeProvider != null ? gazeProvider.CurrentGazeSource : GazeSource.HmdForward,
                    focusManager != null ? focusManager.CurrentCandidateIds : "None",
                    focusedDisplay.name,
                    normalized,
                    appliedScroll,
                    gazeOnDifferentDisplay,
                    gazeRay.origin,
                    gazeRay.direction);
            }
            else
            {
                Debug.Log($"[ScrollController] condition={inputManager.CurrentCondition}, focusedDisplay={focusedDisplay.name}, normalized={Format(normalized)}, scrollAmount={appliedScroll:0.000}, gazeOnDifferentDisplay={gazeOnDifferentDisplay}");
            }
        }
    }

    private static bool TryHandleWebViewScroll(
        DisplaySurface display,
        float stickVertical,
        float deltaTime,
        Vector2 normalizedPosition,
        out float appliedScrollPixels)
    {
        appliedScrollPixels = 0f;
        TLabWebViewDisplayBridge bridge = GetWebViewBridge(display);
        return IsActiveInputWebView(bridge)
            && bridge.TryScroll(stickVertical, deltaTime, normalizedPosition, out appliedScrollPixels);
    }

    private static bool HasActiveInputWebView(DisplaySurface display)
    {
        return IsActiveInputWebView(GetWebViewBridge(display));
    }

    private static bool IsActiveInputWebView(TLabWebViewDisplayBridge bridge)
    {
        return bridge != null
            && bridge.ConsumeExperimentInput
            && (bridge.IsWebViewEnabled || bridge.IsReady);
    }

    private static TLabWebViewDisplayBridge GetWebViewBridge(DisplaySurface display)
    {
        return display != null ? display.GetComponent<TLabWebViewDisplayBridge>() : null;
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

        if (logger == null)
        {
            logger = FindObjectOfType<Logger>();
        }

        if (webViewSessionManager == null)
        {
            webViewSessionManager = FindObjectOfType<WebViewSessionManager>();
        }
    }

    private static string Format(Vector2 value)
    {
        return $"({value.x:0.000}, {value.y:0.000})";
    }
}
