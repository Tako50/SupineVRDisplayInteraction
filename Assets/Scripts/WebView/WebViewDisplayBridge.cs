using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
/// <summary>
/// Optional bridge from this project's DisplaySurface input coordinates to Vuplex 3D WebView.
/// The script uses reflection so the project still compiles before the paid/trial WebView package is imported.
/// </summary>
public class WebViewDisplayBridge : MonoBehaviour
{
    private const string VuplexWebViewPrefabTypeName = "Vuplex.WebView.WebViewPrefab";

    [Header("References")]
    [SerializeField] private DisplaySurface displaySurface;

    [Header("Startup")]
    [SerializeField] private bool enableOnStart = false;
    [SerializeField] private string initialUrl = "https://www.youtube.com";
    [SerializeField] private bool autoCreateVuplexPrefab = true;
    [Tooltip("If true, existing DisplaySurface click/scroll input is consumed and sent to the WebView.")]
    [SerializeField] private bool consumeExperimentInput = true;

    [Header("Vuplex Prefab Setup")]
    [SerializeField] private Vector3 localOffsetMeters = new Vector3(0f, 0f, -0.002f);
    [SerializeField] private Vector3 localEulerAngles = Vector3.zero;
    [SerializeField] private bool disableVuplexCollider = true;
    [SerializeField] private bool disableVuplexBuiltInPointerInput = true;
    [SerializeField] private bool nativeOnScreenKeyboardEnabled = true;
    [SerializeField] private bool remoteDebuggingEnabled = true;
    [SerializeField] private float resolutionPixelsPerUnit = 1300f;

    [Header("Input Mapping")]
    [SerializeField] private float scrollPixelsPerSecond = 900f;
    [SerializeField] private bool invertScrollDirection = true;
    [SerializeField] private bool logInputEvents = false;

    private Component webViewPrefabComponent;
    private object webView;
    private Coroutine initializationCoroutine;

    public bool IsEnabled => enableOnStart;
    public bool IsReady => webView != null;
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

    public void EnableWebView()
    {
        enableOnStart = true;
        ResolveReferences();

        if (initializationCoroutine != null)
        {
            return;
        }

        initializationCoroutine = StartCoroutine(InitializeVuplexWebView());
    }

    public void DisableWebView()
    {
        enableOnStart = false;
        webView = null;

        if (initializationCoroutine != null)
        {
            StopCoroutine(initializationCoroutine);
            initializationCoroutine = null;
        }

        if (webViewPrefabComponent != null)
        {
            SetPropertyIfAvailable(webViewPrefabComponent, "Visible", false);
        }

        Status = "Disabled";
    }

