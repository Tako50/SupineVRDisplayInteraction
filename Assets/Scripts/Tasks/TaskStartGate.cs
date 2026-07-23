using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
/// <summary>
/// タスク開始前のSTARTボタンとカウントダウンを共通管理する。
/// </summary>
public class TaskStartGate : MonoBehaviour
{
    private RectTransform gateRect;
    private Image gateImage;
    private Text gateText;
    private DisplaySurface display;
    private Vector2 normalizedPosition;
    private Vector2 normalizedSize;
    private float countdownSeconds;
    private Color buttonColor;
    private Color countdownColor;
    private Color lockedButtonColor;
    private string readyButtonLabel = "START";
    private Action countdownCompleted;
    private string logPrefix = "[TaskStartGate]";
    private bool waitingForButton;
    private bool buttonLocked;
    private float buttonUnlockTime;
    private bool countdownRunning;
    private float countdownEndTime;
    private int lastCountdownNumber;
    private SyncMarkerController syncMarkerController;

    public static TaskStartGate GetOrCreate(DisplaySurface targetDisplay)
    {
        if (targetDisplay == null || targetDisplay.WorldSpaceCanvas == null)
        {
            return null;
        }

        Transform canvasTransform = targetDisplay.WorldSpaceCanvas.transform;
        Transform existing = canvasTransform.Find("TaskStartGate");
        GameObject gateObject = existing != null
            ? existing.gameObject
            : new GameObject("TaskStartGate", typeof(RectTransform));
        gateObject.transform.SetParent(canvasTransform, false);

        TaskStartGate gate = gateObject.GetComponent<TaskStartGate>();
        return gate != null ? gate : gateObject.AddComponent<TaskStartGate>();
    }

    public void Show(
        DisplaySurface targetDisplay,
        Vector2 buttonPosition,
        Vector2 buttonSize,
        float durationSeconds,
        Color normalColor,
        Color activeCountdownColor,
        Action onCountdownCompleted,
        string ownerLogPrefix,
        float buttonLockSeconds = 0f,
        string buttonLabel = "START")
    {
        display = targetDisplay;
        normalizedPosition = buttonPosition;
        normalizedSize = buttonSize;
        countdownSeconds = Mathf.Max(0f, durationSeconds);
        buttonColor = normalColor;
        countdownColor = activeCountdownColor;
        lockedButtonColor = Color.Lerp(normalColor, Color.gray, 0.7f);
        readyButtonLabel = string.IsNullOrWhiteSpace(buttonLabel) ? "START" : buttonLabel.Trim();
        countdownCompleted = onCountdownCompleted;
        logPrefix = string.IsNullOrWhiteSpace(ownerLogPrefix)
            ? "[TaskStartGate]"
            : ownerLogPrefix;

        EnsureVisuals();
        ApplyRect();
        waitingForButton = true;
        buttonLocked = buttonLockSeconds > 0f;
        buttonUnlockTime = Time.time + Mathf.Max(0f, buttonLockSeconds);
        countdownRunning = false;
        lastCountdownNumber = 0;
        gameObject.SetActive(true);
        gateRect.SetAsLastSibling();
        if (buttonLocked)
        {
            UpdateButtonLockVisual();
        }
        else
        {
            ShowReadyButtonVisual();
        }
        syncMarkerController = SyncMarkerController.GetOrCreate();
    }

    public bool TryHandleClick(string displayId, Vector2 normalizedClickPosition)
    {
        if (!waitingForButton
            || countdownRunning
            || display == null
            || string.IsNullOrEmpty(displayId)
            || !string.Equals(displayId, display.name, StringComparison.Ordinal)
            || !GetButtonRect().Contains(normalizedClickPosition))
        {
            return false;
        }

        if (buttonLocked)
        {
            return true;
        }

        countdownRunning = true;
        countdownEndTime = Time.time + countdownSeconds;
        lastCountdownNumber = 0;
        gateImage.color = countdownColor;
        UpdateCountdownText();
        Debug.Log($"{logPrefix} start button pressed. countdown={countdownSeconds:0.0}s");
        return true;
    }

    public void Hide()
    {
        waitingForButton = false;
        buttonLocked = false;
        countdownRunning = false;
        countdownCompleted = null;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (waitingForButton && buttonLocked)
        {
            if (Time.time < buttonUnlockTime)
            {
                UpdateButtonLockVisual();
                return;
            }

            buttonLocked = false;
            ShowReadyButtonVisual();
        }

        if (!countdownRunning)
        {
            return;
        }

        UpdateCountdownText();
        if (Time.time < countdownEndTime)
        {
            return;
        }

        Action completed = countdownCompleted;
        Hide();
        completed?.Invoke();
    }

    private void UpdateButtonLockVisual()
    {
        if (gateImage == null || gateText == null)
        {
            return;
        }

        int remainingSeconds = Mathf.Max(1, Mathf.CeilToInt(buttonUnlockTime - Time.time));
        gateImage.color = lockedButtonColor;
        gateText.text = $"{readyButtonLabel}\n{remainingSeconds}s";
    }

    private void ShowReadyButtonVisual()
    {
        if (gateImage != null)
        {
            gateImage.color = buttonColor;
        }

        if (gateText != null)
        {
            gateText.text = readyButtonLabel;
        }
    }

    private void EnsureVisuals()
    {
        gateRect = GetComponent<RectTransform>();
        gateRect.anchorMin = new Vector2(0.5f, 0.5f);
        gateRect.anchorMax = new Vector2(0.5f, 0.5f);
        gateRect.pivot = new Vector2(0.5f, 0.5f);

        gateImage = GetComponent<Image>();
        if (gateImage == null)
        {
            gateImage = gameObject.AddComponent<Image>();
        }

        Transform textTransform = transform.Find("Label");
        GameObject textObject = textTransform != null
            ? textTransform.gameObject
            : new GameObject("Label", typeof(RectTransform));
        textObject.transform.SetParent(transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        gateText = textObject.GetComponent<Text>();
        if (gateText == null)
        {
            gateText = textObject.AddComponent<Text>();
        }

        gateText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        gateText.fontSize = 42;
        gateText.fontStyle = FontStyle.Bold;
        gateText.alignment = TextAnchor.MiddleCenter;
        gateText.color = Color.white;
        gateText.raycastTarget = false;
    }

    private void ApplyRect()
    {
        if (display == null || gateRect == null)
        {
            return;
        }

        gateRect.anchoredPosition = display.NormalizedToCanvasPosition(normalizedPosition);
        Vector2 buttonSize = display.NormalizedToCanvasSize(normalizedSize);
        gateRect.sizeDelta = new Vector2(
            Mathf.Max(32f, buttonSize.x),
            Mathf.Max(32f, buttonSize.y));
    }

    private Rect GetButtonRect()
    {
        return new Rect(normalizedPosition - normalizedSize * 0.5f, normalizedSize);
    }

    private void UpdateCountdownText()
    {
        if (gateText == null)
        {
            return;
        }

        float remaining = Mathf.Max(0f, countdownEndTime - Time.time);
        int count = Mathf.Max(1, Mathf.CeilToInt(remaining));
        gateText.text = count.ToString(CultureInfo.InvariantCulture);

        if (count != lastCountdownNumber)
        {
            lastCountdownNumber = count;
            if (count >= 1 && count <= 3 && countdownSeconds > 0f)
            {
                if (syncMarkerController == null)
                {
                    syncMarkerController = SyncMarkerController.GetOrCreate();
                }

                syncMarkerController?.PlayCountdownBeep();
            }
        }
    }
}
