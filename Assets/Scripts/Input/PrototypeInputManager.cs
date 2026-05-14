using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

[DisallowMultipleComponent]
public class PrototypeInputManager : MonoBehaviour
{
    public static PrototypeInputManager Instance { get; private set; }

    [SerializeField] private InteractionCondition currentCondition = InteractionCondition.RaycastBaseline;
    [SerializeField] private bool allowConditionToggle = true;
    [SerializeField] private float stickDeadzone = 0.08f;
    [SerializeField] private EditorDebugInputProvider debugInputProvider;

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
    public bool ResetFocusPressed => debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.ResetFocusPressed;
    public bool DebugInputEnabled => debugInputProvider != null && debugInputProvider.IsEnabled;

    private void Awake()
    {
        Instance = this;
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();
        ReadInputs();

        if (allowConditionToggle
            && debugInputProvider != null
            && debugInputProvider.IsEnabled
            && debugInputProvider.ToggleConditionPressed)
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

        Vector2 debugStick = debugInputProvider != null && debugInputProvider.IsEnabled
            ? debugInputProvider.Stick
            : Vector2.zero;

        Stick = debugStick.sqrMagnitude > xrStick.sqrMagnitude ? debugStick : xrStick;
        if (Stick.magnitude < stickDeadzone)
        {
            Stick = Vector2.zero;
        }

        bool debugSubmit = debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.SubmitPressed;
        bool debugGrip = debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.GripPressed;
        bool debugTriggerHeld = debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.TriggerHeld;
        bool debugTriggerPressed = debugInputProvider != null && debugInputProvider.IsEnabled && debugInputProvider.TriggerPressed;

        SubmitPressed = debugSubmit || (xrPrimary && !previousPrimaryButton);
        GripPressed = debugGrip || (xrGrip && !previousGripButton);
        TriggerHeld = debugTriggerHeld || xrTriggerButton || xrTriggerValue > 0.5f;
        TriggerPressed = debugTriggerPressed || ((xrTriggerButton || xrTriggerValue > 0.5f) && !previousTriggerButton);

        previousPrimaryButton = xrPrimary;
        previousGripButton = xrGrip;
        previousTriggerButton = xrTriggerButton || xrTriggerValue > 0.5f;
    }

    private void ResolveReferences()
    {
        if (debugInputProvider == null)
        {
            debugInputProvider = FindObjectOfType<EditorDebugInputProvider>();
        }
    }

    private XRInputDevice GetRightHandDevice()
    {
        rightHandDevices.Clear();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller,
            rightHandDevices);

        return rightHandDevices.Count > 0 ? rightHandDevices[0] : default;
    }

}
