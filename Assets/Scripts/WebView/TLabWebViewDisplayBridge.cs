using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public enum TLabCaptureModePreference
{
    HardwareBuffer,
    ByteBuffer,
    Surface
}

[DisallowMultipleComponent]
/// <summary>
/// Optional bridge from DisplaySurface input coordinates to TLabWebView.
/// TLab is loaded through reflection so the project still compiles before the UPM package is resolved.
/// </summary>
public class TLabWebViewDisplayBridge : MonoBehaviour
{
    private const string BrowserPrefabResourcesPath = "TLab/WebView/Browser";
    private const string BrowserContainerTypeName = "TLab.WebView.BrowserContainer";
    private const string BrowserTypeName = "TLab.WebView.Browser";
    private const string CaptureModeTypeName = "TLab.WebView.CaptureMode";
    private const int TouchActionDown = 0;
    private const int TouchActionUp = 1;
    private const int TouchActionDrag = 2;

    [Header("References")]
    [SerializeField] private DisplaySurface displaySurface;

    [Header("Startup")]
    [SerializeField] private bool enableOnStart = false;
    [SerializeField] private string initialUrl = "https://www.youtube.com";
    [SerializeField] private bool autoCreateTLabPrefab = true;
    [Tooltip("If true, existing DisplaySurface click/scroll input is consumed and sent to the WebView.")]
    [SerializeField] private bool consumeExperimentInput = true;

    [Header("TLab Prefab Setup")]
    [SerializeField] private Vector2Int viewSize = new Vector2Int(960, 540);
    [SerializeField] private Vector2Int textureSize = new Vector2Int(1920, 1080);
    [SerializeField] private bool matchDisplayAspect = true;
    [Min(1)]
    [SerializeField] private int fps = 24;
    [SerializeField] private bool limitTextureUpdatesToFps = true;
    [SerializeField] private TLabCaptureModePreference captureMode = TLabCaptureModePreference.HardwareBuffer;
    [Min(1f)]
    [SerializeField] private float initializationTimeoutSeconds = 12f;
    [SerializeField] private bool showEditorPlaceholder = true;

    [Header("Keyboard")]
    [Tooltip("TLab's bundled keyboard is screen-layout oriented. Keep this off for the VR display-local keyboard.")]
    [SerializeField] private bool useBuiltInTLabKeyboard = false;
    [Tooltip("Enables this project's display-local keyboard. When Show Keyboard Only For Text Input is true, this stays hidden until a web text field is focused.")]
    [SerializeField] private bool showDisplayKeyboard = true;
    [SerializeField] private bool showKeyboardOnlyForTextInput = true;
    [Range(0.18f, 0.55f)]
    [SerializeField] private float displayKeyboardHeightNormalized = 0.34f;
    [SerializeField] private int keyboardSortingOrder = 30;
    [SerializeField] private int cursorSortingOrderAboveKeyboard = 31;
    [SerializeField] private Color keyboardBackgroundColor = new Color(0.02f, 0.03f, 0.05f, 0.86f);
    [SerializeField] private Color keyboardKeyColor = new Color(0.16f, 0.19f, 0.23f, 0.96f);
    [SerializeField] private Color keyboardTextColor = Color.white;
    [SerializeField] private Color keyboardTextOutlineColor = new Color(0f, 0f, 0f, 0.88f);
    [Min(0.05f)]
    [SerializeField] private float keyboardFocusBridgeRefreshSeconds = 0.75f;
    [SerializeField] private bool logKeyboardFocusChanges = false;

    [Header("Input Mapping")]
    [SerializeField] private float scrollPixelsPerSecond = 900f;
    [SerializeField] private bool invertScrollDirection = true;
    [Tooltip("Horizontal trigger+stick seek speed for HTML video elements.")]
    [SerializeField] private float seekSecondsPerSecond = 30f;
    [SerializeField] private bool logInputEvents = false;

    [Header("Shutdown")]
    [Tooltip("Loads a blank page before disabling so video/audio playback is stopped immediately.")]
    [SerializeField] private bool loadBlankPageOnDisable = true;
    [SerializeField] private string blankPageUrl = "about:blank";
    [Tooltip("Destroys the TLab Browser prefab on disable. This releases the native WebView and stops media playback.")]
    [SerializeField] private bool destroyPrefabOnDisable = true;

    private GameObject browserRoot;
    private Component browserContainer;
    private object browser;
    private Coroutine initializationCoroutine;
    private GameObject editorPlaceholder;
    private RectTransform displayKeyboardRoot;
    private bool initialized;
    private bool keyboardInputFocused;
    private float nextKeyboardFocusBridgeRefreshTime;
    private float nextTextureUpdateTime;
    private string keyboardFocusBridgeJavaScript;
    private bool pointerDownActive;
    private long activePointerDownTime;
    private Vector2 activePointerNormalized;

