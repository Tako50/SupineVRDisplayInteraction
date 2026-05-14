using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PrototypeDebugVisualizer : MonoBehaviour
{
    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private GazeProvider gazeProvider;
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private FocusManager focusManager;
    [SerializeField] private VirtualCursorController virtualCursorController;
    [SerializeField] private ClickDispatcher clickDispatcher;
    [SerializeField] private ScrollController scrollController;
    [SerializeField] private ExperimentManager experimentManager;
    [SerializeField] private EditorDebugInputProvider debugInputProvider;
    [SerializeField] private Transform controllerRaySource;
    [SerializeField] private float rayLength = 4f;
    [SerializeField] private float hitPointRadius = 0.025f;
    [SerializeField] private bool showOverlay = true;
    [SerializeField] private bool useUnityUiOverlay = true;
    [SerializeField] private Text overlayText;

    private void Awake()
    {
        ResolveReferences();
        EnsureOverlayText();
    }

    private void Update()
    {
        ResolveReferences();
        UpdateUnityUiOverlay();
    }

    private void OnDrawGizmos()
    {
        ResolveReferences();

        if (displayManager == null)
        {
            return;
        }

        Ray controllerRay = GetControllerRay();
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(controllerRay.origin, controllerRay.origin + controllerRay.direction * rayLength);
        if (displayManager.TryGetForemostHit(controllerRay, out DisplayHit controllerHit))
        {
            Gizmos.DrawSphere(controllerHit.Hit.point, hitPointRadius);
        }

        if (gazeProvider != null)
        {
            Ray gazeRay = gazeProvider.GetGazeRay();
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

        GUI.Box(new Rect(12f, 12f, 540f, 292f), "Explicit Display Focus Debug");
        GUI.Label(new Rect(24f, 40f, 500f, 250f), BuildOverlayText());
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
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
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

    private string BuildOverlayText()
    {
        if (displayManager == null)
        {
            return "Explicit Display Focus Debug\nDisplayManager: missing";
        }

        Ray controllerRay = GetControllerRay();
        bool hasRayHit = displayManager.TryGetForemostHit(controllerRay, out DisplayHit rayHit);
        DisplayHit gazeHit = default;
        bool hasGazeHit = gazeProvider != null && displayManager.TryGetForemostHit(gazeProvider.GetGazeRay(), out gazeHit);
        string condition = inputManager != null ? inputManager.CurrentCondition.ToString() : "Unknown";
        string focusState = focusManager != null ? focusManager.CurrentState.ToString() : "Unknown";
        string candidates = focusManager != null ? focusManager.CurrentCandidateIds : "None";
        string focusedDisplay = displayManager.FocusedDisplay != null ? displayManager.FocusedDisplay.name : "None";
        string cursorPosition = virtualCursorController != null ? Format(virtualCursorController.NormalizedPosition) : "(n/a)";
        string layout = experimentManager != null ? experimentManager.CurrentLayout.ToString() : "Unknown";
        string gazeSource = gazeProvider != null ? gazeProvider.CurrentGazeSource.ToString() : "Unknown";
        string debugInput = debugInputProvider != null && debugInputProvider.IsEnabled ? "Enabled" : "Disabled";
        string lastClick = clickDispatcher != null ? clickDispatcher.LastClickResult : "None";
        string lastScroll = scrollController != null
            ? $"{scrollController.LastScrollDisplayId} {scrollController.LastScrollAmount:0.000}"
            : "None";

        return
            "Explicit Display Focus Debug\n" +
            $"Condition: {condition}\n" +
            $"Gaze source: {gazeSource}\n" +
            $"Focus state: {focusState}\n" +
            $"Focused display: {focusedDisplay}\n" +
            $"Gaze candidates: {candidates}\n" +
            $"Gaze hit: {(hasGazeHit ? gazeHit.DisplayId + " " + Format(gazeHit.Normalized) : "None")}\n" +
            $"Controller hit: {(hasRayHit ? rayHit.DisplayId + " " + Format(rayHit.Normalized) : "None")}\n" +
            $"Cursor normalized: {cursorPosition}\n" +
            $"Layout: {layout}\n" +
            $"Debug input: {debugInput}\n" +
            $"Last click: {lastClick}\n" +
            $"Last scroll: {lastScroll}";
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
}
