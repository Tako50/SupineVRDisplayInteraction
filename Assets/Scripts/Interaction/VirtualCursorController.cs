using UnityEngine;

[DisallowMultipleComponent]
/// <summary>
/// ExplicitDisplayFocus用の仮想カーソルを動かす。
/// 通常は右スティックで動き、グリップ保持中だけ視線位置へ追従する。
/// </summary>
public class VirtualCursorController : MonoBehaviour
{
    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private GazeProvider gazeProvider;
    [SerializeField] private float cursorSpeed = 0.55f;
    [SerializeField] private float deadzone = 0.08f;
    [SerializeField] private float acceleration = 0f;
    [SerializeField] private bool allowStickCursorMovement = true;
    [SerializeField] private bool logCursorMovement = false;
    [SerializeField] private float cursorMoveLogInterval = 0.25f;

    private float lastCursorMoveLogTime;
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

        if (inputManager.GripHeld)
        {
            // グリップ中は視線でカーソルを置き直す。表示外ならDisplaySurface側で端へクランプされる。
            UpdateCursorFromGaze(focusedDisplay);
            displayManager.SetCursorNormalized(focusedDisplay, NormalizedPosition, true);
            return;
        }

        if (!allowStickCursorMovement)
        {
            displayManager.SetCursorNormalized(focusedDisplay, NormalizedPosition, true);
            return;
        }

        Vector2 stick = inputManager.Stick;
        if (stick.magnitude < deadzone || inputManager.TriggerHeld)
        {
            // トリガー中はスクロール操作を優先し、カーソル移動と競合させない。
            stick = Vector2.zero;
        }

        float speed = cursorSpeed;
        if (acceleration > 0f)
        {
            speed += acceleration * stick.magnitude;
        }

        Vector2 delta = stick * speed * Time.deltaTime;
        if (delta.sqrMagnitude > 0f)
        {
            NormalizedPosition = new Vector2(
                Mathf.Clamp01(NormalizedPosition.x + delta.x),
                Mathf.Clamp01(NormalizedPosition.y + delta.y));

            if (logCursorMovement && Time.time - lastCursorMoveLogTime >= cursorMoveLogInterval)
            {
                lastCursorMoveLogTime = Time.time;
                Debug.Log($"[VirtualCursor] moved displayId={focusedDisplay.name}, normalized={Format(NormalizedPosition)}");
            }
        }

        displayManager.SetCursorNormalized(focusedDisplay, NormalizedPosition, true);

        if (inputManager.SubmitReleased)
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

        if (gazeProvider == null)
        {
            gazeProvider = FindObjectOfType<GazeProvider>();
        }
    }

    private void UpdateCursorFromGaze(DisplaySurface focusedDisplay)
    {
        if (focusedDisplay == null || gazeProvider == null)
        {
            return;
        }

        Ray gazeRay = gazeProvider.GetGazeRay();
        if (focusedDisplay.TryRayToClampedNormalized(gazeRay, out Vector2 clampedNormalized))
        {
            NormalizedPosition = clampedNormalized;
        }
    }

    public void WarpTo(DisplaySurface display, Vector2 normalized)
    {
        NormalizedPosition = new Vector2(Mathf.Clamp01(normalized.x), Mathf.Clamp01(normalized.y));
        if (displayManager != null)
        {
            displayManager.SetOnlyCursorsVisible(display, null);
            displayManager.SetCursorNormalized(display, NormalizedPosition, true);
        }
    }

    public void SetStickCursorMovementEnabled(bool enabled)
    {
        allowStickCursorMovement = enabled;
    }

    private static string Format(Vector2 value)
    {
        return $"({value.x:0.000}, {value.y:0.000})";
    }
}
