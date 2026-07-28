using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
/// <summary>
/// セッション開始時のHMD正面を基準に左90度へ終了パネルを固定し、左コントローラRayで操作する。
/// 左トリガーを一定時間保持した場合だけ現在のタスクを終了する。
/// </summary>
public class ExitPanelController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera hmdCamera;
    [SerializeField] private Transform leftControllerTransform;
    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private VRTaskMenuManager taskMenuManager;
    [SerializeField] private Logger logger;

    [Header("Placement")]
    [SerializeField] private float panelDistanceMeters = 1.2f;
    [SerializeField] private float panelVerticalOffsetMeters = 0f;
    [SerializeField] private Vector2 panelSizePixels = new Vector2(640f, 360f);
    [SerializeField] private float panelScale = 0.001f;

    [Header("Selection")]
    [SerializeField] private float holdToExitSeconds = 0.6f;
    [SerializeField] private float rayLengthMeters = 3f;
    [SerializeField] private float rayWidthMeters = 0.006f;
    [SerializeField] private Color rayColor = new Color(0.4f, 0.8f, 1f, 0.55f);
    [SerializeField] private Color rayHoverColor = new Color(1f, 0.48f, 0.24f, 1f);

    [Header("Visuals")]
    [SerializeField] private Color panelColor = new Color(0.08f, 0.025f, 0.035f, 0.97f);
    [SerializeField] private Color panelHoverColor = new Color(0.24f, 0.055f, 0.065f, 0.99f);
    [SerializeField] private Color progressColor = new Color(0.96f, 0.28f, 0.18f, 1f);

    private RectTransform panelRoot;
    private Image panelImage;
    private RectTransform progressFill;
    private Text titleText;
    private Text statusText;
    private BoxCollider panelCollider;
    private LineRenderer leftRayLine;
    private Material rayMaterial;
    private float holdElapsed;
    private bool taskWasRunning;
    private bool wasHovered;
    private bool exitInvoked;
    private bool requireTriggerRelease;

    public bool IsVisible => panelRoot != null && panelRoot.gameObject.activeSelf;
    public bool IsHovered { get; private set; }
    public float HoldProgress => holdToExitSeconds > 0f
        ? Mathf.Clamp01(holdElapsed / holdToExitSeconds)
        : 1f;

    private void Awake()
    {
        ResolveReferences();
        EnsureVisuals();
        SetVisible(false);
    }

    private void Update()
    {
        ResolveReferences();
        bool taskRunning = taskMenuManager != null && taskMenuManager.IsTaskRunning();
        if (taskRunning && !taskWasRunning)
        {
            PlacePanelFromCurrentHmdPose();
            ResetHoldState();
            SetVisible(true);
        }
        else if (!taskRunning && taskWasRunning)
        {
            ResetHoldState();
            SetVisible(false);
        }

        taskWasRunning = taskRunning;
        if (!taskRunning || panelRoot == null || panelCollider == null)
        {
            return;
        }

        Ray selectionRay = GetSelectionRay();
        IsHovered = TryGetPanelHit(selectionRay, out Vector3 rayEnd);
        UpdateRay(selectionRay, rayEnd, IsHovered);

        if (IsHovered && !wasHovered)
        {
            inputManager?.SendLeftHapticImpulse(0.18f, 0.04f);
        }

        wasHovered = IsHovered;
        bool triggerHeld = inputManager != null && inputManager.LeftTriggerHeld;
        if (requireTriggerRelease && !triggerHeld)
        {
            requireTriggerRelease = false;
        }

        bool holding = triggerHeld && !requireTriggerRelease;
        if (IsHovered && holding && !exitInvoked)
        {
            holdElapsed += Time.unscaledDeltaTime;
        }
        else if (!exitInvoked)
        {
            holdElapsed = 0f;
        }

        UpdateVisualState();
        if (!exitInvoked && HoldProgress >= 1f)
        {
            ExecuteExit();
        }
    }

    private void OnDisable()
    {
        ResetHoldState();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (rayMaterial != null)
        {
            Destroy(rayMaterial);
        }
    }

    private void ExecuteExit()
    {
        exitInvoked = true;
        inputManager?.SendLeftHapticImpulse(0.75f, 0.12f);
        if (taskMenuManager != null && taskMenuManager.TryCompleteActiveTraining())
        {
            logger?.LogEvent(
                "ExitPanel_EndTraining",
                inputManager != null ? inputManager.CurrentCondition : InteractionCondition.RaycastBaseline,
                "LeftExitPanel",
                Vector2.zero);
            Debug.Log("[ExitPanel] practice readiness confirmed. Returning to the task menu.");
            holdElapsed = 0f;
            exitInvoked = false;
            requireTriggerRelease = true;
            UpdateVisualState();
            return;
        }

        logger?.LogEvent(
            "ExitPanel_Exit",
            inputManager != null ? inputManager.CurrentCondition : InteractionCondition.RaycastBaseline,
            "LeftExitPanel",
            Vector2.zero);
        Debug.Log("[ExitPanel] left trigger hold completed. Returning to condition selection.");
        taskMenuManager?.ReturnActiveTaskToSelection();
    }

    private Ray GetSelectionRay()
    {
#if UNITY_EDITOR
        if (inputManager != null
            && inputManager.DebugInputEnabled
            && inputManager.LeftTriggerHeld
            && hmdCamera != null
            && panelRoot != null)
        {
            Vector3 direction = panelRoot.position - hmdCamera.transform.position;
            return new Ray(hmdCamera.transform.position, direction.normalized);
        }
#endif

        if (leftControllerTransform != null)
        {
            return new Ray(leftControllerTransform.position, leftControllerTransform.forward);
        }

        return hmdCamera != null
            ? new Ray(hmdCamera.transform.position, hmdCamera.transform.forward)
            : default;
    }

    private bool TryGetPanelHit(Ray ray, out Vector3 rayEnd)
    {
        rayEnd = ray.origin + ray.direction * rayLengthMeters;
        if (ray.direction == Vector3.zero
            || !Physics.Raycast(
                ray,
                out RaycastHit hit,
                rayLengthMeters,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide))
        {
            return false;
        }

        rayEnd = hit.point;
        return hit.collider == panelCollider;
    }

    private void PlacePanelFromCurrentHmdPose()
    {
        if (hmdCamera == null || panelRoot == null)
        {
            return;
        }

        Transform hmd = hmdCamera.transform;
        Vector3 leftDirection = -hmd.right.normalized;
        Vector3 panelPosition = hmd.position
            + leftDirection * Mathf.Max(0.25f, panelDistanceMeters)
            + hmd.up * panelVerticalOffsetMeters;
        panelRoot.position = panelPosition;
        panelRoot.rotation = Quaternion.LookRotation(leftDirection, hmd.up);
    }

    private void EnsureVisuals()
    {
        if (panelRoot != null)
        {
            return;
        }

        GameObject root = new GameObject("Left_ExitPanel", typeof(RectTransform), typeof(Canvas));
        panelRoot = root.GetComponent<RectTransform>();
        panelRoot.sizeDelta = panelSizePixels;
        panelRoot.localScale = Vector3.one * panelScale;

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = hmdCamera;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 80;

        RectTransform background = CreateImage(
            panelRoot,
            "Background",
            Vector2.zero,
            Vector2.one,
            panelColor);
        panelImage = background.GetComponent<Image>();

        titleText = CreateText(
            panelRoot,
            "Title",
            "EXIT CONDITION",
            new Vector2(0.08f, 0.52f),
            new Vector2(0.92f, 0.86f),
            54);
        titleText.fontStyle = FontStyle.Bold;

        statusText = CreateText(
            panelRoot,
            "Status",
            "LEFT TRIGGER HOLD  0.6 s",
            new Vector2(0.08f, 0.28f),
            new Vector2(0.92f, 0.50f),
            26);

        CreateImage(
            panelRoot,
            "ProgressBackground",
            new Vector2(0.08f, 0.12f),
            new Vector2(0.92f, 0.22f),
            new Color(1f, 1f, 1f, 0.16f));
        progressFill = CreateImage(
            panelRoot,
            "ProgressFill",
            new Vector2(0.08f, 0.12f),
            new Vector2(0.08f, 0.22f),
            progressColor);

        panelCollider = root.AddComponent<BoxCollider>();
        panelCollider.isTrigger = true;
        panelCollider.size = new Vector3(panelSizePixels.x, panelSizePixels.y, 20f);

        GameObject rayObject = new GameObject("Left_ExitRay");
        leftRayLine = rayObject.AddComponent<LineRenderer>();
        leftRayLine.useWorldSpace = true;
        leftRayLine.positionCount = 2;
        leftRayLine.startWidth = rayWidthMeters;
        leftRayLine.endWidth = rayWidthMeters * 0.45f;
        leftRayLine.numCapVertices = 4;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            rayMaterial = new Material(shader);
            leftRayLine.material = rayMaterial;
        }

        UpdateVisualState();
    }

    private static RectTransform CreateImage(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color color)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        child.GetComponent<Image>().color = color;
        return rect;
    }

    private static Text CreateText(
        Transform parent,
        string name,
        string value,
        Vector2 anchorMin,
        Vector2 anchorMax,
        int fontSize)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Text text = child.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private void UpdateRay(Ray ray, Vector3 rayEnd, bool hovered)
    {
        if (leftRayLine == null)
        {
            return;
        }

        leftRayLine.enabled = IsVisible && hovered && ray.direction != Vector3.zero;
        if (!leftRayLine.enabled)
        {
            return;
        }

        leftRayLine.SetPosition(0, ray.origin);
        leftRayLine.SetPosition(1, rayEnd);
        Color color = hovered ? rayHoverColor : rayColor;
        leftRayLine.startColor = color;
        leftRayLine.endColor = color;
    }

    private void UpdateVisualState()
    {
        bool canEndTraining = taskMenuManager != null && taskMenuManager.IsActiveTrainingReadyToEnd();
        if (titleText != null)
        {
            titleText.text = canEndTraining
                ? "END PRACTICE"
                : "EXIT CONDITION";
        }

        if (panelImage != null)
        {
            panelImage.color = IsHovered ? panelHoverColor : panelColor;
        }

        if (progressFill != null)
        {
            Vector2 anchorMax = progressFill.anchorMax;
            anchorMax.x = Mathf.Lerp(0.08f, 0.92f, HoldProgress);
            progressFill.anchorMax = anchorMax;
        }

        if (statusText != null)
        {
            statusText.text = IsHovered
                ? canEndTraining
                    ? $"HOLD TO CONFIRM READY  {HoldProgress * 100f:0}%"
                    : $"HOLD LEFT TRIGGER  {HoldProgress * 100f:0}%"
                : canEndTraining
                    ? "PARTICIPANT READY? AIM WITH LEFT CONTROLLER"
                    : "AIM WITH LEFT CONTROLLER";
        }
    }

    private void ResetHoldState()
    {
        holdElapsed = 0f;
        IsHovered = false;
        wasHovered = false;
        exitInvoked = false;
        requireTriggerRelease = false;
        UpdateVisualState();
    }

    private void SetVisible(bool visible)
    {
        if (panelRoot != null)
        {
            panelRoot.gameObject.SetActive(visible);
        }

        if (leftRayLine != null)
        {
            // The left-controller ray is revealed by UpdateRay only while it hits the exit panel.
            leftRayLine.enabled = false;
        }
    }

    private void ResolveReferences()
    {
        if (hmdCamera == null)
        {
            hmdCamera = Camera.main;
        }

        if (leftControllerTransform == null)
        {
            GameObject leftController = GameObject.Find("Left Controller");
            leftControllerTransform = leftController != null ? leftController.transform : null;
        }

        if (inputManager == null)
        {
            inputManager = PrototypeInputManager.Instance != null
                ? PrototypeInputManager.Instance
                : FindObjectOfType<PrototypeInputManager>();
        }

        if (taskMenuManager == null)
        {
            taskMenuManager = FindObjectOfType<VRTaskMenuManager>();
        }

        if (logger == null)
        {
            logger = FindObjectOfType<Logger>();
        }
    }
}
