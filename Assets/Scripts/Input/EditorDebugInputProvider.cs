using UnityEngine;

#pragma warning disable 0414

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
/// <summary>
/// Quest実機なしでEditor上の操作を検証するためのキーボード入力。
/// 本番入力とはPrototypeInputManagerで統合し、実験ロジック側には直接キー入力を書かない。
/// </summary>
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
    [SerializeField] private KeyCode stickClickKey = KeyCode.LeftControl;
    [SerializeField] private KeyCode exitHoldKey = KeyCode.X;
    [SerializeField] private KeyCode resetFocusKey = KeyCode.R;
    [SerializeField] private KeyCode upDownDepthKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode leftRightKey = KeyCode.Alpha2;
    [SerializeField] private KeyCode upDownKey = KeyCode.Alpha3;

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
    public bool SubmitHeld { get; private set; }
    public bool SubmitReleased { get; private set; }
    public bool GripPressed { get; private set; }
    public bool GripHeld { get; private set; }
    public bool TriggerPressed { get; private set; }
    public bool TriggerHeld { get; private set; }
    public bool TriggerReleased { get; private set; }
    public bool StickClickPressed { get; private set; }
    public bool StickClickHeld { get; private set; }
    public bool StickClickReleased { get; private set; }
    public bool ExitPressed { get; private set; }
    public bool ExitHeld { get; private set; }
    public bool ExitReleased { get; private set; }
    public bool ToggleConditionPressed { get; private set; }
    public bool ResetFocusPressed { get; private set; }
    public bool UpDownDepthPressed { get; private set; }
    public bool LeftRightPressed { get; private set; }
    public bool UpDownPressed { get; private set; }

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
        SubmitHeld = GetKey(submitKey);
        SubmitReleased = GetKeyUp(submitKey);
        GripPressed = GetKeyDown(gripKey);
        GripHeld = GetKey(gripKey);
        TriggerHeld = GetKey(triggerKey);
        TriggerPressed = GetKeyDown(triggerKey);
        TriggerReleased = GetKeyUp(triggerKey);
        StickClickHeld = GetKey(stickClickKey);
        StickClickPressed = GetKeyDown(stickClickKey);
        StickClickReleased = GetKeyUp(stickClickKey);
        ExitHeld = GetKey(exitHoldKey);
        ExitPressed = GetKeyDown(exitHoldKey);
        ExitReleased = GetKeyUp(exitHoldKey);
        ToggleConditionPressed = GetKeyDown(toggleConditionKey);
        ResetFocusPressed = GetKeyDown(resetFocusKey);
        UpDownDepthPressed = GetKeyDown(upDownDepthKey);
        LeftRightPressed = GetKeyDown(leftRightKey);
        UpDownPressed = GetKeyDown(upDownKey);

        if (logDebugInputEvents)
        {
            LogPressedEvents();
        }
    }

    private void Clear()
    {
        Stick = Vector2.zero;
        SubmitPressed = false;
        SubmitHeld = false;
        SubmitReleased = false;
        GripPressed = false;
        GripHeld = false;
        TriggerHeld = false;
        TriggerPressed = false;
        TriggerReleased = false;
        StickClickHeld = false;
        StickClickPressed = false;
        StickClickReleased = false;
        ExitPressed = false;
        ExitHeld = false;
        ExitReleased = false;
        ToggleConditionPressed = false;
        ResetFocusPressed = false;
        UpDownDepthPressed = false;
        LeftRightPressed = false;
        UpDownPressed = false;
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
            Debug.Log("[EditorDebugInput] AButtonPressed");
        }

        if (SubmitReleased)
        {
            Debug.Log("[EditorDebugInput] AButtonReleased");
        }

        if (TriggerPressed)
        {
            Debug.Log("[EditorDebugInput] TriggerPressed");
        }

        if (TriggerReleased)
        {
            Debug.Log("[EditorDebugInput] TriggerReleased");
        }

        if (StickClickPressed)
        {
            Debug.Log("[EditorDebugInput] StickClickPressed");
        }

        if (StickClickReleased)
        {
            Debug.Log("[EditorDebugInput] StickClickReleased");
        }

        if (ResetFocusPressed)
        {
            Debug.Log("[EditorDebugInput] Reset focus");
        }

        if (UpDownDepthPressed || LeftRightPressed || UpDownPressed)
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

    private static bool GetKeyUp(KeyCode keyCode)
    {
#if ENABLE_INPUT_SYSTEM
        KeyControl key = GetInputSystemKey(keyCode);
        return key != null && key.wasReleasedThisFrame;
#else
        return UnityEngine.Input.GetKeyUp(keyCode);
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
            case KeyCode.H:
                return keyboard.hKey;
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
            case KeyCode.V:
                return keyboard.vKey;
            default:
                return null;
        }
    }
#endif
}

#pragma warning restore 0414
