using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

[DisallowMultipleComponent]
public class PrototypeInputManager : MonoBehaviour
{
    public static PrototypeInputManager Instance { get; private set; }

    [SerializeField] private InteractionCondition currentCondition = InteractionCondition.RaycastBaseline;
    [SerializeField] private bool allowConditionToggle = false;
    [SerializeField] private float stickDeadzone = 0.08f;

    private readonly List<XRInputDevice> rightHandDevices = new List<XRInputDevice>();
    private bool previousPrimaryButton;
    private bool previousGripButton;
    private bool previousTriggerButton;

    public InteractionCondition CurrentCondition => currentCondition;
    public Vector2 Stick { get; private set; }
    public bool SubmitPressed { get; private set; }
    public bool GripPressed { get; private set; }
    public bool TriggerPressed { get; private set; }
    public bool TriggerHeld { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        ReadInputs();

        if (allowConditionToggle && GetKeyDown(KeyCode.Tab))
        {
            ToggleCondition();
        }
    }

    public void SetCondition(InteractionCondition condition)
    {
        if (currentCondition == condition)
        {
            return;
        }

        currentCondition = condition;
        Debug.Log($"[PrototypeInput] condition={currentCondition}");
    }

    private void ToggleCondition()
    {
        SetCondition(currentCondition == InteractionCondition.RaycastBaseline
            ? InteractionCondition.ExplicitDisplayFocus
            : InteractionCondition.RaycastBaseline);
    }

    private void ReadInputs()
    {
        Vector2 editorStick = Vector2.zero;
        editorStick.x += GetKey(KeyCode.D) || GetKey(KeyCode.RightArrow) ? 1f : 0f;
        editorStick.x -= GetKey(KeyCode.A) || GetKey(KeyCode.LeftArrow) ? 1f : 0f;
        editorStick.y += GetKey(KeyCode.W) || GetKey(KeyCode.UpArrow) ? 1f : 0f;
        editorStick.y -= GetKey(KeyCode.S) || GetKey(KeyCode.DownArrow) ? 1f : 0f;
        editorStick = Vector2.ClampMagnitude(editorStick, 1f);

        Vector2 xrStick = Vector2.zero;
        bool xrPrimary = false;
        bool xrGrip = false;
        bool xrTriggerButton = false;
        float xrTriggerValue = 0f;

        XRInputDevice rightHand = GetRightHandDevice();
        if (rightHand.isValid)
        {
            rightHand.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out xrStick);
            rightHand.TryGetFeatureValue(XRCommonUsages.primaryButton, out xrPrimary);
            rightHand.TryGetFeatureValue(XRCommonUsages.gripButton, out xrGrip);
            rightHand.TryGetFeatureValue(XRCommonUsages.triggerButton, out xrTriggerButton);
            rightHand.TryGetFeatureValue(XRCommonUsages.trigger, out xrTriggerValue);
        }

        Stick = editorStick.sqrMagnitude > xrStick.sqrMagnitude ? editorStick : xrStick;
        if (Stick.magnitude < stickDeadzone)
        {
            Stick = Vector2.zero;
        }

        bool editorSubmit = GetKeyDown(KeyCode.Space);
        bool editorGrip = GetKeyDown(KeyCode.G);
        bool editorTriggerHeld = GetKey(KeyCode.LeftShift);
        bool editorTriggerPressed = GetKeyDown(KeyCode.LeftShift);

        SubmitPressed = editorSubmit || (xrPrimary && !previousPrimaryButton);
        GripPressed = editorGrip || (xrGrip && !previousGripButton);
        TriggerHeld = editorTriggerHeld || xrTriggerButton || xrTriggerValue > 0.5f;
        TriggerPressed = editorTriggerPressed || ((xrTriggerButton || xrTriggerValue > 0.5f) && !previousTriggerButton);

        previousPrimaryButton = xrPrimary;
        previousGripButton = xrGrip;
        previousTriggerButton = xrTriggerButton || xrTriggerValue > 0.5f;
    }

    private XRInputDevice GetRightHandDevice()
    {
        rightHandDevices.Clear();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller,
            rightHandDevices);

        return rightHandDevices.Count > 0 ? rightHandDevices[0] : default;
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
            default:
                return null;
        }
    }
#endif
}