    public void LoadUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        initialUrl = url;
        if (webView != null && TryInvoke(webView, "LoadUrl", url))
        {
            Status = $"Loaded URL: {url}";
        }
    }

    public void Reload()
    {
        TryInvoke(webView, "Reload");
    }

    public void GoBack()
    {
        TryInvoke(webView, "GoBack");
    }

    public void GoForward()
    {
        TryInvoke(webView, "GoForward");
    }

    public bool TryClick(Vector2 displayNormalized)
    {
        if (!CanReceiveInput())
        {
            return false;
        }

        Vector2 webPoint = ToVuplexNormalizedPoint(displayNormalized);
        bool sent = TryInvoke(webView, "Click", webPoint, false);
        if (sent && logInputEvents)
        {
            Debug.Log($"[WebViewDisplayBridge] click display={name}, normalized={Format(displayNormalized)}, web={Format(webPoint)}");
        }

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

        if (Mathf.Approximately(scrollPixels, 0f))
        {
            return false;
        }

        Vector2 scrollDelta = new Vector2(0f, scrollPixels);
        Vector2 webPoint = ToVuplexNormalizedPoint(displayNormalized);
        bool sent = TryInvoke(webView, "Scroll", scrollDelta, webPoint);
        if (!sent)
        {
            sent = TryInvoke(webView, "Scroll", scrollDelta);
        }

        if (!sent)
        {
            return false;
        }

        appliedScrollPixels = scrollPixels;
        if (logInputEvents)
        {
            Debug.Log($"[WebViewDisplayBridge] scroll display={name}, pixels={scrollPixels:0.0}, web={Format(webPoint)}");
        }

        return consumeExperimentInput;
    }

    private IEnumerator InitializeVuplexWebView()
    {
        Type prefabType = FindType(VuplexWebViewPrefabTypeName);
        if (prefabType == null)
        {
            Status = "Vuplex 3D WebView not found. Import the Android trial/package first.";
            Debug.LogWarning($"[WebViewDisplayBridge] {Status}");
            initializationCoroutine = null;
            yield break;
        }

        webViewPrefabComponent = FindExistingVuplexPrefab(prefabType);
        if (webViewPrefabComponent == null && autoCreateVuplexPrefab)
        {
            webViewPrefabComponent = CreateVuplexPrefab(prefabType);
        }

        if (webViewPrefabComponent == null)
        {
            Status = "No WebViewPrefab found or created.";
            Debug.LogWarning($"[WebViewDisplayBridge] {Status}");
            initializationCoroutine = null;
            yield break;
        }

        ConfigureVuplexPrefab();
        Status = "Initializing WebView...";

        object taskObject = InvokeAndReturn(webViewPrefabComponent, "WaitUntilInitialized");
        if (taskObject is Task initializationTask)
        {
            while (!initializationTask.IsCompleted)
            {
                yield return null;
            }

            if (initializationTask.IsFaulted)
            {
                Status = initializationTask.Exception != null
                    ? initializationTask.Exception.GetBaseException().Message
                    : "WebView initialization failed.";
                Debug.LogWarning($"[WebViewDisplayBridge] {Status}");
                initializationCoroutine = null;
                yield break;
            }
        }
        else
        {
            yield return null;
        }

        webView = GetPropertyValue(webViewPrefabComponent, "WebView");
        if (webView == null)
        {
            Status = "WebView property is null after initialization.";
            Debug.LogWarning($"[WebViewDisplayBridge] {Status}");
            initializationCoroutine = null;
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(initialUrl))
        {
            TryInvoke(webView, "LoadUrl", initialUrl);
        }

        Status = $"Ready: {initialUrl}";
        Debug.Log($"[WebViewDisplayBridge] {name} ready url={initialUrl}");
        initializationCoroutine = null;
    }

    private Component CreateVuplexPrefab(Type prefabType)
    {
        Vector2 size = displaySurface != null
            ? displaySurface.PhysicalSizeMeters
            : DisplayGeometry.DefaultPhysicalSizeMeters;

        object instance = InvokeStaticAndReturn(
            prefabType,
            "Instantiate",
            Mathf.Max(0.01f, size.x),
            Mathf.Max(0.01f, size.y));

        if (!(instance is Component component))
        {
            return null;
        }

        component.transform.SetParent(transform, false);
        component.transform.localPosition = localOffsetMeters;
        component.transform.localRotation = Quaternion.Euler(localEulerAngles);
        component.gameObject.name = "Vuplex_WebViewPrefab";
        return component;
    }

    private void ConfigureVuplexPrefab()
    {
        if (webViewPrefabComponent == null)
        {
            return;
        }

        webViewPrefabComponent.transform.localPosition = localOffsetMeters;
        webViewPrefabComponent.transform.localRotation = Quaternion.Euler(localEulerAngles);
        SetPropertyIfAvailable(webViewPrefabComponent, "Visible", true);
        SetPropertyIfAvailable(webViewPrefabComponent, "InitialUrl", initialUrl);
        SetPropertyIfAvailable(webViewPrefabComponent, "NativeOnScreenKeyboardEnabled", nativeOnScreenKeyboardEnabled);
        SetPropertyIfAvailable(webViewPrefabComponent, "RemoteDebuggingEnabled", remoteDebuggingEnabled);
        SetPropertyIfAvailable(webViewPrefabComponent, "Resolution", resolutionPixelsPerUnit);

        if (disableVuplexBuiltInPointerInput)
        {
            SetPropertyIfAvailable(webViewPrefabComponent, "ClickingEnabled", false);
            SetPropertyIfAvailable(webViewPrefabComponent, "ScrollingEnabled", false);
            SetPropertyIfAvailable(webViewPrefabComponent, "HoveringEnabled", false);
        }

        if (disableVuplexCollider)
        {
            object colliderObject = GetPropertyValue(webViewPrefabComponent, "Collider");
            if (colliderObject is Collider collider)
            {
                collider.enabled = false;
            }
        }
    }

    private bool CanReceiveInput()
    {
        if (!enableOnStart || !consumeExperimentInput || webView == null)
        {
            return false;
        }

        return gameObject.activeInHierarchy && enabled;
    }

    private void ResolveReferences()
    {
        if (displaySurface == null)
        {
            displaySurface = GetComponent<DisplaySurface>();
        }
    }

    private Component FindExistingVuplexPrefab(Type prefabType)
    {
        Component[] childComponents = GetComponentsInChildren<Component>(true);
        for (int i = 0; i < childComponents.Length; i++)
        {
            Component component = childComponents[i];
            if (component != null && prefabType.IsAssignableFrom(component.GetType()))
            {
                return component;
            }
        }

        return null;
    }

    private static Vector2 ToVuplexNormalizedPoint(Vector2 displayNormalized)
    {
        // DisplaySurface uses y=0 at bottom; web browsers use y=0 at top.
        return new Vector2(
            Mathf.Clamp01(displayNormalized.x),
            1f - Mathf.Clamp01(displayNormalized.y));
    }

    private static Type FindType(string fullName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType(fullName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    private static bool SetPropertyIfAvailable(object target, string propertyName, object value)
    {
        if (target == null)
        {
            return false;
        }

        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property == null || !property.CanWrite)
        {
            return false;
        }

        try
        {
            property.SetValue(target, value);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[WebViewDisplayBridge] Could not set {propertyName}: {exception.Message}");
            return false;
        }
    }

    private static object GetPropertyValue(object target, string propertyName)
    {
        if (target == null)
        {
            return null;
        }

        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        return property != null && property.CanRead ? property.GetValue(target) : null;
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
            Debug.LogWarning($"[WebViewDisplayBridge] {methodName} failed: {exception.GetBaseException().Message}");
            return null;
        }
    }

    private static object InvokeStaticAndReturn(Type type, string methodName, params object[] args)
    {
        MethodInfo method = FindCompatibleMethod(type, methodName, args, BindingFlags.Static | BindingFlags.Public);
        if (method == null)
        {
            return null;
        }

        try
        {
            return method.Invoke(null, BuildInvocationArguments(method, args));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[WebViewDisplayBridge] static {methodName} failed: {exception.GetBaseException().Message}");
            return null;
        }
    }

    private static MethodInfo FindCompatibleMethod(
        Type type,
        string methodName,
        object[] args,
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public)
    {
        MethodInfo[] methods = type.GetMethods(flags);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];
            if (method.Name != methodName || !ParametersMatch(method.GetParameters(), args))
            {
                continue;
            }

            return method;
        }

        return null;
    }

    private static bool ParametersMatch(ParameterInfo[] parameters, object[] args)
    {
        if (parameters.Length < args.Length)
        {
            return false;
        }

        for (int i = args.Length; i < parameters.Length; i++)
        {
            if (!parameters[i].IsOptional)
            {
                return false;
            }
        }

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == null)
            {
                continue;
            }

            Type parameterType = parameters[i].ParameterType;
            Type argumentType = args[i].GetType();
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
        for (int i = 0; i < parameters.Length; i++)
        {
            invocationArgs[i] = i < args.Length ? args[i] : parameters[i].DefaultValue;
        }

        return invocationArgs;
    }

    private static string Format(Vector2 value)
    {
        return $"({value.x:0.000}, {value.y:0.000})";
    }
}
