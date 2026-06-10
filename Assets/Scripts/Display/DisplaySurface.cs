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
/// <summary>
/// 1枚の仮想ディスプレイを表す。
/// World Space Canvas、透明HitPlane、カーソル、疑似コンテンツをまとめ、ワールド座標と表示内正規化座標を変換する。
/// </summary>
public class DisplaySurface : MonoBehaviour
{
    [Header("Display Parts")]
    [SerializeField] private Canvas worldSpaceCanvas;
    [SerializeField] private RectTransform visiblePanel;
    [SerializeField] private BoxCollider transparentHitPlane;
    [SerializeField] private RectTransform cursor;
    [SerializeField] private RectTransform scrollContent;

    [Header("Sizing")]
    [SerializeField] private Vector2 physicalSizeMeters = new Vector2(0.8f, 0.45f);
    [SerializeField] private Vector2 canvasPixelSize = new Vector2(800f, 450f);
    [SerializeField] private float canvasScale = 0.001f;
    [SerializeField] private float hitPlaneDepthMeters = 0.02f;
    [SerializeField] private Vector2 cursorPixelSize = new Vector2(10f, 10f);

    [Header("Debug Content")]
    [SerializeField] private DisplayContentMode contentMode = DisplayContentMode.Debug;
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
    public Vector2 PhysicalSizeMeters => physicalSizeMeters;
    public Vector2 CanvasPixelSize => canvasPixelSize;
    public DisplayContentMode ContentMode => contentMode;
    public float ScrollOffsetPixels => scrollOffsetPixels;

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

    public void AssignScrollContent(RectTransform contentRoot)
    {
        scrollContent = contentRoot;
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
        // Displayのローカル平面上の位置を0-1へ変換する。外側は端へクランプする。
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        if (physicalSizeMeters.x <= 0f || physicalSizeMeters.y <= 0f)
        {
            return new Vector2(0.5f, 0.5f);
        }

        return new Vector2(
            Mathf.Clamp01(localPoint.x / physicalSizeMeters.x + 0.5f),
            Mathf.Clamp01(localPoint.y / physicalSizeMeters.y + 0.5f));
    }

    public bool TryRayToClampedNormalized(Ray ray, out Vector2 normalized)
    {
        // 視線が表示のHitPlane外を向いていても、表示平面との交点を使って一番近い端へ寄せる。
        normalized = new Vector2(0.5f, 0.5f);
        if (physicalSizeMeters.x <= 0f || physicalSizeMeters.y <= 0f || ray.direction == Vector3.zero)
        {
            return false;
        }

        Plane displayPlane = new Plane(transform.forward, transform.position);
        if (!displayPlane.Raycast(ray, out float enter) || enter < 0f)
        {
            return false;
        }

        Vector3 worldPoint = ray.GetPoint(enter);
        normalized = WorldToNormalized(worldPoint);
        return true;
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

    public void ResetScroll()
    {
        scrollOffsetPixels = 0f;
        ApplyScrollOffset();
    }

    public void SetMaxScrollPixels(float value)
    {
        maxScrollPixels = Mathf.Max(0f, value);
        scrollOffsetPixels = Mathf.Clamp(scrollOffsetPixels, -maxScrollPixels, maxScrollPixels);
        ApplyScrollOffset();
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
        // T1ポインティング中はスクロール用の疑似文章を消し、スクロール課題だけで再表示できるようにする。
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
