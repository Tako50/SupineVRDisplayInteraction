using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public class EditorDebugInputProvider : MonoBehaviour
{
    [Header("Activation")]
    [SerializeField] private bool enableInEditor = true;
    [SerializeField] private bool enableInPlayer = false;
    [SerializeField] private bool forceEnabled = false;

    [Header("Key Bindings")]
    [SerializeField] private KeyCode toggleConditionKey = KeyCode.Tab;
    [SerializeField] private KeyCode gripKey = KeyCode.G;
    [SerializeField] private KeyCode submitKey = KeyCode.Space;
    [SerializeField] private KeyCode triggerKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode resetFocusKey = KeyCode.R;
    [SerializeField] private KeyCode noOcclusionKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode partialOcclusionKey = KeyCode.Alpha2;
    [SerializeField] private KeyCode strongOcclusionKey = KeyCode.Alpha3;

    [Header("Stick Keys")]
    [SerializeField] private KeyCode stickLeftKey = KeyCode.A;
    [SerializeField] private KeyCode stickRightKey = KeyCode.D;
    [SerializeField] private KeyCode stickUpKey = KeyCode.W;
    [SerializeField] private KeyCode stickDownKey = KeyCode.S;
    [SerializeField] private bool useArrowKeys = true;

    [Header("Logging")]
    [SerializeField] private bool logDebugInputEvents = true;

    public bool IsEnabled => forceEnabled || IsPlatformEnabled();
    public bool LogDebugInputEvents => logDebugInputEvents;
    public Vector2 Stick { get; private set; }
    public bool SubmitPressed { get; private set; }
    public bool GripPressed { get; private set; }
    public bool TriggerPressed { get; private set; }
    public bool TriggerHeld { get; private set; }
    public bool ToggleConditionPressed { get; private set; }
    public bool ResetFocusPressed { get; private set; }
    public bool NoOcclusionPressed { get; private set; }
    public bool PartialOcclusionPressed { get; private set; }
    public bool StrongOcclusionPressed { get; private set; }

    private void Update()
    {
        ReadInputs();
    }

    private void ReadInputs()
    {
        if (!IsEnabled)
        {
            Clear();
            return;
        }

        Vector2 stick = Vector2.zero;
        stick.x += GetKey(stickRightKey) || (useArrowKeys && GetKey(KeyCode.RightArrow)) ? 1f : 0f;
        stick.x -= GetKey(stickLeftKey) || (useArrowKeys && GetKey(KeyCode.LeftArrow)) ? 1f : 0f;
        stick.y += GetKey(stickUpKey) || (useArrowKeys && GetKey(KeyCode.UpArrow)) ? 1f : 0f;
        stick.y -= GetKey(stickDownKey) || (useArrowKeys && GetKey(KeyCode.DownArrow)) ? 1f : 0f;

        Stick = Vector2.ClampMagnitude(stick, 1f);
        SubmitPressed = GetKeyDown(submitKey);
        GripPressed = GetKeyDown(gripKey);
        TriggerHeld = GetKey(triggerKey);
        TriggerPressed = GetKeyDown(triggerKey);
        ToggleConditionPressed = GetKeyDown(toggleConditionKey);
        ResetFocusPressed = GetKeyDown(resetFocusKey);
        NoOcclusionPressed = GetKeyDown(noOcclusionKey);
        PartialOcclusionPressed = GetKeyDown(partialOcclusionKey);
        StrongOcclusionPressed = GetKeyDown(strongOcclusionKey);

        if (logDebugInputEvents)
        {
            LogPressedEvents();
        }
    }

    private void Clear()
    {
        Stick = Vector2.zero;
        SubmitPressed = false;
        GripPressed = false;
        TriggerHeld = false;
        TriggerPressed = false;
        ToggleConditionPressed = false;
        ResetFocusPressed = false;
        NoOcclusionPressed = false;
        PartialOcclusionPressed = false;
        StrongOcclusionPressed = false;
    }

    private bool IsPlatformEnabled()
    {
#if UNITY_EDITOR
        return enableInEditor;
#else
        return enableInPlayer;
#endif
    }

    private void LogPressedEvents()
    {
        if (ToggleConditionPressed)
        {
            Debug.Log("[EditorDebugInput] Toggle condition");
        }

        if (GripPressed)
        {
            Debug.Log("[EditorDebugInput] GripDown");
        }

        if (SubmitPressed)
        {
            Debug.Log("[EditorDebugInput] AButtonDown");
        }

        if (TriggerPressed)
        {
            Debug.Log("[EditorDebugInput] TriggerPressed");
        }

        if (ResetFocusPressed)
        {
            Debug.Log("[EditorDebugInput] Reset focus");
        }

        if (NoOcclusionPressed || PartialOcclusionPressed || StrongOcclusionPressed)
        {
            Debug.Log("[EditorDebugInput] Layout preset shortcut");
        }
    }

    private static bool GetKey(KeyCode keyCode)
    {
#if ENABLE_INPUT_SYSTEM
        KeyControl key = GetInputSystemKey(keyCode);
        return key != null && key.isPressed;
#else
        return UnityEngine.Input.GetKey(keyCode);
#endif
    }

    private static bool GetKeyDown(KeyCode keyCode)
    {
#if ENABLE_INPUT_SYSTEM
        KeyControl key = GetInputSystemKey(keyCode);
        return key != null && key.wasPressedThisFrame;
#else
        return UnityEngine.Input.GetKeyDown(keyCode);
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private static KeyControl GetInputSystemKey(KeyCode keyCode)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return null;
        }

        switch (keyCode)
        {
            case KeyCode.A:
                return keyboard.aKey;
            case KeyCode.D:
                return keyboard.dKey;
            case KeyCode.G:
                return keyboard.gKey;
            case KeyCode.R:
                return keyboard.rKey;
            case KeyCode.S:
                return keyboard.sKey;
            case KeyCode.W:
                return keyboard.wKey;
            case KeyCode.Space:
                return keyboard.spaceKey;
            case KeyCode.Tab:
                return keyboard.tabKey;
            case KeyCode.LeftArrow:
                return keyboard.leftArrowKey;
            case KeyCode.RightArrow:
                return keyboard.rightArrowKey;
            case KeyCode.UpArrow:
                return keyboard.upArrowKey;
            case KeyCode.DownArrow:
                return keyboard.downArrowKey;
            case KeyCode.LeftShift:
                return keyboard.leftShiftKey;
            case KeyCode.Alpha1:
                return keyboard.digit1Key;
            case KeyCode.Alpha2:
                return keyboard.digit2Key;
            case KeyCode.Alpha3:
                return keyboard.digit3Key;
            default:
                return null;
        }
    }
#endif
}
