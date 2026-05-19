using UnityEngine;
using UnityEngine.UI;

public enum DisplayContentMode
{
    Debug,
    ConditionSelection,
    PointingTask,
    ScrollTask
}

[DisallowMultipleComponent]
public class DisplaySurface : MonoBehaviour
{
    [Header("Display Parts")]
    [SerializeField] private Canvas worldSpaceCanvas;
    [SerializeField] private RectTransform visiblePanel;
    [SerializeField] private BoxCollider transparentHitPlane;
    [SerializeField] private RectTransform cursor;
    [SerializeField] private RectTransform scrollContent;
    [SerializeField] private RectTransform debugClickTarget;

    [Header("Sizing")]
    [SerializeField] private Vector2 physicalSizeMeters = new Vector2(0.8f, 0.45f);
    [SerializeField] private Vector2 canvasPixelSize = new Vector2(800f, 450f);
    [SerializeField] private float canvasScale = 0.001f;
    [SerializeField] private float hitPlaneDepthMeters = 0.02f;
    [SerializeField] private Vector2 cursorPixelSize = new Vector2(10f, 10f);

    [Header("Debug Content")]
    [SerializeField] private DisplayContentMode contentMode = DisplayContentMode.Debug;
    [SerializeField] private string debugTargetId = "DebugTarget";
    [SerializeField] private float scrollPixelsPerUnit = 160f;
    [SerializeField] private float maxScrollPixels = 420f;

    private float scrollOffsetPixels;
    private Image visiblePanelImage;
    private Color basePanelColor = Color.white;
    private bool hasCachedBasePanelColor;

    public Canvas WorldSpaceCanvas => worldSpaceCanvas;
    public RectTransform VisiblePanel => visiblePanel;
    public BoxCollider TransparentHitPlane => transparentHitPlane;
    public RectTransform Cursor => cursor;
    public RectTransform ScrollContent => scrollContent;
    public RectTransform DebugClickTarget => debugClickTarget;
    public Vector2 PhysicalSizeMeters => physicalSizeMeters;
    public Vector2 CanvasPixelSize => canvasPixelSize;
    public DisplayContentMode ContentMode => contentMode;

    private void Awake()
    {
        CachePanelImage();
    }

    public void AssignParts(Canvas canvas, RectTransform panel, BoxCollider hitPlane, RectTransform cursorRect)
    {
        worldSpaceCanvas = canvas;
        visiblePanel = panel;
        transparentHitPlane = hitPlane;
        cursor = cursorRect;
        ApplyConfiguration();
    }

    public void AssignDebugContent(RectTransform contentRoot, RectTransform clickTarget)
    {
        scrollContent = contentRoot;
        debugClickTarget = clickTarget;
        ApplyScrollOffset();
        ApplyContentMode();
    }

    public void SetContentMode(DisplayContentMode mode)
    {
        contentMode = mode;
        ApplyContentMode();
        BringCursorToFront();
    }

    public void BringCursorToFront()
    {
        if (cursor != null)
        {
            cursor.SetAsLastSibling();
        }
    }

    public void SetFocusVisual(bool focused)
    {
        CachePanelImage();
        if (visiblePanelImage == null)
        {
            return;
        }

        visiblePanelImage.color = WithAlpha(basePanelColor, basePanelColor.a);
    }

    public void SetCandidateVisual(bool candidate, bool overlapPreview)
    {
        CachePanelImage();
        if (visiblePanelImage == null)
        {
            return;
        }

        visiblePanelImage.color = WithAlpha(basePanelColor, basePanelColor.a);
    }

    public void SetSize(Vector2 sizeMeters, Vector2 pixelSize)
    {
        physicalSizeMeters = sizeMeters;
        canvasPixelSize = pixelSize;
        ApplyConfiguration();
    }

    public void ApplyConfiguration()
    {
        if (worldSpaceCanvas != null)
        {
            worldSpaceCanvas.renderMode = RenderMode.WorldSpace;
            worldSpaceCanvas.transform.localPosition = Vector3.zero;
            worldSpaceCanvas.transform.localRotation = Quaternion.identity;
            worldSpaceCanvas.transform.localScale = Vector3.one * canvasScale;

            RectTransform canvasRect = worldSpaceCanvas.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                canvasRect.sizeDelta = canvasPixelSize;
                canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
                canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
                canvasRect.pivot = new Vector2(0.5f, 0.5f);
            }
        }