    public bool IsWebViewEnabled => enableOnStart;
    public bool IsReady => initialized && browser != null;
    public bool ConsumeExperimentInput => consumeExperimentInput;
    public string InitialUrl => initialUrl;
    public string Status { get; private set; } = "Disabled";

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        if (enableOnStart)
        {
            EnableWebView();
        }
    }

    private void Update()
    {
        if (!initialized || browser == null)
        {
            return;
        }

        TryInvoke(browser, "DispatchMessageQueue");
        if (ShouldUpdateTextureThisFrame())
        {
            TryInvoke(browser, "UpdateFrame");
        }

        RefreshKeyboardFocusBridgeIfNeeded();
    }

    private void OnDisable()
    {
        DisableWebView();
    }

    public void EnableWebView()
    {
        enableOnStart = true;
        ResolveReferences();
        keyboardInputFocused = false;
        nextTextureUpdateTime = 0f;
        SetDisplayKeyboardVisible(ShouldShowKeyboard());

        if (Application.isEditor)
        {
            if (showEditorPlaceholder)
            {
                ShowEditorPlaceholder();
            }

            Status = "TLabWebView is Android-device only. Showing Editor placeholder.";
            Debug.Log($"[TLabWebViewDisplayBridge] {Status}");
            return;
        }

        if (initializationCoroutine == null)
        {
            initializationCoroutine = StartCoroutine(InitializeTLabWebView());
        }
    }

    public void DisableWebView()
    {
        enableOnStart = false;

        if (initializationCoroutine != null)
        {
            StopCoroutine(initializationCoroutine);
            initializationCoroutine = null;
        }

        StopPagePlayback();
        HideEditorPlaceholder();
        SetDisplayKeyboardVisible(false);

        if (browserRoot != null)
        {
            if (destroyPrefabOnDisable)
            {
                Destroy(browserRoot);
                browserRoot = null;
                browserContainer = null;
                browser = null;
            }
            else
            {
                browserRoot.SetActive(false);
            }
        }

        if (destroyPrefabOnDisable && displayKeyboardRoot != null)
        {
            Destroy(displayKeyboardRoot.gameObject);
            displayKeyboardRoot = null;
        }

        initialized = false;
        Status = "Disabled";
    }

    public void LoadUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        initialUrl = url.Trim();
        if (initialized && browser != null && TryInvoke(browser, "LoadUrl", initialUrl))
        {
            Status = $"Loaded URL: {initialUrl}";
        }
    }

    public bool TryEvaluateJavaScript(string javaScript)
    {
        return !string.IsNullOrWhiteSpace(javaScript)
            && CanReceiveInput()
            && TryInvoke(browser, "EvaluateJS", javaScript);
    }

    public bool TryGoBack()
    {
        return CanReceiveInput() && TryInvoke(browser, "GoBack");
    }

    public bool TryClick(Vector2 displayNormalized)
    {
        if (TryHandleDisplayKeyboardClick(displayNormalized))
        {
            return consumeExperimentInput;
        }

        if (!CanReceiveInput())
        {
            return false;
        }

        Vector2Int size = ResolveViewSize();
        Vector2 webPoint = ToWebNormalizedPoint(displayNormalized);
        int x = Mathf.Clamp(Mathf.RoundToInt(webPoint.x * size.x), 0, Mathf.Max(0, size.x - 1));
        int y = Mathf.Clamp(Mathf.RoundToInt(webPoint.y * size.y), 0, Mathf.Max(0, size.y - 1));

        object downTimeObject = InvokeAndReturn(browser, "TouchEvent", x, y, 0, 0L);
        long downTime = downTimeObject is long value ? value : 0L;
        bool sent = InvokeAndReturn(browser, "TouchEvent", x, y, 1, downTime) != null;
        if (sent && logInputEvents)
        {
            Debug.Log($"[TLabWebViewDisplayBridge] click display={name}, normalized={Format(displayNormalized)}, pixel=({x}, {y})");
        }

        RequestKeyboardFocusBridgeRefresh();
        return sent && consumeExperimentInput;
    }

    public bool TryScroll(float stickVertical, float deltaTime, Vector2 displayNormalized, out float appliedScrollPixels)
    {
        appliedScrollPixels = 0f;
        if (!CanReceiveInput() || Mathf.Approximately(stickVertical, 0f))
        {
            return false;
        }

        float scrollPixels = stickVertical * scrollPixelsPerSecond * Mathf.Max(0f, deltaTime);
        if (invertScrollDirection)
        {
            scrollPixels *= -1f;
        }

        int roundedPixels = Mathf.RoundToInt(scrollPixels);
        if (roundedPixels == 0)
        {
            return false;
        }

        // TLab's native ScrollBy is not reliable on the Quest WebView used by T2.
        // EvaluateJS is already the proven path for direct seek, so update the root
        // scrolling element through the same bridge. Assigning scrollTop directly
        // also avoids the page's CSS smooth-scroll animation being restarted every frame.
        bool sent = TryInvoke(browser, "EvaluateJS", BuildPageScrollJavaScript(roundedPixels));
        if (!sent)
        {
            sent = TryInvoke(browser, "ScrollBy", 0, roundedPixels);
        }

        if (!sent)
        {
            return false;
        }

        appliedScrollPixels = roundedPixels;
        if (logInputEvents)
        {
            Debug.Log($"[TLabWebViewDisplayBridge] scroll display={name}, pixels={roundedPixels:0.0}, normalized={Format(displayNormalized)}");
        }

        return consumeExperimentInput;
    }

    public bool TrySeekHorizontal(float stickHorizontal, float deltaTime, Vector2 displayNormalized, out float appliedSeekSeconds)
    {
        appliedSeekSeconds = 0f;
        if (!CanReceiveInput() || Mathf.Approximately(stickHorizontal, 0f))
        {
            return false;
        }

        float seekSeconds = stickHorizontal * seekSecondsPerSecond * Mathf.Max(0f, deltaTime);
        if (Mathf.Approximately(seekSeconds, 0f))
        {
            return false;
        }

        bool sent = TryInvoke(browser, "EvaluateJS", BuildVideoSeekJavaScript(seekSeconds));
        if (!sent)
        {
            return false;
        }

        appliedSeekSeconds = seekSeconds;
        if (logInputEvents)
        {
            Debug.Log($"[TLabWebViewDisplayBridge] seek display={name}, seconds={seekSeconds:0.00}, normalized={Format(displayNormalized)}");
        }

        return consumeExperimentInput;
    }

    public bool TryPointerDown(Vector2 displayNormalized)
    {
        if (IsDisplayKeyboardArea(displayNormalized) || !CanReceiveInput())
        {
            return false;
        }

        if (pointerDownActive)
        {
            TryPointerUp(activePointerNormalized);
        }

        Vector2Int size = ResolveViewSize();
        Vector2 pixel = ToWebPixelPoint(displayNormalized, size);
        object downTimeObject = InvokeAndReturn(
            browser,
            "TouchEvent",
            Mathf.RoundToInt(pixel.x),
            Mathf.RoundToInt(pixel.y),
            TouchActionDown,
            activePointerDownTime);

        if (downTimeObject == null)
        {
            return false;
        }

        activePointerDownTime = downTimeObject is long value ? value : activePointerDownTime;
        activePointerNormalized = displayNormalized;
        pointerDownActive = true;

        if (logInputEvents)
        {
            Debug.Log($"[TLabWebViewDisplayBridge] pointerDown display={name}, normalized={Format(displayNormalized)}, pixel=({pixel.x:0}, {pixel.y:0})");
        }

        return consumeExperimentInput;
    }

    public bool TryPointerMove(Vector2 displayNormalized)
    {
        if (!pointerDownActive || !CanReceiveInput())
        {
            return false;
        }

        Vector2Int size = ResolveViewSize();
        Vector2 pixel = ToWebPixelPoint(displayNormalized, size);
        bool sent = TryInvoke(
            browser,
            "TouchEvent",
            Mathf.RoundToInt(pixel.x),
            Mathf.RoundToInt(pixel.y),
            TouchActionDrag,
            activePointerDownTime);

        if (!sent)
        {
            return false;
        }

        activePointerNormalized = displayNormalized;
        return consumeExperimentInput;
    }

    public bool TryPointerUp(Vector2 displayNormalized)
    {
        if (!pointerDownActive || !CanReceiveInput())
        {
            pointerDownActive = false;
            return false;
        }

        Vector2Int size = ResolveViewSize();
        Vector2 pixel = ToWebPixelPoint(displayNormalized, size);
        bool sent = TryInvoke(
            browser,
            "TouchEvent",
            Mathf.RoundToInt(pixel.x),
            Mathf.RoundToInt(pixel.y),
            TouchActionUp,
            activePointerDownTime);

        pointerDownActive = false;
        activePointerNormalized = displayNormalized;

        if (sent && logInputEvents)
        {
            Debug.Log($"[TLabWebViewDisplayBridge] pointerUp display={name}, normalized={Format(displayNormalized)}, pixel=({pixel.x:0}, {pixel.y:0})");
        }

        return sent && consumeExperimentInput;
    }

    private IEnumerator InitializeTLabWebView()
    {
        Type containerType = FindType(BrowserContainerTypeName);
        Type browserType = FindType(BrowserTypeName);
        if (containerType == null || browserType == null)
        {
            Status = "TLabWebView package not found. Add com.tlabaltoh.webview from the UPM git URL.";
            Debug.LogWarning($"[TLabWebViewDisplayBridge] {Status}");
            initializationCoroutine = null;
            yield break;
        }

        browserContainer = FindExistingContainer(containerType);
        if (browserContainer == null && autoCreateTLabPrefab)
        {
            browserContainer = CreateTLabPrefab(containerType);
        }

        if (browserContainer == null)
        {
            Status = "No TLab Browser prefab found or created.";
            Debug.LogWarning($"[TLabWebViewDisplayBridge] {Status}");
            initializationCoroutine = null;
            yield break;
        }

        browserRoot = browserContainer.gameObject;
        browserRoot.SetActive(true);
        ConfigurePrefabTransform(browserRoot);

        browser = GetMemberValue(browserContainer, "browser");
        if (browser == null)
        {
            browser = FindBrowserComponent(browserRoot, browserType);
            SetMemberIfAvailable(browserContainer, "m_browser", browser);
        }

        if (browser == null)
        {
            Status = "TLab Browser component was not found.";
            Debug.LogWarning($"[TLabWebViewDisplayBridge] {Status}");
            initializationCoroutine = null;
            yield break;
        }

        ConfigureBrowserFields(browser);
        Status = "Initializing TLabWebView...";
        Debug.Log($"[TLabWebViewDisplayBridge] Initializing TLabWebView on {name}");

        object initResult = InvokeAndReturn(browser, "Init", viewSize, textureSize);
        if (initResult == null)
        {
            Status = "TLab Browser.Init(Vector2Int, Vector2Int) was not available.";
            Debug.LogWarning($"[TLabWebViewDisplayBridge] {Status}");
            initializationCoroutine = null;
            yield break;
        }

        float timeoutAt = Time.realtimeSinceStartup + initializationTimeoutSeconds;
        while (!IsBrowserInitialized(browser))
        {
            if (Time.realtimeSinceStartup >= timeoutAt)
            {
                Status = $"TLabWebView initialization timed out after {initializationTimeoutSeconds:0.0}s.";
                Debug.LogWarning($"[TLabWebViewDisplayBridge] {Status}");
                initializationCoroutine = null;
                yield break;
            }

            yield return null;
        }

        initialized = true;
        if (!string.IsNullOrWhiteSpace(initialUrl))
        {
            TryInvoke(browser, "LoadUrl", initialUrl);
        }

        InstallKeyboardFocusBridge();
        Status = $"Ready: {initialUrl}";
        Debug.Log($"[TLabWebViewDisplayBridge] {name} ready url={initialUrl}");
        initializationCoroutine = null;
    }

    private Component CreateTLabPrefab(Type containerType)
    {
        GameObject prefab = Resources.Load<GameObject>(BrowserPrefabResourcesPath);
        if (prefab == null)
        {
            return null;
        }

        GameObject instance = Instantiate(prefab, ResolveCanvasParent(), false);
        instance.name = "TLab_Browser";
        ConfigurePrefabTransform(instance);
        return instance.GetComponentInChildren(containerType, true) as Component;
    }

    private void ConfigurePrefabTransform(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        Transform parent = ResolveCanvasParent();
        root.transform.SetParent(parent, false);

        RectTransform rootRect = root.transform as RectTransform;
        StretchRectTransform(rootRect);

        Canvas rootCanvas = root.GetComponent<Canvas>();
        if (rootCanvas != null)
        {
            rootCanvas.renderMode = RenderMode.WorldSpace;
            rootCanvas.worldCamera = ResolveCanvasCamera();
            rootCanvas.overrideSorting = true;
            rootCanvas.sortingOrder = 10;
        }

        Transform view = FindChildRecursive(root.transform, "View");
        if (view != null)
        {
            StretchRectTransform(view as RectTransform);
            view.SetAsFirstSibling();
        }

        ConfigureBuiltInKeyboard(root);
        SetDisplayKeyboardVisible(ShouldShowKeyboard());
    }

    private void ConfigureBrowserFields(object targetBrowser)
    {
        ApplyDisplayAspectToResolution();
        SetMemberIfAvailable(targetBrowser, "m_url", initialUrl);
        SetMemberIfAvailable(targetBrowser, "m_fps", Mathf.Max(1, fps));
        SetMemberIfAvailable(targetBrowser, "m_viewSize", viewSize);
        SetMemberIfAvailable(targetBrowser, "m_texSize", textureSize);

        Type captureModeType = FindType(CaptureModeTypeName);
        if (captureModeType != null)
        {
            object captureModeValue = Enum.Parse(captureModeType, captureMode.ToString());
            SetMemberIfAvailable(targetBrowser, "m_captureMode", captureModeValue);
        }
    }

    private bool ShouldUpdateTextureThisFrame()
    {
        if (!limitTextureUpdatesToFps)
        {
            return true;
        }

        float now = Time.unscaledTime;
        if (now < nextTextureUpdateTime)
        {
            return false;
        }

        float interval = 1f / Mathf.Max(1, fps);
        nextTextureUpdateTime = now + interval;
        return true;
    }

    private void ApplyDisplayAspectToResolution()
    {
        if (!matchDisplayAspect)
        {
            return;
        }

        Vector2 canvasSize = displaySurface != null
            ? displaySurface.GetCanvasSize()
            : new Vector2(16f, 9f);

        float aspect = canvasSize.y > 0f ? canvasSize.x / canvasSize.y : 16f / 9f;
        aspect = Mathf.Clamp(aspect, 0.5f, 4f);

        viewSize = MatchAspect(viewSize, aspect);
        textureSize = MatchAspect(textureSize, aspect);
    }

    private static Vector2Int MatchAspect(Vector2Int size, float aspect)
    {
        int width = Mathf.Max(16, size.x);
        int height = Mathf.Max(16, Mathf.RoundToInt(width / aspect));
        return new Vector2Int(width, height);
    }

    private void ConfigureBuiltInKeyboard(GameObject root)
    {
        Transform builtInKeyboard = FindChildRecursive(root.transform, "VKeyborad");
        if (builtInKeyboard != null)
        {
            builtInKeyboard.gameObject.SetActive(useBuiltInTLabKeyboard);
        }

        Component[] components = root.GetComponentsInChildren<Component>(true);
        for (int index = 0; index < components.Length; index++)
        {
            Component component = components[index];
            if (component == null)
            {
                continue;
            }

            string typeName = component.GetType().FullName;
            if (typeName == "TLab.WebView.BrowserInputField" || typeName == "TLab.WebView.SearchBar")
            {
                SetMemberIfAvailable(component, "m_activeOnAwake", useBuiltInTLabKeyboard);
            }
        }
    }

    private void StopPagePlayback()
    {
        if (browser == null || !initialized || !loadBlankPageOnDisable || string.IsNullOrWhiteSpace(blankPageUrl))
        {
            return;
        }

        TryInvoke(browser, "LoadUrl", blankPageUrl.Trim());
    }

    private bool CanReceiveInput()
    {
        return enableOnStart
            && consumeExperimentInput
            && initialized
            && browser != null
            && gameObject.activeInHierarchy
            && enabled;
    }

    private Vector2Int ResolveViewSize()
    {
        object value = GetMemberValue(browser, "viewSize");
        return value is Vector2Int resolved ? resolved : viewSize;
    }

    private Vector2 ToWebPixelPoint(Vector2 displayNormalized, Vector2Int size)
    {
        Vector2 webPoint = ToWebNormalizedPoint(displayNormalized);
        return new Vector2(
            Mathf.Clamp(Mathf.Round(webPoint.x * size.x), 0f, Mathf.Max(0, size.x - 1)),
            Mathf.Clamp(Mathf.Round(webPoint.y * size.y), 0f, Mathf.Max(0, size.y - 1)));
    }

    private static string BuildVideoSeekJavaScript(float seekSeconds)
    {
        string seconds = seekSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        return "(function(){"
            + "var v=document.querySelector('video');"
            + "if(!v||!isFinite(v.duration)||v.duration<=0){return false;}"
            + $"var t=Math.max(0,Math.min(v.duration,(v.currentTime||0)+({seconds})));"
            + "v.currentTime=t;"
            + "v.dispatchEvent(new Event('timeupdate',{bubbles:true}));"
            + "return true;"
            + "})();";
    }

    private static string BuildPageScrollJavaScript(int scrollPixels)
    {
        string pixels = scrollPixels.ToString(CultureInfo.InvariantCulture);
        return "(function(){"
            + "var e=document.scrollingElement||document.documentElement||document.body;"
            + "if(!e){return false;}"
            + "var before=e.scrollTop||0;"
            + $"var max=Math.max(0,(e.scrollHeight||0)-(e.clientHeight||0));var next=Math.max(0,Math.min(max,before+({pixels})));"
            + "e.scrollTop=next;"
            + "return next!==before;"
            + "})();";
    }

    private bool IsBrowserInitialized(object targetBrowser)
    {
        object result = InvokeAndReturn(targetBrowser, "IsInitialized");
        if (result is bool boolResult && boolResult)
        {
            return true;
        }

        object state = GetMemberValue(targetBrowser, "state");
        return state != null && string.Equals(state.ToString(), "Initialized", StringComparison.Ordinal);
    }

    private void SetDisplayKeyboardVisible(bool visible)
    {
        if (!showDisplayKeyboard)
        {
            visible = false;
        }

        if (visible)
        {
            EnsureDisplayKeyboard();
        }

        if (displayKeyboardRoot != null)
        {
            displayKeyboardRoot.gameObject.SetActive(visible);
            displayKeyboardRoot.SetAsLastSibling();
        }

        if (visible)
        {
            BringDisplayCursorAboveKeyboard();
        }
    }

    private bool ShouldShowKeyboard()
    {
        return showDisplayKeyboard && (!showKeyboardOnlyForTextInput || keyboardInputFocused);
    }

    private void BringDisplayCursorAboveKeyboard()
    {
        ResolveReferences();
        if (displaySurface == null || displaySurface.Cursor == null)
        {
            return;
        }

        displaySurface.BringCursorToFront();

        Canvas cursorCanvas = displaySurface.Cursor.GetComponent<Canvas>();
        if (cursorCanvas == null)
        {
            cursorCanvas = displaySurface.Cursor.gameObject.AddComponent<Canvas>();
        }

        cursorCanvas.overrideSorting = true;
        cursorCanvas.sortingOrder = Mathf.Max(cursorSortingOrderAboveKeyboard, keyboardSortingOrder + 1);
    }

    private void EnsureDisplayKeyboard()
    {
        if (displayKeyboardRoot != null)
        {
            return;
        }

        GameObject root = new GameObject("DisplayLocalKeyboard", typeof(RectTransform));
        root.transform.SetParent(ResolveCanvasParent(), false);
        displayKeyboardRoot = root.GetComponent<RectTransform>();
        float height = Mathf.Clamp(displayKeyboardHeightNormalized, 0.18f, 0.55f);
        displayKeyboardRoot.anchorMin = Vector2.zero;
        displayKeyboardRoot.anchorMax = new Vector2(1f, height);
        displayKeyboardRoot.pivot = new Vector2(0.5f, 0f);
        displayKeyboardRoot.offsetMin = Vector2.zero;
        displayKeyboardRoot.offsetMax = Vector2.zero;
        displayKeyboardRoot.localScale = Vector3.one;
        displayKeyboardRoot.localRotation = Quaternion.identity;

        Image background = root.AddComponent<Image>();
        background.color = keyboardBackgroundColor;
        background.raycastTarget = false;

        Canvas keyboardCanvas = root.AddComponent<Canvas>();
        keyboardCanvas.overrideSorting = true;
        keyboardCanvas.sortingOrder = keyboardSortingOrder;

        string[] regularRows =
        {
            "1234567890",
            "qwertyuiop",
            "asdfghjkl",
            "zxcvbnm.-/"
        };

        int rowCount = regularRows.Length + 1;
        for (int row = 0; row < regularRows.Length; row++)
        {
            string keys = regularRows[row];
            float yMax = 1f - row / (float)rowCount;
            float yMin = 1f - (row + 1) / (float)rowCount;
            for (int index = 0; index < keys.Length; index++)
            {
                float xMin = index / (float)keys.Length;
                float xMax = (index + 1) / (float)keys.Length;
                CreateKeyboardKey(keys[index].ToString(), new Vector2(xMin, yMin), new Vector2(xMax, yMax));
            }
        }

        float bottomYMax = 1f / rowCount;
        CreateKeyboardKey("BACK", new Vector2(0f, 0f), new Vector2(0.20f, bottomYMax));
        CreateKeyboardKey("SPACE", new Vector2(0.20f, 0f), new Vector2(0.78f, bottomYMax));
        CreateKeyboardKey("ENTER", new Vector2(0.78f, 0f), new Vector2(1f, bottomYMax));
    }

    private void CreateKeyboardKey(string label, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject keyObject = new GameObject($"Key_{label}", typeof(RectTransform));
        keyObject.transform.SetParent(displayKeyboardRoot, false);
        RectTransform keyRect = keyObject.GetComponent<RectTransform>();
        keyRect.anchorMin = anchorMin;
        keyRect.anchorMax = anchorMax;
        keyRect.offsetMin = new Vector2(3f, 3f);
        keyRect.offsetMax = new Vector2(-3f, -3f);
        keyRect.localScale = Vector3.one;

        Image keyImage = keyObject.AddComponent<Image>();
        keyImage.color = keyboardKeyColor;
        keyImage.raycastTarget = false;

        GameObject textObject = new GameObject("Label", typeof(RectTransform));
        textObject.transform.SetParent(keyObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        StretchRectTransform(textRect);

        Text text = textObject.AddComponent<Text>();
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = ResolveKeyboardFont();
        text.fontStyle = FontStyle.Bold;
        text.fontSize = label.Length > 1 ? 22 : 30;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 12;
        text.resizeTextMaxSize = label.Length > 1 ? 24 : 32;
        text.color = keyboardTextColor;
        text.raycastTarget = false;

        Outline outline = textObject.AddComponent<Outline>();
        outline.effectColor = keyboardTextOutlineColor;
        outline.effectDistance = new Vector2(1.2f, -1.2f);
    }

    private bool TryHandleDisplayKeyboardClick(Vector2 displayNormalized)
    {
        if (!IsDisplayKeyboardArea(displayNormalized))
        {
            return false;
        }

        float keyboardHeight = Mathf.Clamp(displayKeyboardHeightNormalized, 0.18f, 0.55f);
        float keyboardX = Mathf.Clamp01(displayNormalized.x);
        float keyboardY = Mathf.Clamp01(displayNormalized.y / keyboardHeight);

        string[] regularRows =
        {
            "1234567890",
            "qwertyuiop",
            "asdfghjkl",
            "zxcvbnm.-/"
        };

        int rowCount = regularRows.Length + 1;
        int rowFromTop = Mathf.Clamp(Mathf.FloorToInt((1f - keyboardY) * rowCount), 0, rowCount - 1);
        if (rowFromTop >= regularRows.Length)
        {
            if (keyboardX < 0.20f)
            {
                SendKeyboardBackspace();
            }
            else if (keyboardX < 0.78f)
            {
                SendKeyboardCharacter(' ');
            }
            else
            {
                SendKeyboardEnter();
            }

            return true;
        }

        string row = regularRows[rowFromTop];
        int keyIndex = Mathf.Clamp(Mathf.FloorToInt(keyboardX * row.Length), 0, row.Length - 1);
        SendKeyboardCharacter(row[keyIndex]);
        return true;
    }

    private bool IsDisplayKeyboardArea(Vector2 displayNormalized)
    {
        if (!showDisplayKeyboard || displayKeyboardRoot == null || !displayKeyboardRoot.gameObject.activeInHierarchy)
        {
            return false;
        }

        float keyboardHeight = Mathf.Clamp(displayKeyboardHeightNormalized, 0.18f, 0.55f);
        return displayNormalized.y <= keyboardHeight;
    }

    private void SendKeyboardCharacter(char character)
    {
        if (browser == null)
        {
            return;
        }

        TryInvoke(browser, "KeyEvent", character);
        RequestKeyboardFocusBridgeRefresh();
    }

    private void SendKeyboardBackspace()
    {
        if (browser == null)
        {
            return;
        }

        TryInvoke(browser, "KeyEvent", 67);
        RequestKeyboardFocusBridgeRefresh();
    }

    private void SendKeyboardEnter()
    {
        if (browser == null)
        {
            return;
        }

        if (!TryInvoke(browser, "KeyEvent", 66))
        {
            TryInvoke(browser, "KeyEvent", '\n');
        }

        RequestKeyboardFocusBridgeRefresh();
    }

    public void OnKeyboardFocusMessage(string message)
    {
        bool focused = string.Equals(message, "focusin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(message, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(message, "true", StringComparison.OrdinalIgnoreCase);

        if (keyboardInputFocused == focused)
        {
            SetDisplayKeyboardVisible(ShouldShowKeyboard());
            return;
        }

        keyboardInputFocused = focused;
        SetDisplayKeyboardVisible(ShouldShowKeyboard());

        if (logKeyboardFocusChanges)
        {
            Debug.Log($"[TLabWebViewDisplayBridge] keyboardInputFocused={keyboardInputFocused} display={name}");
        }
    }

    private void RefreshKeyboardFocusBridgeIfNeeded()
    {
        if (!showDisplayKeyboard || !showKeyboardOnlyForTextInput)
        {
            return;
        }

        if (Time.unscaledTime < nextKeyboardFocusBridgeRefreshTime)
        {
            return;
        }

        InstallKeyboardFocusBridge();
    }

    private void RequestKeyboardFocusBridgeRefresh()
    {
        nextKeyboardFocusBridgeRefreshTime = 0f;
    }

    private void InstallKeyboardFocusBridge()
    {
        if (!CanRunKeyboardFocusJavaScript())
        {
            return;
        }

        nextKeyboardFocusBridgeRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, keyboardFocusBridgeRefreshSeconds);
        if (string.IsNullOrEmpty(keyboardFocusBridgeJavaScript))
        {
            keyboardFocusBridgeJavaScript = BuildKeyboardFocusBridgeJavaScript(gameObject.name, nameof(OnKeyboardFocusMessage));
        }

        TryInvoke(browser, "EvaluateJS", keyboardFocusBridgeJavaScript);
    }

    private bool CanRunKeyboardFocusJavaScript()
    {
        return showDisplayKeyboard
            && showKeyboardOnlyForTextInput
            && initialized
            && browser != null
            && gameObject.activeInHierarchy
            && enabled;
    }

    private void ShowEditorPlaceholder()
    {
        if (editorPlaceholder == null)
        {
            editorPlaceholder = new GameObject("TLab_EditorPlaceholder", typeof(RectTransform));
            editorPlaceholder.transform.SetParent(ResolveCanvasParent(), false);

            RectTransform rect = editorPlaceholder.GetComponent<RectTransform>();
            StretchRectTransform(rect);

            Image image = editorPlaceholder.AddComponent<Image>();
            image.color = new Color(0.03f, 0.07f, 0.10f, 0.94f);
            image.raycastTarget = false;

            GameObject textObject = new GameObject("Message", typeof(RectTransform));
            textObject.transform.SetParent(editorPlaceholder.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            StretchRectTransform(textRect);
            textRect.offsetMin = new Vector2(40f, 40f);
            textRect.offsetMax = new Vector2(-40f, -40f);

            Text text = textObject.AddComponent<Text>();
            text.text = "TLabWebView\nAndroid device build required\nQuestで実ブラウザ表示を確認";
            text.alignment = TextAnchor.MiddleCenter;
            text.font = ResolveKeyboardFont();
            text.fontSize = 34;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        editorPlaceholder.SetActive(true);
        editorPlaceholder.transform.SetAsFirstSibling();
    }

    private void HideEditorPlaceholder()
    {
        if (editorPlaceholder != null)
        {
            editorPlaceholder.SetActive(false);
        }
    }

    private void ResolveReferences()
    {
        if (displaySurface == null)
        {
            displaySurface = GetComponent<DisplaySurface>();
        }
    }

    private Transform ResolveCanvasParent()
    {
        ResolveReferences();
        if (displaySurface != null && displaySurface.VisiblePanel != null)
        {
            return displaySurface.VisiblePanel;
        }

        if (displaySurface != null && displaySurface.WorldSpaceCanvas != null)
        {
            return displaySurface.WorldSpaceCanvas.transform;
        }

        return transform;
    }

    private Camera ResolveCanvasCamera()
    {
        if (displaySurface != null && displaySurface.WorldSpaceCanvas != null && displaySurface.WorldSpaceCanvas.worldCamera != null)
        {
            return displaySurface.WorldSpaceCanvas.worldCamera;
        }

        return Camera.main;
    }

    private Component FindExistingContainer(Type containerType)
    {
        Component[] childComponents = GetComponentsInChildren<Component>(true);
        for (int index = 0; index < childComponents.Length; index++)
        {
            Component component = childComponents[index];
            if (component != null && containerType.IsAssignableFrom(component.GetType()))
            {
                return component;
            }
        }

        return null;
    }

    private static object FindBrowserComponent(GameObject root, Type browserType)
    {
        Component[] childComponents = root.GetComponentsInChildren<Component>(true);
        for (int index = 0; index < childComponents.Length; index++)
        {
            Component component = childComponents[index];
            if (component != null && browserType.IsAssignableFrom(component.GetType()))
            {
                return component;
            }
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static void StretchRectTransform(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
    }

    private static Vector2 ToWebNormalizedPoint(Vector2 displayNormalized)
    {
        return new Vector2(
            Mathf.Clamp01(displayNormalized.x),
            1f - Mathf.Clamp01(displayNormalized.y));
    }

    private static Font ResolveKeyboardFont()
    {
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static string BuildKeyboardFocusBridgeJavaScript(string gameObjectName, string methodName)
    {
        string targetObject = EscapeJavaScriptString(gameObjectName);
        string targetMethod = EscapeJavaScriptString(methodName);
        return "(function(){"
            + $"var go='{targetObject}';"
            + $"var method='{targetMethod}';"
            + "function editable(el){"
            + "if(!el){return false;}"
            + "var tag=(el.tagName||'').toUpperCase();"
            + "if(tag==='TEXTAREA'){return true;}"
            + "if(tag==='INPUT'){"
            + "var type=(el.type||'text').toLowerCase();"
            + "return ['button','submit','checkbox','radio','range','color','file','image','reset','hidden'].indexOf(type)<0;"
            + "}"
            + "return !!el.isContentEditable;"
            + "}"
            + "function activeElement(){"
            + "var el=document.activeElement;"
            + "while(el&&el.shadowRoot&&el.shadowRoot.activeElement){el=el.shadowRoot.activeElement;}"
            + "return el;"
            + "}"
            + "function send(focused){"
            + "if(window.tlab&&window.tlab.unitySendMessage){window.tlab.unitySendMessage(go,method,focused?'focusin':'focusout');}"
            + "}"
            + "function report(){"
            + "var focused=editable(activeElement());"
            + "if(window.__supineDisplayKeyboardLastFocused!==focused){window.__supineDisplayKeyboardLastFocused=focused;send(focused);}"
            + "}"
            + "if(!window.__supineDisplayKeyboardBridgeInstalled){"
            + "document.addEventListener('focusin',report,true);"
            + "document.addEventListener('focusout',function(){setTimeout(report,0);},true);"
            + "document.addEventListener('pointerdown',function(){setTimeout(report,0);},true);"
            + "window.__supineDisplayKeyboardBridgeInstalled=true;"
            + "}"
            + "report();"
            + "})();";
    }

    private static string EscapeJavaScriptString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");
    }

    private static Type FindType(string fullName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int index = 0; index < assemblies.Length; index++)
        {
            Type type = assemblies[index].GetType(fullName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    private static bool SetMemberIfAvailable(object target, string memberName, object value)
    {
        MemberInfo member = FindMember(target, memberName, true);
        try
        {
            if (member is PropertyInfo property && property.CanWrite)
            {
                property.SetValue(target, value);
                return true;
            }

            if (member is FieldInfo field)
            {
                field.SetValue(target, value);
                return true;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[TLabWebViewDisplayBridge] Could not set {memberName}: {exception.Message}");
        }

        return false;
    }

    private static object GetMemberValue(object target, string memberName)
    {
        MemberInfo member = FindMember(target, memberName, false);
        if (member is PropertyInfo property && property.CanRead)
        {
            return property.GetValue(target);
        }

        if (member is FieldInfo field)
        {
            return field.GetValue(target);
        }

        return null;
    }

    private static MemberInfo FindMember(object target, string memberName, bool includePrivate)
    {
        if (target == null)
        {
            return null;
        }

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
        if (includePrivate)
        {
            flags |= BindingFlags.NonPublic;
        }

        Type type = target.GetType();
        while (type != null)
        {
            PropertyInfo property = type.GetProperty(memberName, flags);
            if (property != null)
            {
                return property;
            }

            FieldInfo field = type.GetField(memberName, flags);
            if (field != null)
            {
                return field;
            }

            type = type.BaseType;
        }

        return null;
    }

    private static bool TryInvoke(object target, string methodName, params object[] args)
    {
        return InvokeAndReturn(target, methodName, args) != null;
    }

    private static object InvokeAndReturn(object target, string methodName, params object[] args)
    {
        if (target == null)
        {
            return null;
        }

        MethodInfo method = FindCompatibleMethod(target.GetType(), methodName, args);
        if (method == null)
        {
            return null;
        }

        try
        {
            object result = method.Invoke(target, BuildInvocationArguments(method, args));
            return method.ReturnType == typeof(void) ? true : result;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[TLabWebViewDisplayBridge] {methodName} failed: {exception.GetBaseException().Message}");
            return null;
        }
    }

    private static MethodInfo FindCompatibleMethod(Type type, string methodName, object[] args)
    {
        while (type != null)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int index = 0; index < methods.Length; index++)
            {
                MethodInfo method = methods[index];
                if (method.Name == methodName && ParametersMatch(method.GetParameters(), args))
                {
                    return method;
                }
            }

            type = type.BaseType;
        }

        return null;
    }

    private static bool ParametersMatch(ParameterInfo[] parameters, object[] args)
    {
        if (parameters.Length < args.Length)
        {
            return false;
        }

        for (int index = args.Length; index < parameters.Length; index++)
        {
            if (!parameters[index].IsOptional)
            {
                return false;
            }
        }

        for (int index = 0; index < args.Length; index++)
        {
            if (args[index] == null)
            {
                continue;
            }

            Type parameterType = parameters[index].ParameterType;
            Type argumentType = args[index].GetType();
            if (!parameterType.IsAssignableFrom(argumentType))
            {
                return false;
            }
        }

        return true;
    }

    private static object[] BuildInvocationArguments(MethodInfo method, object[] args)
    {
        ParameterInfo[] parameters = method.GetParameters();
        object[] invocationArgs = new object[parameters.Length];
        for (int index = 0; index < parameters.Length; index++)
        {
            invocationArgs[index] = index < args.Length ? args[index] : parameters[index].DefaultValue;
        }

        return invocationArgs;
    }

    private static string Format(Vector2 value)
    {
        return $"({value.x:0.000}, {value.y:0.000})";
    }
}
