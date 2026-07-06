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
    [SerializeField] private Vector2 physicalSizeMeters = DisplayGeometry.DefaultPhysicalSizeMeters;
    [SerializeField] private Vector2 canvasPixelSize = DisplayGeometry.DefaultCanvasPixelSize;
    [Tooltip("Fallback meters-per-pixel scale used only when the physical or pixel size is invalid.")]
    [SerializeField] private float canvasScale = 0.001f;
    [SerializeField] private float hitPlaneDepthMeters = 0.02f;
    [SerializeField] private Vector2 cursorPixelSize = new Vector2(10f, 10f);

    [Header("Debug Content")]
    [SerializeField] private DisplayContentMode contentMode = DisplayContentMode.Debug;
    [SerializeField] private float scrollPixelsPerUnit = 160f;
    [SerializeField] private float maxScrollPixels = 420f;

    [Header("Gaze Highlight")]
    [SerializeField] private Color gazeHighlightColor = new Color(0.302f, 0.639f, 1f, 1f);
    [SerializeField] private Color displayFrameColor = new Color(0.4f, 0.4f, 0.4f, 1f);
    [Range(0f, 1f)]
    [SerializeField] private float gazeHighlightStrength = 1f;
    [Min(1f)]
    [SerializeField] private float gazeHighlightBorderThicknessPixels = 6f;

    private float scrollOffsetPixels;
    private Image visiblePanelImage;
    private RectTransform gazeHighlightFrame;
    private Image[] gazeHighlightEdges;
    private Color basePanelColor = Color.white;
    private bool hasCachedBasePanelColor;
    private bool gazeHighlighted;
    private bool applyingOnValidate;
    private static Sprite circleCursorSprite;

    public Canvas WorldSpaceCanvas => worldSpaceCanvas;
    public RectTransform VisiblePanel => visiblePanel;
    public BoxCollider TransparentHitPlane => transparentHitPlane;
    public RectTransform Cursor => cursor;
    public RectTransform ScrollContent => scrollContent;
    public Vector2 PhysicalSizeMeters => physicalSizeMeters;
    public Vector2 CanvasPixelSize => canvasPixelSize;
    public DisplayContentMode ContentMode => contentMode;
    public float ScrollOffsetPixels => scrollOffsetPixels;
    public bool IsGazeHighlighted => gazeHighlighted;

    private void Awake()
    {
        CachePanelImage();
        EnsureCursorVisual();
        EnsureHighlightFrame();
        ApplyHighlightFrameVisual();
        SetHighlightFrameVisible(true);
    }

    public void AssignParts(Canvas canvas, RectTransform panel, BoxCollider hitPlane, RectTransform cursorRect)
    {
        worldSpaceCanvas = canvas;
        visiblePanel = panel;
        transparentHitPlane = hitPlane;
        cursor = cursorRect;
        visiblePanelImage = null;
        gazeHighlightFrame = null;
        gazeHighlightEdges = null;
        hasCachedBasePanelColor = false;
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
        // 入力フォーカスの状態と視線ハイライトは独立させる。
        if (!gazeHighlighted)
        {
            RestoreBasePanelColor();
            ApplyHighlightFrameVisual();
            SetHighlightFrameVisible(true);
        }
    }

    public void SetCandidateVisual(bool candidate, bool overlapPreview)
    {
        // 候補管理から視線ハイライトの見た目を上書きしない。
        if (!gazeHighlighted)
        {
            RestoreBasePanelColor();
            ApplyHighlightFrameVisual();
            SetHighlightFrameVisible(true);
        }
    }

    public void SetFocused(bool focused)
    {
        CachePanelImage();
        EnsureHighlightFrame();
        gazeHighlighted = focused;
        RestoreBasePanelColor();

        if (!focused)
        {
            ApplyHighlightFrameVisual();
            SetHighlightFrameVisible(true);
            return;
        }

        ApplyHighlightFrameVisual();
        SetHighlightFrameVisible(true);
    }

    public void SetSize(Vector2 sizeMeters, Vector2 pixelSize)
    {
        physicalSizeMeters = sizeMeters;
        canvasPixelSize = pixelSize;
        ApplyConfiguration();
    }

    public Vector2 GetCanvasSize()
    {
        if (worldSpaceCanvas != null)
        {
            RectTransform canvasRect = worldSpaceCanvas.GetComponent<RectTransform>();
            if (canvasRect != null && canvasRect.rect.width > 0f && canvasRect.rect.height > 0f)
            {
                return canvasRect.rect.size;
            }
        }

        return DisplayGeometry.ValidPixelSize(canvasPixelSize);
    }

    public Vector2 NormalizedToCanvasPosition(Vector2 normalized)
    {
        Vector2 canvasSize = GetCanvasSize();
        return new Vector2(
            (Mathf.Clamp01(normalized.x) - 0.5f) * canvasSize.x,
            (Mathf.Clamp01(normalized.y) - 0.5f) * canvasSize.y);
    }

    public Vector2 NormalizedToCanvasSize(Vector2 normalizedSize)
    {
        Vector2 canvasSize = GetCanvasSize();
        return new Vector2(
            Mathf.Max(0f, normalizedSize.x) * canvasSize.x,
            Mathf.Max(0f, normalizedSize.y) * canvasSize.y);
    }

    public void ApplyConfiguration()
    {
        if (worldSpaceCanvas != null)
        {
            worldSpaceCanvas.renderMode = RenderMode.WorldSpace;
            worldSpaceCanvas.transform.localPosition = Vector3.zero;
            worldSpaceCanvas.transform.localRotation = Quaternion.identity;
            worldSpaceCanvas.transform.localScale = ComputeCanvasScale();

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
            EnsureCursorVisual();
            if (!applyingOnValidate)
            {
                cursor.SetAsLastSibling();
            }
        }

        ApplyScrollOffset();
    }

    private Vector3 ComputeCanvasScale()
    {
        if (physicalSizeMeters.x <= 0f
            || physicalSizeMeters.y <= 0f
            || canvasPixelSize.x <= 0f
            || canvasPixelSize.y <= 0f)
        {
            float fallbackScale = Mathf.Max(0.000001f, canvasScale);
            return Vector3.one * fallbackScale;
        }

        float scaleX = physicalSizeMeters.x / canvasPixelSize.x;
        float scaleY = physicalSizeMeters.y / canvasPixelSize.y;
        return new Vector3(scaleX, scaleY, Mathf.Min(scaleX, scaleY));
    }

    private void EnsureCursorVisual()
    {
        if (cursor == null)
        {
            return;
        }

        Image image = cursor.GetComponent<Image>();
        if (image == null)
        {
            image = cursor.gameObject.AddComponent<Image>();
            image.color = Color.yellow;
        }

        image.color = Color.yellow;
        image.sprite = GetCircleCursorSprite();
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = false;

        Outline outline = cursor.GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = false;
        }
    }

    private static Sprite GetCircleCursorSprite()
    {
        if (circleCursorSprite != null)
        {
            return circleCursorSprite;
        }

        const int textureSize = 64;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            name = "GeneratedCircleCursorSprite",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color clear = Color.clear;
        Color white = Color.white;
        float center = (textureSize - 1) * 0.5f;
        float radius = center;
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float dx = x - center;
                float dy = y - center;
                texture.SetPixel(x, y, dx * dx + dy * dy <= radius * radius ? white : clear);
            }
        }

        texture.Apply();
        circleCursorSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            textureSize);
        circleCursorSprite.name = "GeneratedCircleCursorSprite";
        return circleCursorSprite;
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

    private void EnsureHighlightFrame()
    {
        if (visiblePanel == null)
        {
            return;
        }

        if (gazeHighlightFrame == null)
        {
            Transform existingFrame = visiblePanel.Find("GazeHighlightFrame");
            GameObject frameObject = existingFrame != null
                ? existingFrame.gameObject
                : new GameObject("GazeHighlightFrame", typeof(RectTransform));
            frameObject.transform.SetParent(visiblePanel, false);
            gazeHighlightFrame = frameObject.GetComponent<RectTransform>();
        }

        gazeHighlightFrame.anchorMin = Vector2.zero;
        gazeHighlightFrame.anchorMax = Vector2.one;
        gazeHighlightFrame.offsetMin = Vector2.zero;
        gazeHighlightFrame.offsetMax = Vector2.zero;
        gazeHighlightFrame.pivot = new Vector2(0.5f, 0.5f);
        gazeHighlightFrame.localScale = Vector3.one;
        gazeHighlightFrame.localRotation = Quaternion.identity;
        gazeHighlightFrame.SetAsLastSibling();

        if (gazeHighlightEdges == null || gazeHighlightEdges.Length != 4)
        {
            gazeHighlightEdges = new Image[4];
        }

        gazeHighlightEdges[0] = EnsureHighlightEdge("Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        gazeHighlightEdges[1] = EnsureHighlightEdge("Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
        gazeHighlightEdges[2] = EnsureHighlightEdge("Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
        gazeHighlightEdges[3] = EnsureHighlightEdge("Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f));
        ApplyHighlightFrameVisual();
    }

    private Image EnsureHighlightEdge(string edgeName, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        Transform existingEdge = gazeHighlightFrame.Find(edgeName);
        GameObject edgeObject = existingEdge != null
            ? existingEdge.gameObject
            : new GameObject(edgeName, typeof(RectTransform));
        edgeObject.transform.SetParent(gazeHighlightFrame, false);

        RectTransform rect = edgeObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        Image edgeImage = edgeObject.GetComponent<Image>();
        if (edgeImage == null)
        {
            edgeImage = edgeObject.AddComponent<Image>();
        }

        edgeImage.raycastTarget = false;
        return edgeImage;
    }

    private void ApplyHighlightFrameVisual()
    {
        if (gazeHighlightFrame == null || gazeHighlightEdges == null)
        {
            return;
        }

        float thickness = Mathf.Max(1f, gazeHighlightBorderThicknessPixels);
        Color frameColor = gazeHighlighted ? gazeHighlightColor : displayFrameColor;
        if (gazeHighlighted)
        {
            frameColor.a *= Mathf.Clamp01(gazeHighlightStrength);
        }

        ConfigureHighlightEdge(gazeHighlightEdges[0], new Vector2(0f, thickness), frameColor);
        ConfigureHighlightEdge(gazeHighlightEdges[1], new Vector2(0f, thickness), frameColor);
        ConfigureHighlightEdge(gazeHighlightEdges[2], new Vector2(thickness, 0f), frameColor);
        ConfigureHighlightEdge(gazeHighlightEdges[3], new Vector2(thickness, 0f), frameColor);
    }

    private static void ConfigureHighlightEdge(Image edgeImage, Vector2 sizeDelta, Color color)
    {
        if (edgeImage == null)
        {
            return;
        }

        RectTransform rect = edgeImage.rectTransform;
        rect.sizeDelta = sizeDelta;
        edgeImage.color = color;
    }

    private void SetHighlightFrameVisible(bool visible)
    {
        if (gazeHighlightFrame != null)
        {
            gazeHighlightFrame.gameObject.SetActive(visible);
        }
    }

    private void RestoreBasePanelColor()
    {
        CachePanelImage();
        if (visiblePanelImage != null)
        {
            visiblePanelImage.color = WithAlpha(basePanelColor, basePanelColor.a);
        }
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private void OnValidate()
    {
        applyingOnValidate = true;
        try
        {
            ApplyConfiguration();
            ApplyContentMode();
            ApplyHighlightFrameVisual();
            SetHighlightFrameVisible(gazeHighlighted);
        }
        finally
        {
            applyingOnValidate = false;
        }
    }
}
