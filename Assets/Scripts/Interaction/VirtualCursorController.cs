using UnityEngine;

[DisallowMultipleComponent]
public class VirtualCursorController : MonoBehaviour
{
    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private float cursorSpeedNormalizedPerSecond = 0.55f;

    public Vector2 NormalizedPosition { get; private set; } = new Vector2(0.5f, 0.5f);

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

        if (inputManager.CurrentCondition != InteractionCondition.ExplicitDisplayFocus)
        {
            return;
        }

        DisplaySurface focusedDisplay = displayManager.FocusedDisplay;
        if (focusedDisplay == null)
        {
            return;
        }

        Vector2 delta = inputManager.Stick * cursorSpeedNormalizedPerSecond * Time.deltaTime;
        if (delta.sqrMagnitude > 0f)
        {
            NormalizedPosition = new Vector2(
                Mathf.Clamp01(NormalizedPosition.x + delta.x),
                Mathf.Clamp01(NormalizedPosition.y + delta.y));
        }

        displayManager.SetCursorNormalized(focusedDisplay, NormalizedPosition, true);

        if (inputManager.SubmitPressed)
        {
            Debug.Log($"[VirtualCursor] condition={inputManager.CurrentCondition}, displayId={focusedDisplay.name}, normalized={Format(NormalizedPosition)}");
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
    }

    public void WarpTo(DisplaySurface display, Vector2 normalized)
    {
        NormalizedPosition = new Vector2(Mathf.Clamp01(normalized.x), Mathf.Clamp01(normalized.y));
        if (displayManager != null)
        {
            displayManager.SetCursorNormalized(display, NormalizedPosition, true);
        }
    }

    private static string Format(Vector2 value)
    {
        return $"({value.x:0.000}, {value.y:0.000})";
    }
}
