using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class DisplaySurface : MonoBehaviour
{
    [Header("Display Parts")]
    [SerializeField] private Canvas worldSpaceCanvas;
    [SerializeField] private RectTransform visiblePanel;
    [SerializeField] private BoxCollider transparentHitPlane;
    [SerializeField] private RectTransform cursor;

    [Header("Sizing")]
    [SerializeField] private Vector2 physicalSizeMeters = new Vector2(0.8f, 0.45f);
    [SerializeField] private Vector2 canvasPixelSize = new Vector2(800f, 450f);
    [SerializeField] private float canvasScale = 0.001f;
    [SerializeField] private float hitPlaneDepthMeters = 0.02f;

    public Canvas WorldSpaceCanvas => worldSpaceCanvas;
    public RectTransform VisiblePanel => visiblePanel;
    public BoxCollider TransparentHitPlane => transparentHitPlane;
    public RectTransform Cursor => cursor;
    public Vector2 PhysicalSizeMeters => physicalSizeMeters;

    public void AssignParts(Canvas canvas, RectTransform panel, BoxCollider hitPlane, RectTransform cursorRect)
    {
        worldSpaceCanvas = canvas;
        visiblePanel = panel;
        transparentHitPlane = hitPlane;
        cursor = cursorRect;
        ApplyConfiguration();
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
            cursor.sizeDelta = new Vector2(24f, 24f);
            cursor.localScale = Vector3.one;
            cursor.localRotation = Quaternion.identity;
        }
    }

    private void OnValidate()
    {
        ApplyConfiguration();
    }
}