        if (visiblePanel != null)
        {
            visiblePanel.anchorMin = Vector2.zero;
            visiblePanel.anchorMax = Vector2.one;
            visiblePanel.offsetMin = Vector2.zero;
            visiblePanel.offsetMax = Vector2.zero;
            visiblePanel.localScale = Vector3.one;
            visiblePanel.localRotation = Quaternion.identity;
        }

        if (transparentHitPlane != null)
        {
            transparentHitPlane.isTrigger = true;
            transparentHitPlane.transform.localPosition = Vector3.zero;
            transparentHitPlane.transform.localRotation = Quaternion.identity;
            transparentHitPlane.transform.localScale = Vector3.one;
            transparentHitPlane.size = new Vector3(physicalSizeMeters.x, physicalSizeMeters.y, hitPlaneDepthMeters);
            transparentHitPlane.center = Vector3.zero;
        }

        if (cursor != null)
        {
            cursor.anchorMin = new Vector2(0.5f, 0.5f);
            cursor.anchorMax = new Vector2(0.5f, 0.5f);
            cursor.pivot = new Vector2(0.5f, 0.5f);
            cursor.anchoredPosition = Vector2.zero;
            cursor.sizeDelta = cursorPixelSize;
            cursor.localScale = Vector3.one;
            cursor.localRotation = Quaternion.identity;
            cursor.SetAsLastSibling();
        }

        ApplyScrollOffset();
    }

    public Vector2 WorldToNormalized(Vector3 worldPoint)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        if (physicalSizeMeters.x <= 0f || physicalSizeMeters.y <= 0f)
        {
            return new Vector2(0.5f, 0.5f);
        }

        return new Vector2(
            Mathf.Clamp01(localPoint.x / physicalSizeMeters.x + 0.5f),
            Mathf.Clamp01(localPoint.y / physicalSizeMeters.y + 0.5f));
    }

    public bool TryClickDebugTarget(Vector2 normalized, out string targetId)
    {
        targetId = debugTargetId;
        if (debugClickTarget == null || worldSpaceCanvas == null || !debugClickTarget.gameObject.activeInHierarchy)
        {
            return false;
        }

        RectTransform canvasRect = worldSpaceCanvas.GetComponent<RectTransform>();
        if (canvasRect == null)
        {
            return false;
        }

        Vector2 canvasSize = canvasRect.sizeDelta;
        Vector2 canvasPoint = new Vector2(
            (Mathf.Clamp01(normalized.x) - 0.5f) * canvasSize.x,
            (Mathf.Clamp01(normalized.y) - 0.5f) * canvasSize.y);

        Vector3[] worldCorners = new Vector3[4];
        debugClickTarget.GetWorldCorners(worldCorners);

        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < worldCorners.Length; i++)
        {
            Vector3 local = canvasRect.InverseTransformPoint(worldCorners[i]);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }

        return canvasPoint.x >= min.x
            && canvasPoint.x <= max.x
            && canvasPoint.y >= min.y
            && canvasPoint.y <= max.y;
    }

    public float Scroll(float stickVertical, float deltaTime)
    {
        if (scrollContent == null || !scrollContent.gameObject.activeInHierarchy)
        {
            return 0f;
        }

        float deltaPixels = stickVertical * scrollPixelsPerUnit * deltaTime;
        if (Mathf.Approximately(deltaPixels, 0f))
        {
            return 0f;
        }

        float previous = scrollOffsetPixels;
        scrollOffsetPixels = Mathf.Clamp(scrollOffsetPixels + deltaPixels, -maxScrollPixels, maxScrollPixels);
        ApplyScrollOffset();
        return scrollOffsetPixels - previous;
    }

    private void ApplyScrollOffset()
    {
        if (scrollContent != null)
        {
            scrollContent.anchoredPosition = new Vector2(scrollContent.anchoredPosition.x, scrollOffsetPixels);
        }
    }

    private void ApplyContentMode()
    {
        if (scrollContent == null)
        {
            return;
        }

        bool showPseudoContent = contentMode == DisplayContentMode.Debug || contentMode == DisplayContentMode.ScrollTask;
        scrollContent.gameObject.SetActive(showPseudoContent);
    }

    private void CachePanelImage()
    {
        if (visiblePanelImage == null && visiblePanel != null)
        {
            visiblePanelImage = visiblePanel.GetComponent<Image>();
        }

        if (!hasCachedBasePanelColor && visiblePanelImage != null)
        {
            basePanelColor = visiblePanelImage.color;
            hasCachedBasePanelColor = true;
        }
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private void OnValidate()
    {
        ApplyConfiguration();
        ApplyContentMode();
    }
}
