using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public enum WebViewSessionPhase
{
    ConditionSelection,
    Training,
    MainTask
}

public enum WebViewInputMode
{
    LegacyDirectScrollAndSubmitDrag,
    DirectScrollAndSeek,
    StickTouchGesture
}

public enum T2ContentSet
{
    ACampGear2024,
    BCampGear2025
}

[DisallowMultipleComponent]
/// <summary>
/// T2枠を使った自由WebView利用セッションを管理する。
/// </summary>
public class WebViewSessionManager : MonoBehaviour
{
    private enum T2TrainingStep
    {
        PauseYouTube,
        PlayYouTube,
        SkipForwardTenSeconds,
        SeekYouTube,
        ScrollComparisonPage,
        OpenProductDetail,
        AddCandidate,
        RemoveCandidate,
        Completed
    }

    private const int PracticeStepsPerRound = (int)T2TrainingStep.Completed;

    private enum T2MainStage
    {
        V2D,
        D2V,
        SemiFree,
        Finalize
    }

    [Header("References")]
    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private ExperimentManager experimentManager;
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private Logger logger;
    [SerializeField] private VirtualCursorController virtualCursorController;
    [SerializeField] private RaycastPointer raycastPointer;
    [SerializeField] private DisplaySurface displayA;
    [SerializeField] private DisplaySurface displayB;
    [SerializeField] private Camera hmdCamera;
    [SerializeField] private TLabWebViewDisplayBridge targetTLabWebViewBridge;
    [SerializeField] private bool autoAddTLabBridgeToTargetDisplay = true;

    [Header("Input")]
    [Tooltip("Direct Scroll And Seek is the T2 default: Baseline uses trigger+Ray for Web UI drag, Explicit uses trigger+horizontal stick for Web UI drag, and Explicit trigger+vertical stick scrolls.")]
    [SerializeField] private WebViewInputMode inputMode = WebViewInputMode.DirectScrollAndSeek;

    [Header("Session")]
    [SerializeField] private string targetDisplayId = "Display_B_Back";
    [SerializeField] private string initialUrl = "https://www.youtube.com";
    [SerializeField] private bool enableSecondaryWebView = true;
    [SerializeField] private string secondaryDisplayId = "Display_A_Front";
    [Tooltip("Comparison Web URL served by the local Vite development server. Update the host when the Mac LAN address changes.")]
    [SerializeField] private string secondaryInitialUrl = "http://133.87.151.83:5173/";
    [SerializeField] private bool autoAddTLabBridgeToSecondaryDisplay = true;
    [SerializeField] private bool disableOtherWebViewsOnStart = true;
    [SerializeField] private bool disableWebViewOnReturn = true;
    [SerializeField] private bool focusTargetDisplayOnStart = true;
    [SerializeField] private Vector2 initialCursorNormalizedPosition = new Vector2(0.5f, 0.5f);

    [Header("T2 Participant")]
    [SerializeField] private string participantId = "P000";
    [SerializeField] private string sessionId = "S000";
    [SerializeField] private string runId = "Run";
    [Tooltip("Counterbalance label such as G1, G2, G3, or G4.")]
    [SerializeField] private string conditionOrder = "G1";
    [SerializeField] private T2ContentSet selectedContentSet = T2ContentSet.ACampGear2024;

    [Header("T2 Content")]
    [SerializeField] private string youtubeUrlSetA = "https://www.youtube.com/watch?v=oCKnZl-XT1Y";
    [Tooltip("A separate video from the Main video's channel, used only for practice. Do not use another segment of the Main video.")]
    [SerializeField] private string practiceYoutubeUrl = "https://www.youtube.com/watch?v=HQRzNpPDk0k";
    [Tooltip("Practice-only comparison Web content. Keep the same UI as Main, but use different products/content.")]
    [SerializeField] private string practiceComparisonInitialUrl = "http://133.87.151.83:5173/?mode=practice";
    [Tooltip("Forces YouTube's HTML video element to unmute when T2 telemetry attaches or playback starts.")]
    [SerializeField] private bool forceYoutubeUnmuted = true;
    [Range(0f, 1f)]
    [SerializeField] private float youtubeForcedVolume = 1f;
    [Tooltip("Strict D2V completion range for Content A. The task completes only when the D2V target detail has been opened and YouTube is paused within this range during the D2V stage.")]
    [SerializeField] private Vector2 d2vPauseRangeSetASeconds = new Vector2(688f, 743f);
    [Tooltip("TextAsset Resources path for the standalone comparison Web bundled into the Unity build.")]
    [SerializeField] private string comparisonPageResourceSetA = "T2/t2_content_2024";

    [Header("T2 Procedure")]
    [Tooltip("Minimum complete P01-P08 rounds before the experimenter may end practice. Practice repeats until readiness is confirmed.")]
    [Min(1)]
    [FormerlySerializedAs("practiceInstructionRounds")]
    [SerializeField] private int minimumPracticeRounds = 1;
    [Tooltip("Main-task duration, measured from the practice-to-Main transition.")]
    [Min(10f)]
    [SerializeField] private float totalTaskDurationSeconds = 720f;
    [Tooltip("Minimum elapsed time from Main start before the final candidate can be confirmed.")]
    [Min(0f)]
    [SerializeField] private float candidateConfirmationUnlockSeconds = 690f;
    [Tooltip("Practice stage timeout in seconds. Set to 0 until the experiment value is decided.")]
    [Min(0f)]
    [SerializeField] private float practiceStageTimeoutSeconds;
    [Tooltip("V2D stage timeout in seconds. Set to 0 until the experiment value is decided.")]
    [Min(0f)]
    [SerializeField] private float v2dStageTimeoutSeconds;
    [Tooltip("D2V stage timeout in seconds. Set to 0 until the experiment value is decided.")]
    [Min(0f)]
    [SerializeField] private float d2vStageTimeoutSeconds;
    [Tooltip("Semi-free comparison stage timeout in seconds. Set to 0 until the experiment value is decided.")]
    [Min(0f)]
    [SerializeField] private float semiFreeStageTimeoutSeconds;
    [Tooltip("Final candidate confirmation stage timeout in seconds. Set to 0 until the experiment value is decided.")]
    [Min(0f)]
    [SerializeField] private float finalizeStageTimeoutSeconds;
    [Min(1f)]
    [SerializeField] private float practiceScrollThresholdPixels = 80f;
    [Min(0.1f)]
    [SerializeField] private float telemetryRefreshSeconds = 0.75f;

    [Header("Task Start Gate")]
    [SerializeField] private bool requireStartButtonBeforeSession = true;
    [SerializeField] private string startGateDisplayId = "Display_B_Back";
    [SerializeField] private Vector2 startButtonNormalizedPosition = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 startButtonNormalizedSize = new Vector2(0.28f, 0.14f);
    [SerializeField] private float startCountdownSeconds = 3f;
    [SerializeField] private Color startGateButtonColor = new Color(0.502f, 0.812f, 1f, 1f);
    [SerializeField] private Color startGateCountdownColor = new Color(0.125f, 0.125f, 0.125f, 1f);

    [Header("T2 Instruction Panel")]
    [Tooltip("Shows the current T2 instruction above Display_B_Back.")]
    [FormerlySerializedAs("showInstructionPanelBetweenDisplays")]
    [SerializeField] private bool showInstructionPanelAboveBackDisplay = true;
    [SerializeField] private Vector2 instructionPanelSizePixels = new Vector2(1600f, 180f);
    [SerializeField] private float instructionPanelScale = 0.0009f;
    [Min(8)]
    [SerializeField] private int instructionPanelPhaseFontSize = 30;
    [Min(8)]
    [SerializeField] private int instructionPanelMessageFontSize = 42;
    [Tooltip("Space between the upper edge of Display_B_Back and the lower edge of the instruction panel.")]
    [SerializeField] private float instructionPanelVerticalGapMeters = 0.01f;
    [Tooltip("Moves the panel along Display_B_Back's forward axis. Keep this near zero unless z-fighting appears.")]
    [SerializeField] private float instructionPanelDepthOffsetMeters;
    [SerializeField] private int instructionPanelSortingOrder = 70;
    [SerializeField] private Color instructionPanelColor = new Color(0.035f, 0.075f, 0.13f, 0.96f);
    [SerializeField] private Color instructionReminderColor = new Color(0.34f, 0.16f, 0.025f, 0.98f);
    [SerializeField] private Color instructionCompleteColor = new Color(0.035f, 0.24f, 0.13f, 0.98f);
    [Tooltip("Briefly pulses the instruction panel when the displayed instruction changes. The first instruction is not emphasized.")]
    [SerializeField] private bool emphasizeInstructionChanges = true;
    [Min(0.1f)]
    [SerializeField] private float instructionChangeAttentionSeconds = 3f;
    [Min(0.25f)]
    [SerializeField] private float instructionChangePulseFrequency = 2.5f;
    [Range(1f, 1.2f)]
    [SerializeField] private float instructionChangeScaleMultiplier = 1.04f;
    [SerializeField] private Color instructionChangeAttentionColor = new Color(0.08f, 0.52f, 0.92f, 0.99f);

    private InteractionCondition selectedCondition = InteractionCondition.RaycastBaseline;
    private DisplayLayoutPreset selectedLayout = DisplayLayoutPreset.UpDownDepth;
    private InteractionCondition activeCondition = InteractionCondition.RaycastBaseline;
    private WebViewSessionPhase currentPhase = WebViewSessionPhase.ConditionSelection;
    private TaskStartGate startGate;
    private TLabWebViewDisplayBridge activeWebViewBridge;
    private TLabWebViewDisplayBridge activeSecondaryWebViewBridge;
    private string activeYoutubeUrl;
    private string activeComparisonPageUrl;
    private bool taskActive;
    private T2TrainingStep trainingStep;
    private float taskStartTime;
    private float taskEndTime;
    private float nextTelemetryRefreshTime;
    private float currentStageStartTime;
    private T2MainStage mainStage;
    private bool awaitingPostTimeoutSelection;
    private bool awaitingPostTaskReset;
    private bool resultWritten;
    private string selectedCandidate = string.Empty;
    private string postTimeoutFinalSelection = string.Empty;
    private float postTimeoutSelectionTime = -1f;
    private string pendingResult = string.Empty;
    private string resultNote = string.Empty;
    private int youtubeOperationCount;
    private int youtubePauseCount;
    private int youtubePlayCount;
    private int youtubeSeekCount;
    private float youtubeTotalPauseTime;
    private float youtubePauseStartedAt = -1f;
    private float webScrollAmount;
    private float practiceStepScrollAmount;
    private int completedPracticeRounds;
    private int completedPracticeStepCount;
    private int webClickCount;
    private int detailPageOpenCount;
    private int candidateAddCount;
    private int candidateRemoveCount;
    private float v2dCompletionTime = -1f;
    private float d2vCompletionTime = -1f;
    private bool v2dTargetDetailOpened;
    private bool d2vTargetDetailOpened;
    private bool practiceCandidatesReset;
    private bool practiceCompletionLogged;
    private bool candidateConfirmationUnlockLogged;
    private bool candidateListOpenedAtUnlock;
    private int displaySwitchCount;
    private int clutchCount;
    private float controllerMovementAmount;
    private float controllerRotationAmount;
    private string lastInputDisplayId = string.Empty;
    private bool hasControllerSample;
    private Vector3 lastControllerPosition;
    private Quaternion lastControllerRotation;
    private StreamWriter t2EventWriter;
    private StreamWriter t2ResultWriter;
    private StreamWriter t2PostTimeoutSelectionWriter;
    private string t2PostTimeoutSelectionLogPath;
    private RectTransform instructionPanelRoot;
    private Image instructionPanelBackground;
    private Text instructionPanelPhaseText;
    private Text instructionPanelMessageText;
    private Font instructionPanelFont;
    private bool hasInstructionPanelPose;
    private Vector3 lastInstructionDisplayAPosition;
    private Quaternion lastInstructionDisplayARotation;
    private Vector3 lastInstructionDisplayBPosition;
    private Quaternion lastInstructionDisplayBRotation;
    private string lastInstructionSignature = string.Empty;
    private float instructionChangeAttentionStartedAt = -1f;
    private Color instructionPanelBaseColor;
    private SyncMarkerController syncMarkerController;
    private bool startSyncMarkerLogged;
    private bool endSyncMarkerLogged;

    public WebViewSessionPhase CurrentPhase => currentPhase;
    public bool IsSessionRunning => currentPhase != WebViewSessionPhase.ConditionSelection;
    public string ParticipantId => participantId;
    public string SessionId => sessionId;
    public string RunId => runId;
    public string InitialUrl => initialUrl;
    public string SecondaryInitialUrl => secondaryInitialUrl;
    public WebViewInputMode InputMode => inputMode;
    public bool UsesDirectScrollAndSeek => inputMode == WebViewInputMode.DirectScrollAndSeek;
    public bool UsesStickTouchGesture => inputMode == WebViewInputMode.StickTouchGesture;
    public string TargetDisplayId => targetDisplayId;
    public T2ContentSet SelectedContentSet => selectedContentSet;
    public bool IsTaskActive => taskActive;
    public string SelectedCandidate => selectedCandidate;
    public int YouTubeOperationCount => youtubeOperationCount;
    public int CompletedPracticeRounds => completedPracticeRounds;
    public int CompletedPracticeStepCount => completedPracticeStepCount;
    public int AdditionalPracticeStepCount => Mathf.Max(
        0,
        completedPracticeStepCount - MinimumPracticeRounds * PracticeStepsPerRound);
    public int MinimumPracticeRounds => Mathf.Max(1, minimumPracticeRounds);
    public bool CanEndTraining => currentPhase == WebViewSessionPhase.Training
        && taskActive
        && completedPracticeRounds >= MinimumPracticeRounds;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (taskActive)
        {
            RefreshWebTelemetryIfNeeded();
            if (taskStartTime <= 0f && AreWebViewsReadyForTask())
            {
                BeginTaskMeasurement();
            }

            if (taskStartTime > 0f)
            {
                TrackSessionMetrics();
                UpdateTaskTiming();
            }
        }
        else if (awaitingPostTimeoutSelection)
        {
            RefreshWebTelemetryIfNeeded();
            TryOpenCandidateListAtUnlock();
        }

        if (!IsSessionRunning || inputManager == null || !inputManager.SecondaryButtonPressed)
        {
            return;
        }

        TLabWebViewDisplayBridge bridge = ResolveNavigationWebViewBridge(out string displayId);
        if (bridge == null || !bridge.IsReady || !bridge.TryGoBack())
        {
            return;
        }

        logger?.LogEvent("T2Web_GoBack", activeCondition, displayId, Vector2.zero);
        Debug.Log($"[WebViewSession] go back display={displayId}");
    }

    private void LateUpdate()
    {
        if (IsSessionRunning && instructionPanelRoot != null && instructionPanelRoot.gameObject.activeSelf)
        {
            UpdateInstructionPanelPoseIfNeeded();
            UpdateInstructionChangeAttention();
        }
    }

    private void OnDisable()
    {
        HideStartGate();
        StopInstructionChangeAttention();
        SetInstructionPanelVisible(false);
        if (disableWebViewOnReturn)
        {
            DisableSessionWebViews();
        }

        CloseT2LogWriters();
    }

    public void SetSelectedCondition(InteractionCondition condition)
    {
        if (!IsSessionRunning)
        {
            selectedCondition = condition;
        }
    }

    public void SetSelectedLayout(DisplayLayoutPreset layout)
    {
        if (!IsSessionRunning)
        {
            selectedLayout = layout;
        }
    }

    public void SetSelectedContentSet(T2ContentSet contentSet)
    {
        if (!IsSessionRunning)
        {
            selectedContentSet = T2ContentSet.ACampGear2024;
        }
    }

    public void SetParticipantContext(
        string newParticipantId,
        string newSessionId,
        string newConditionOrder,
        string newRunId = "")
    {
        if (IsSessionRunning)
        {
            return;
        }

        participantId = NormalizeId(newParticipantId, "P000");
        sessionId = NormalizeId(newSessionId, "S000");
        conditionOrder = NormalizeId(newConditionOrder, "G1");
        runId = NormalizeId(newRunId, sessionId);
    }

    public void SetInitialUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        initialUrl = url.Trim();
        ResolveTargetWebViewBridge()?.LoadUrl(initialUrl);
    }

    public void SetSecondaryInitialUrl(string url)
    {
        secondaryInitialUrl = string.IsNullOrWhiteSpace(url) ? "" : url.Trim();
        TLabWebViewDisplayBridge bridge = ResolveSecondaryWebViewBridge();
        string resolvedUrl = ResolveSecondaryInitialUrl(bridge);
        if (bridge != null && !string.IsNullOrWhiteSpace(resolvedUrl))
        {
            bridge.LoadUrl(resolvedUrl);
        }
    }

    public void BeginTrainingTask()
    {
        BeginSession(WebViewSessionPhase.Training);
    }

    public void BeginMainTask()
    {
        BeginSession(WebViewSessionPhase.MainTask);
    }

    public bool TryCompleteTraining()
    {
        if (!CanEndTraining)
        {
            WriteT2Event(
                "practice_end_blocked",
                secondaryDisplayId,
                completedPracticeRounds.ToString(CultureInfo.InvariantCulture),
                $"minimum_rounds={MinimumPracticeRounds};completed_steps={completedPracticeStepCount}");
            return false;
        }

        trainingStep = T2TrainingStep.Completed;
        WriteT2Event(
            "practice_readiness_confirmed",
            secondaryDisplayId,
            completedPracticeRounds.ToString(CultureInfo.InvariantCulture),
            $"participant_ready;experimenter_confirmed;completed_steps={completedPracticeStepCount};"
            + $"additional_steps={AdditionalPracticeStepCount}");
        MarkPracticeCompleted();
        ApplyCurrentPageInstruction();
        return true;
    }

    public void ReturnToConditionSelection()
    {
        if (currentPhase == WebViewSessionPhase.ConditionSelection)
        {
            return;
        }

        WebViewSessionPhase endedPhase = currentPhase;
        if (taskActive || awaitingPostTimeoutSelection)
        {
            taskEndTime = taskEndTime > 0f ? taskEndTime : Time.time;
            taskStartTime = taskStartTime > 0f ? taskStartTime : taskEndTime;
            string result = !string.IsNullOrEmpty(pendingResult)
                ? pendingResult
                : endedPhase == WebViewSessionPhase.Training && trainingStep == T2TrainingStep.Completed
                    ? "completed"
                    : "fail";
            FinalizeSessionResult(endedPhase, result);
        }

        taskActive = false;
        awaitingPostTimeoutSelection = false;
        awaitingPostTaskReset = false;
        currentPhase = WebViewSessionPhase.ConditionSelection;
        HideStartGate();
        StopInstructionChangeAttention();
        lastInstructionSignature = string.Empty;
        SetInstructionPanelVisible(false);

        if (disableWebViewOnReturn)
        {
            DisableSessionWebViews();
        }

        if (inputManager != null)
        {
            inputManager.UnlockCondition();
        }

        if (displayManager != null)
        {
            displayManager.SetAllDisplayContentMode(DisplayContentMode.ConditionSelection);
        }

        logger?.LogEvent(
            endedPhase == WebViewSessionPhase.Training ? "T2Web_TrainingReturn" : "T2Web_MainReturn",
            activeCondition,
            targetDisplayId,
            Vector2.zero);
        logger?.EndRunContext();

        Debug.Log($"[WebViewSession] returned phase={endedPhase}, condition={activeCondition}");
    }

    public void RecordWebViewClick(string displayId, Vector2 normalizedPosition)
    {
        if (!taskActive)
        {
            return;
        }

        if (string.Equals(displayId, secondaryDisplayId, StringComparison.Ordinal))
        {
            webClickCount++;
        }

        WriteT2Event("web_click", displayId, webClickCount.ToString(CultureInfo.InvariantCulture),
            $"{normalizedPosition.x:0.000}:{normalizedPosition.y:0.000}");
    }

    public void OnWebViewTaskMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        string[] fields = message.Split(new[] { '|' }, 4);
        if (fields.Length < 3 || !string.Equals(fields[0], "t2", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string role = fields[1];
        string eventType = fields[2];
        string value = fields.Length >= 4 ? fields[3] : string.Empty;
        if (awaitingPostTaskReset)
        {
            if (string.Equals(role, "web", StringComparison.OrdinalIgnoreCase)
                && string.Equals(eventType, "action", StringComparison.OrdinalIgnoreCase)
                && string.Equals(value, "reset_candidates_and_home", StringComparison.Ordinal))
            {
                logger?.LogEvent("T2Web_ResetCandidatesAndHome", activeCondition, secondaryDisplayId, Vector2.zero);
                awaitingPostTaskReset = false;
                ReturnToConditionSelection();
            }
            return;
        }

        if (awaitingPostTimeoutSelection)
        {
            if (string.Equals(role, "web", StringComparison.OrdinalIgnoreCase)
                && string.Equals(eventType, "candidate", StringComparison.OrdinalIgnoreCase))
            {
                HandleCandidateSelection(value);
            }
            return;
        }

        if (!taskActive)
        {
            return;
        }

        if (taskStartTime <= 0f)
        {
            BeginTaskMeasurement();
        }

        if (string.Equals(role, "youtube", StringComparison.OrdinalIgnoreCase))
        {
            HandleYouTubeTelemetry(eventType, value);
        }
        else if (string.Equals(role, "web", StringComparison.OrdinalIgnoreCase))
        {
            HandleComparisonPageTelemetry(eventType, value);
        }
    }

    public bool TryHandleTaskControlClick(string displayId, Vector2 normalizedPosition)
    {
        return startGate != null && startGate.TryHandleClick(displayId, normalizedPosition);
    }

    private void BeginSession(WebViewSessionPhase phase)
    {
        ResolveReferences();
        activeWebViewBridge = ResolveTargetWebViewBridge();
        activeSecondaryWebViewBridge = ResolveSecondaryWebViewBridge();
        if (inputManager == null || activeWebViewBridge == null)
        {
            Debug.LogError("[WebViewSession] requires PrototypeInputManager and a target WebView bridge.");
            return;
        }

        if (enableSecondaryWebView && activeSecondaryWebViewBridge == null)
        {
            Debug.LogWarning($"[WebViewSession] secondary WebView is enabled but no bridge was found on {secondaryDisplayId}.");
        }

        activeCondition = selectedCondition;
        currentPhase = phase;
        taskActive = false;
        StopInstructionChangeAttention();
        lastInstructionSignature = string.Empty;
        startSyncMarkerLogged = false;
        endSyncMarkerLogged = false;
        syncMarkerController = SyncMarkerController.GetOrCreate();
        ResetSessionMetrics();
        inputManager.LockCondition(activeCondition);
        selectedLayout = DisplayLayoutPreset.UpDownDepth;
        experimentManager?.ApplyLayout(DisplayLayoutPreset.UpDownDepth);
        displayManager?.SetAllDisplayContentMode(DisplayContentMode.PointingTask);
        ApplyCurrentPageInstruction();

        if (disableOtherWebViewsOnStart)
        {
            DisableNonSessionWebViews(activeWebViewBridge, activeSecondaryWebViewBridge);
        }

        PrepareSessionUrls();
        LoadSessionUrls();

        logger?.LogEvent(
            phase == WebViewSessionPhase.Training ? "T2Web_TrainingBegin" : "T2Web_MainBegin",
            activeCondition,
            targetDisplayId,
            Vector2.zero);

        if (requireStartButtonBeforeSession)
        {
            ShowStartGate();
            return;
        }

        StartWebViewSession();
    }

    private void StartWebViewSession()
    {
        HideStartGate();
        ResolveReferences();
        DisplaySurface targetDisplay = FindTargetDisplay();
        if (targetDisplay != null)
        {
            targetDisplay.SetContentMode(DisplayContentMode.PointingTask);
            targetDisplay.ResetScroll();
        }

        DisplaySurface secondaryDisplay = FindDisplay(secondaryDisplayId);
        if (enableSecondaryWebView && secondaryDisplay != null && secondaryDisplay != targetDisplay)
        {
            secondaryDisplay.SetContentMode(DisplayContentMode.PointingTask);
            secondaryDisplay.ResetScroll();
        }

        if (focusTargetDisplayOnStart && targetDisplay != null && displayManager != null)
        {
            displayManager.SetFocusedDisplay(targetDisplay);
            virtualCursorController?.WarpTo(targetDisplay, initialCursorNormalizedPosition);
        }

        activeWebViewBridge = ResolveTargetWebViewBridge();
        activeSecondaryWebViewBridge = ResolveSecondaryWebViewBridge();
        if (activeWebViewBridge == null)
        {
            Debug.LogError("[WebViewSession] target WebView bridge was lost before session start.");
            ReturnToConditionSelection();
            return;
        }

        LoadSessionUrls();
        activeWebViewBridge.EnableWebView();
        if (activeSecondaryWebViewBridge != null
            && !ReferenceEquals(activeSecondaryWebViewBridge, activeWebViewBridge))
        {
            activeSecondaryWebViewBridge.EnableWebView();
        }

        taskActive = true;
        nextTelemetryRefreshTime = Time.time;
        ApplyCurrentPageInstruction();
        EnsureT2LogWriters();
        if (Application.isEditor)
        {
            BeginTaskMeasurement();
        }

        logger?.LogEvent("T2Web_WebViewStart", activeCondition, targetDisplayId, initialCursorNormalizedPosition);
        Debug.Log(
            $"[WebViewSession] WebView start phase={currentPhase}, primary={targetDisplayId}, url={activeYoutubeUrl}, "
            + $"secondary={(activeSecondaryWebViewBridge != null ? secondaryDisplayId : "None")}, "
            + $"secondaryUrl={ResolveSecondaryInitialUrl(activeSecondaryWebViewBridge)}");
    }

    private bool AreWebViewsReadyForTask()
    {
        return activeWebViewBridge != null
            && activeWebViewBridge.IsReady
            && (!enableSecondaryWebView
                || activeSecondaryWebViewBridge == null
                || activeSecondaryWebViewBridge.IsReady);
    }

    private void BeginTaskMeasurement()
    {
        if (taskStartTime > 0f)
        {
            return;
        }

        taskStartTime = Time.time;
        currentStageStartTime = taskStartTime;
        EmitStartSyncMarkerIfNeeded();
        WriteT2Event("task_start", targetDisplayId, selectedContentSet.ToString(),
            currentPhase == WebViewSessionPhase.Training ? "practice" : "main");
        if (currentPhase == WebViewSessionPhase.MainTask)
        {
            WriteT2Event(
                "main_start",
                targetDisplayId,
                selectedContentSet.ToString(),
                "main_content_loaded;start_gate_completed");
            logger?.LogEvent("T2Web_MainBegin", activeCondition, targetDisplayId, Vector2.zero);
        }
    }

    private void ShowStartGate()
    {
        DisplaySurface display = FindDisplay(startGateDisplayId);
        startGate = TaskStartGate.GetOrCreate(display);
        if (startGate == null)
        {
            Debug.LogWarning("[WebViewSession] Start gate display was not found. Starting WebView without start gate.");
            StartWebViewSession();
            return;
        }

        startGate.Show(
            display,
            startButtonNormalizedPosition,
            startButtonNormalizedSize,
            startCountdownSeconds,
            startGateButtonColor,
            startGateCountdownColor,
            StartWebViewSession,
            "[WebViewSession]");

        Debug.Log($"[WebViewSession] waiting for start button phase={currentPhase}, display={startGateDisplayId}");
    }

    private void HideStartGate()
    {
        if (startGate != null)
        {
            startGate.Hide();
        }

        startGate = null;
    }

    private void LoadSessionUrls()
    {
        string youtubeUrl = !string.IsNullOrWhiteSpace(activeYoutubeUrl)
            ? activeYoutubeUrl
            : initialUrl;
        activeWebViewBridge?.LoadUrl(youtubeUrl);

        string resolvedSecondaryUrl = ResolveSecondaryInitialUrl(activeSecondaryWebViewBridge);
        if (activeSecondaryWebViewBridge != null
            && !ReferenceEquals(activeSecondaryWebViewBridge, activeWebViewBridge)
            && !string.IsNullOrWhiteSpace(resolvedSecondaryUrl))
        {
            activeSecondaryWebViewBridge.LoadUrl(resolvedSecondaryUrl);
        }
    }

    private string ResolveSecondaryInitialUrl(TLabWebViewDisplayBridge bridge)
    {
        return !string.IsNullOrWhiteSpace(activeComparisonPageUrl)
            ? activeComparisonPageUrl
            : !string.IsNullOrWhiteSpace(secondaryInitialUrl)
            ? secondaryInitialUrl.Trim()
            : bridge?.InitialUrl ?? "";
    }

    private void PrepareSessionUrls()
    {
        selectedContentSet = T2ContentSet.ACampGear2024;
        activeYoutubeUrl = currentPhase == WebViewSessionPhase.Training
            ? practiceYoutubeUrl
            : youtubeUrlSetA;
        if (string.IsNullOrWhiteSpace(activeYoutubeUrl))
        {
            activeYoutubeUrl = initialUrl;
        }

        if (currentPhase == WebViewSessionPhase.Training
            && !string.IsNullOrWhiteSpace(practiceComparisonInitialUrl))
        {
            activeComparisonPageUrl = practiceComparisonInitialUrl.Trim();
            Debug.Log(
                $"[WebViewSession] Using separate practice content youtube={activeYoutubeUrl}, "
                + $"comparison={activeComparisonPageUrl}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(secondaryInitialUrl))
        {
            activeComparisonPageUrl = secondaryInitialUrl.Trim();
            Debug.Log($"[WebViewSession] Using comparison Web server url={activeComparisonPageUrl}");
            return;
        }

        activeComparisonPageUrl = BuildLocalComparisonPageUrl(comparisonPageResourceSetA);
        if (string.IsNullOrWhiteSpace(activeComparisonPageUrl))
        {
            activeComparisonPageUrl = "about:blank";
            Debug.LogError("[WebViewSession] T2 comparison Web URL and packaged page are both unavailable.");
        }
    }

    private string BuildLocalComparisonPageUrl(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            return string.Empty;
        }

        TextAsset page = Resources.Load<TextAsset>(resourcePath.Trim());
        if (page == null)
        {
            Debug.LogError($"[WebViewSession] T2 comparison page resource not found: {resourcePath}");
            return string.Empty;
        }

        try
        {
            string directory = Path.Combine(Application.persistentDataPath, "T2WebContent");
            Directory.CreateDirectory(directory);
            const string fileName = "t2_content_2024.html";
            string path = Path.Combine(directory, fileName);
            File.WriteAllText(path, page.text);
            string localUrl = new Uri(path).AbsoluteUri;
            Debug.Log(
                $"[WebViewSession] Prepared packaged T2 comparison page url={localUrl}, "
                + $"bytes={new FileInfo(path).Length}");
            return localUrl;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[WebViewSession] Could not prepare T2 comparison page: {exception.Message}");
            return string.Empty;
        }
    }

    private void ResetSessionMetrics()
    {
        candidateConfirmationUnlockLogged = false;
        candidateListOpenedAtUnlock = false;

        taskStartTime = 0f;
        taskEndTime = 0f;
        nextTelemetryRefreshTime = 0f;
        currentStageStartTime = 0f;
        mainStage = T2MainStage.V2D;
        awaitingPostTimeoutSelection = false;
        awaitingPostTaskReset = false;
        resultWritten = false;
        selectedCandidate = string.Empty;
        postTimeoutFinalSelection = string.Empty;
        postTimeoutSelectionTime = -1f;
        pendingResult = string.Empty;
        resultNote = string.Empty;
        youtubeOperationCount = 0;
        youtubePauseCount = 0;
        youtubePlayCount = 0;
        youtubeSeekCount = 0;
        youtubeTotalPauseTime = 0f;
        youtubePauseStartedAt = -1f;
        webScrollAmount = 0f;
        practiceStepScrollAmount = 0f;
        if (currentPhase == WebViewSessionPhase.Training)
        {
            completedPracticeRounds = 0;
            completedPracticeStepCount = 0;
        }
        webClickCount = 0;
        detailPageOpenCount = 0;
        candidateAddCount = 0;
        candidateRemoveCount = 0;
        v2dCompletionTime = -1f;
        d2vCompletionTime = -1f;
        v2dTargetDetailOpened = false;
        d2vTargetDetailOpened = false;
        practiceCandidatesReset = false;
        practiceCompletionLogged = false;
        displaySwitchCount = 0;
        clutchCount = 0;
        controllerMovementAmount = 0f;
        controllerRotationAmount = 0f;
        lastInputDisplayId = string.Empty;
        hasControllerSample = false;
        trainingStep = currentPhase == WebViewSessionPhase.Training
            ? T2TrainingStep.PauseYouTube
            : T2TrainingStep.Completed;
    }

    private void TrackSessionMetrics()
    {
        if (inputManager != null
            && activeCondition == InteractionCondition.ExplicitDisplayFocus
            && inputManager.GripPressed)
        {
            clutchCount++;
            WriteT2Event("clutch", displayManager?.FocusedDisplay != null
                ? displayManager.FocusedDisplay.name
                : "None", clutchCount.ToString(CultureInfo.InvariantCulture), string.Empty);
        }

        DisplaySurface inputDisplay = null;
        if (activeCondition == InteractionCondition.ExplicitDisplayFocus)
        {
            inputDisplay = displayManager != null ? displayManager.FocusedDisplay : null;
        }
        else if (displayManager != null && displayManager.HasCurrentRaycastHit)
        {
            inputDisplay = displayManager.CurrentRaycastHit.Display;
        }

        if (inputDisplay != null)
        {
            string inputDisplayId = inputDisplay.name;
            if (!string.IsNullOrEmpty(lastInputDisplayId)
                && !string.Equals(lastInputDisplayId, inputDisplayId, StringComparison.Ordinal))
            {
                displaySwitchCount++;
                WriteT2Event("display_switch", inputDisplayId,
                    displaySwitchCount.ToString(CultureInfo.InvariantCulture), lastInputDisplayId);
            }

            lastInputDisplayId = inputDisplayId;
        }

        Transform controllerTransform = raycastPointer != null
            ? raycastPointer.RightControllerTransform
            : null;
        if (controllerTransform == null)
        {
            return;
        }

        Vector3 position = controllerTransform.position;
        Quaternion rotation = controllerTransform.rotation;
        if (hasControllerSample)
        {
            controllerMovementAmount += Vector3.Distance(lastControllerPosition, position);
            controllerRotationAmount += Quaternion.Angle(lastControllerRotation, rotation);
        }

        lastControllerPosition = position;
        lastControllerRotation = rotation;
        hasControllerSample = true;
    }

    private void UpdateTaskTiming()
    {
        if (!IsSessionRunning || taskStartTime <= 0f)
        {
            return;
        }

        UpdateInstructionCountdown();
        if (currentPhase == WebViewSessionPhase.Training)
        {
            CheckCurrentStageTimeout();
            return;
        }

        float mainElapsed = Mathf.Max(0f, Time.time - taskStartTime);
        if (totalTaskDurationSeconds > 0f && mainElapsed >= totalTaskDurationSeconds)
        {
            BeginPostTimeoutSelection(
                "timeout",
                "main",
                mainElapsed,
                $"main_limit={totalTaskDurationSeconds:0.000}");
            return;
        }

        if (!candidateConfirmationUnlockLogged
            && mainElapsed >= Mathf.Max(0f, candidateConfirmationUnlockSeconds))
        {
            candidateConfirmationUnlockLogged = true;
            SetMainStage(T2MainStage.Finalize);
            WriteT2Event(
                "candidate_confirmation_unlocked",
                secondaryDisplayId,
                mainElapsed.ToString("0.000", CultureInfo.InvariantCulture),
                $"unlock_at={candidateConfirmationUnlockSeconds:0.000}");
            ApplyCurrentPageInstruction();
        }

        if (mainElapsed >= Mathf.Max(0f, candidateConfirmationUnlockSeconds))
        {
            TryOpenCandidateListAtUnlock();
        }

        if (currentPhase == WebViewSessionPhase.MainTask && taskStartTime > 0f)
        {
            UpdateMainStage();
        }

        CheckCurrentStageTimeout();
    }

    private void BeginPostTimeoutSelection(string eventType, string stageLabel, float elapsed, string details)
    {
        if (awaitingPostTimeoutSelection)
        {
            return;
        }

        pendingResult = "timeout";
        resultNote = $"stage={stageLabel};elapsed={elapsed:0.000};{details}";
        taskEndTime = Time.time;
        EndYouTubePause(taskEndTime);
        mainStage = T2MainStage.Finalize;
        currentStageStartTime = 0f;
        taskActive = false;
        awaitingPostTimeoutSelection = true;
        WriteT2Event(eventType, secondaryDisplayId,
            elapsed.ToString("0.000", CultureInfo.InvariantCulture), resultNote);
        EmitEndSyncMarkerIfNeeded("main_timeout");
        FinalizeSessionResult(currentPhase, pendingResult, true);
        TryOpenCandidateListAtUnlock();
        ApplyCurrentPageInstruction();
        logger?.LogEvent("T2Web_MainTimeout", activeCondition, secondaryDisplayId, Vector2.zero);
    }

    private void CheckCurrentStageTimeout()
    {
        float stageTimeout = GetCurrentStageTimeoutSeconds();
        float stageElapsed = Mathf.Max(0f, Time.time - currentStageStartTime);
        if (stageTimeout > 0f && currentStageStartTime > 0f && stageElapsed >= stageTimeout)
        {
            EndForTimeout(
                "stage_timeout",
                GetCurrentStageLabel(),
                stageElapsed,
                $"stage_limit={stageTimeout:0.000}");
        }
    }

    private void UpdateMainStage()
    {
        switch (mainStage)
        {
            case T2MainStage.V2D:
                if (v2dCompletionTime >= 0f)
                {
                    SetMainStage(T2MainStage.D2V);
                }
                return;
            case T2MainStage.D2V:
                if (d2vCompletionTime >= 0f)
                {
                    SetMainStage(T2MainStage.SemiFree);
                }
                return;
        }
    }

    private void SetMainStage(T2MainStage nextStage)
    {
        if (nextStage == mainStage)
        {
            return;
        }

        mainStage = nextStage;
        currentStageStartTime = Time.time;
        float mainElapsed = Mathf.Max(0f, Time.time - taskStartTime);
        WriteT2Event("main_stage", secondaryDisplayId, mainStage.ToString(), mainElapsed.ToString("0.000", CultureInfo.InvariantCulture));
        ApplyCurrentPageInstruction();
    }

    private float GetCurrentStageTimeoutSeconds()
    {
        if (currentPhase == WebViewSessionPhase.Training)
        {
            return practiceStageTimeoutSeconds;
        }

        switch (mainStage)
        {
            case T2MainStage.V2D:
                return v2dStageTimeoutSeconds;
            case T2MainStage.D2V:
                return d2vStageTimeoutSeconds;
            case T2MainStage.SemiFree:
                return semiFreeStageTimeoutSeconds;
            case T2MainStage.Finalize:
                return finalizeStageTimeoutSeconds;
            default:
                return 0f;
        }
    }

    private string GetCurrentStageLabel()
    {
        return currentPhase == WebViewSessionPhase.Training
            ? "Practice"
            : mainStage.ToString();
    }

    private void EndForTimeout(string eventType, string stageLabel, float elapsed, string details)
    {
        pendingResult = "timeout";
        resultNote = $"stage={stageLabel};elapsed={elapsed:0.000};{details}";
        taskEndTime = Time.time;
        WriteT2Event(eventType, secondaryDisplayId,
            elapsed.ToString("0.000", CultureInfo.InvariantCulture), resultNote);
        ReturnToConditionSelection();
    }

    private void RefreshWebTelemetryIfNeeded()
    {
        if (Time.time < nextTelemetryRefreshTime)
        {
            return;
        }

        nextTelemetryRefreshTime = Time.time + Mathf.Max(0.1f, telemetryRefreshSeconds);
        activeWebViewBridge?.TryEvaluateJavaScript(BuildTelemetryBridgeJavaScript("youtube"));
        activeSecondaryWebViewBridge?.TryEvaluateJavaScript(BuildTelemetryBridgeJavaScript("web"));
        if (currentPhase == WebViewSessionPhase.Training
            && !practiceCandidatesReset
            && activeSecondaryWebViewBridge != null
            && activeSecondaryWebViewBridge.IsReady)
        {
            activeSecondaryWebViewBridge.TryEvaluateJavaScript(
                "window.t2ResetCandidates&&window.t2ResetCandidates();");
            practiceCandidatesReset = true;
        }
        ApplyCurrentPageInstruction();
    }

    private string BuildTelemetryBridgeJavaScript(string role)
    {
        string targetObject = EscapeJavaScriptString(gameObject.name);
        string targetMethod = nameof(OnWebViewTaskMessage);
        string safeRole = EscapeJavaScriptString(role);
        string forceYoutubeUnmutedValue = forceYoutubeUnmuted ? "true" : "false";
        string youtubeForcedVolumeValue = Mathf.Clamp01(youtubeForcedVolume).ToString("0.###", CultureInfo.InvariantCulture);
        string script =
            "(function(){try{" +
            "if(window.__t2UnityBridgeRole==='" + safeRole + "'){return;}" +
            "window.__t2UnityBridgeRole='" + safeRole + "';" +
            "var go='" + targetObject + "',method='" + targetMethod + "',role='" + safeRole + "';" +
            "function send(type,value){" +
            "var clean=String(value==null?'':value).replace(/\\|/g,'/');" +
            "var payload='t2|'+role+'|'+type+'|'+clean;" +
            "if(window.tlab&&window.tlab.unitySendMessage){window.tlab.unitySendMessage(go,method,payload);}" +
            "}" +
            "window.__t2UnitySend=send;";

        if (string.Equals(role, "youtube", StringComparison.Ordinal))
        {
            script +=
                "var forceUnmute=" + forceYoutubeUnmutedValue + ",targetVolume=" + youtubeForcedVolumeValue + ";" +
                "function ensureAudible(v){if(!forceUnmute||!v){return;}try{if(v.muted){v.muted=false;}if(typeof v.volume==='number'&&Math.abs(v.volume-targetVolume)>0.01){v.volume=targetVolume;}}catch(e){}}" +
                "function bindVideo(v){if(!v||v.__t2UnityBound){return;}v.__t2UnityBound=true;ensureAudible(v);" +
                "var lastStableTime=Number(v.currentTime||0),seekOrigin=lastStableTime;" +
                "v.addEventListener('timeupdate',function(){if(!v.seeking){lastStableTime=Number(v.currentTime||0);}},true);" +
                "v.addEventListener('seeking',function(){seekOrigin=lastStableTime;},true);" +
                "v.addEventListener('play',function(){ensureAudible(v);send('play','');},true);" +
                "v.addEventListener('pause',function(){if(!v.ended){send('pause',String(v.currentTime||0));}},true);" +
                "v.addEventListener('volumechange',function(){if(forceUnmute&&(v.muted||v.volume<targetVolume-0.01)){setTimeout(function(){ensureAudible(v);},0);}},true);" +
                "v.addEventListener('seeked',function(){var now=Number(v.currentTime||0),delta=now-seekOrigin;" +
                "send('seek',String(now)+','+String(delta));if(v.paused&&!v.ended){send('paused_seek',String(now));}" +
                "lastStableTime=now;seekOrigin=now;},true);}" +
                "function scan(){var videos=document.querySelectorAll('video');for(var i=0;i<videos.length;i++){bindVideo(videos[i]);}}" +
                "scan();new MutationObserver(scan).observe(document.documentElement||document.body,{childList:true,subtree:true});";
        }
        else
        {
            script +=
            "document.addEventListener('click',function(e){" +
            "var c=e.target&&e.target.closest?e.target.closest('[data-t2-candidate]'):null;" +
            "if(c){send('candidate',c.getAttribute('data-t2-candidate')||'');}" +
            "var a=e.target&&e.target.closest?e.target.closest('[data-t2-action]'):null;" +
            "if(a){send('action',a.getAttribute('data-t2-action')||'');}" +
            "},true);" +
            "var lastY=window.scrollY||0,scrollTimer=0;" +
            "window.addEventListener('scroll',function(){if(scrollTimer){return;}scrollTimer=setTimeout(function(){" +
            "scrollTimer=0;var y=window.scrollY||0,delta=y-lastY;lastY=y;if(delta!==0){send('scroll',String(delta));}" +
            "},120);},{passive:true});";
        }

        return script + "}catch(e){}})();";
    }

    private void ApplyCurrentPageInstruction()
    {
        ResolveCurrentInstruction(out string message, out string state);
        UpdateInstructionPanel(message, state);

        if (activeSecondaryWebViewBridge == null || !activeSecondaryWebViewBridge.IsReady)
        {
            return;
        }

        string javaScript =
            "window.t2SetInstruction&&window.t2SetInstruction('"
            + EscapeJavaScriptString(message) + "','" + state + "');";
        bool confirmationLocked = IsCandidateConfirmationLocked(out float unlockRemainingSeconds);
        javaScript +=
            "window.t2SetConfirmationLock&&window.t2SetConfirmationLock("
            + (confirmationLocked ? "true" : "false") + ","
            + Mathf.CeilToInt(unlockRemainingSeconds).ToString(CultureInfo.InvariantCulture) + ");";
        activeSecondaryWebViewBridge.TryEvaluateJavaScript(javaScript);
    }

    private void ResolveCurrentInstruction(out string message, out string state)
    {
        if (awaitingPostTaskReset)
        {
            message = "選んだ商品を手前Webに表示しています。終了後アンケートに回答し、回答後に「候補リストを削除してホームに戻る」を押してください。";
            state = "complete";
            return;
        }

        if (awaitingPostTimeoutSelection)
        {
            message = "候補リストを確認し、今回のキャンプをより快適にするために最も良さそうな商品を1つ選び、候補を確定してください。";
            state = "reminder";
            return;
        }

        if (currentPhase == WebViewSessionPhase.Training)
        {
            message = GetTrainingInstruction();
            state = trainingStep == T2TrainingStep.Completed ? "complete" : "practice";
            return;
        }

        switch (mainStage)
        {
            case T2MainStage.V2D:
                message = "奥の動画の4分44秒〜6分20秒付近で紹介されている道具を確認してください。手前Webで同じ商品の詳細ページを開き、候補に追加してください。";
                state = "main";
                return;
            case T2MainStage.D2V:
                message = "手前Webの「居住・寝具」カテゴリを開き、2番目の商品詳細を確認してください。奥の動画内で紹介場面を探し、一時停止してください。";
                state = "main";
                return;
            case T2MainStage.Finalize:
                message = IsCandidateConfirmationLocked(out _)
                    ? "候補リストを確認し、今回のキャンプをより快適にするため最も良さそうな商品を1つ選んでください。候補の確定は本番開始11分30秒後です。"
                    : "候補リストを確認し、今回のキャンプをより快適にするために最も良さそうな商品を1つ選び、候補を確定してください。";
                state = "reminder";
                return;
            default:
                message = "動画とWebを自由に見比べ、追加・買い替えの候補として良さそうな動画内の商品を、候補リストに追加してください。候補の数に制限はありません。";
                state = "main";
                return;
        }
    }

    private void UpdateInstructionPanel(string message, string state)
    {
        if (!showInstructionPanelAboveBackDisplay || !IsSessionRunning)
        {
            SetInstructionPanelVisible(false);
            return;
        }

        EnsureInstructionPanel();
        if (instructionPanelRoot == null)
        {
            return;
        }

        instructionPanelPhaseText.text = GetInstructionHeaderLabel(state);
        instructionPanelMessageText.text = message;
        instructionPanelBaseColor = state == "reminder"
            ? instructionReminderColor
            : state == "complete"
                ? instructionCompleteColor
                : instructionPanelColor;
        instructionPanelBackground.color = instructionPanelBaseColor;

        string instructionSignature = state + "\n" + message;
        bool instructionChanged = !string.IsNullOrEmpty(lastInstructionSignature)
            && !string.Equals(lastInstructionSignature, instructionSignature, StringComparison.Ordinal);
        lastInstructionSignature = instructionSignature;
        if (instructionChanged && emphasizeInstructionChanges)
        {
            instructionChangeAttentionStartedAt = Time.unscaledTime;
        }

        SetInstructionPanelVisible(true);
        UpdateInstructionPanelPoseIfNeeded(true);
    }

    private void UpdateInstructionChangeAttention()
    {
        if (instructionChangeAttentionStartedAt < 0f || instructionPanelBackground == null)
        {
            return;
        }

        float duration = Mathf.Max(0.1f, instructionChangeAttentionSeconds);
        float elapsed = Time.unscaledTime - instructionChangeAttentionStartedAt;
        if (elapsed >= duration)
        {
            StopInstructionChangeAttention();
            return;
        }

        float frequency = Mathf.Max(0.25f, instructionChangePulseFrequency);
        float pulse = 0.5f + 0.5f * Mathf.Cos(elapsed * frequency * Mathf.PI * 2f);
        instructionPanelBackground.color = Color.Lerp(instructionPanelBaseColor, instructionChangeAttentionColor, pulse);
        instructionPanelRoot.localScale = Vector3.one * instructionPanelScale
            * Mathf.Lerp(1f, instructionChangeScaleMultiplier, pulse);
    }

    private void StopInstructionChangeAttention()
    {
        instructionChangeAttentionStartedAt = -1f;
        if (instructionPanelBackground != null)
        {
            instructionPanelBackground.color = instructionPanelBaseColor;
        }

        if (instructionPanelRoot != null)
        {
            instructionPanelRoot.localScale = Vector3.one * instructionPanelScale;
        }
    }

    private string GetInstructionHeaderLabel(string state)
    {
        string phaseLabel;
        switch (state)
        {
            case "practice":
                phaseLabel = "PRACTICE";
                break;
            case "complete":
                phaseLabel = "COMPLETE";
                break;
            case "reminder":
                phaseLabel = "REMINDER";
                break;
            default:
                phaseLabel = "MAIN";
                break;
        }

        string header = $"{phaseLabel}  |  {GetMethodLabel()}";
        if (awaitingPostTaskReset || awaitingPostTimeoutSelection || taskStartTime <= 0f)
        {
            return header;
        }

        if (currentPhase == WebViewSessionPhase.Training)
        {
            return header;
        }

        float phaseDurationSeconds = totalTaskDurationSeconds;
        if (phaseDurationSeconds <= 0f)
        {
            return header;
        }

        float remainingSeconds = Mathf.Max(0f, phaseDurationSeconds - (Time.time - taskStartTime));
        return $"{header}  |  {FormatCountdown(remainingSeconds)}";
    }

    private void UpdateInstructionCountdown()
    {
        if (instructionPanelPhaseText == null || !IsSessionRunning)
        {
            return;
        }

        string state = currentPhase == WebViewSessionPhase.Training
            ? (trainingStep == T2TrainingStep.Completed ? "complete" : "practice")
            : mainStage == T2MainStage.Finalize
                ? "reminder"
                : "main";
        instructionPanelPhaseText.text = GetInstructionHeaderLabel(state);
    }

    private static string FormatCountdown(float seconds)
    {
        int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    private void EnsureInstructionPanel()
    {
        if (instructionPanelRoot != null)
        {
            return;
        }

        GameObject root = new GameObject("T2_InstructionPanel", typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(transform, false);
        instructionPanelRoot = root.GetComponent<RectTransform>();
        instructionPanelRoot.sizeDelta = instructionPanelSizePixels;
        instructionPanelRoot.localScale = Vector3.one * instructionPanelScale;

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = hmdCamera;
        canvas.overrideSorting = true;
        canvas.sortingOrder = instructionPanelSortingOrder;

        RectTransform background = CreateInstructionImage(
            instructionPanelRoot,
            "Background",
            Vector2.zero,
            Vector2.one,
            instructionPanelColor);
        instructionPanelBackground = background.GetComponent<Image>();

        instructionPanelPhaseText = CreateInstructionText(
            instructionPanelRoot,
            "Phase",
            new Vector2(0.025f, 0.78f),
            new Vector2(0.975f, 0.96f),
            instructionPanelPhaseFontSize,
            TextAnchor.MiddleLeft);
        instructionPanelPhaseText.fontStyle = FontStyle.Bold;
        instructionPanelPhaseText.color = new Color(0.48f, 0.82f, 1f, 1f);

        instructionPanelMessageText = CreateInstructionText(
            instructionPanelRoot,
            "Message",
            new Vector2(0.025f, 0.08f),
            new Vector2(0.975f, 0.76f),
            instructionPanelMessageFontSize,
            TextAnchor.MiddleLeft);
        instructionPanelMessageText.fontStyle = FontStyle.Bold;

        hasInstructionPanelPose = false;
    }

    private RectTransform CreateInstructionImage(
        Transform parent,
        string objectName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color color)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = child.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private Text CreateInstructionText(
        Transform parent,
        string objectName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        int fontSize,
        TextAnchor alignment)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Text text = child.GetComponent<Text>();
        text.font = ResolveInstructionPanelFont();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;

        Outline outline = child.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.72f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return text;
    }

    private Font ResolveInstructionPanelFont()
    {
        if (instructionPanelFont != null)
        {
            return instructionPanelFont;
        }

        string[] preferredFonts =
        {
            "Noto Sans CJK JP",
            "Noto Sans JP",
            "Noto Sans Japanese",
            "Droid Sans Fallback",
            "Hiragino Sans",
            "Yu Gothic",
            "Arial Unicode MS",
            "sans-serif"
        };
        instructionPanelFont = Font.CreateDynamicFontFromOSFont(preferredFonts, 48);
        if (instructionPanelFont == null)
        {
            instructionPanelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        return instructionPanelFont;
    }

    private void UpdateInstructionPanelPoseIfNeeded(bool force = false)
    {
        ResolveDisplayReferences();
        if (instructionPanelRoot == null || displayB == null)
        {
            return;
        }

        if (!force
            && hasInstructionPanelPose
            && HasSamePose(displayB.transform, lastInstructionDisplayBPosition, lastInstructionDisplayBRotation))
        {
            return;
        }

        Transform backDisplay = displayB.transform;
        float displayHalfHeight = Mathf.Max(0.01f, displayB.PhysicalSizeMeters.y * 0.5f);
        float panelHalfHeight = Mathf.Max(0.001f, instructionPanelSizePixels.y * instructionPanelScale * 0.5f);
        float verticalGap = Mathf.Max(0f, instructionPanelVerticalGapMeters);
        Vector3 panelPosition = backDisplay.position
            + backDisplay.up * (displayHalfHeight + panelHalfHeight + verticalGap)
            + backDisplay.forward * instructionPanelDepthOffsetMeters;

        instructionPanelRoot.SetPositionAndRotation(
            panelPosition,
            backDisplay.rotation);
        instructionPanelRoot.localScale = Vector3.one * instructionPanelScale;

        lastInstructionDisplayAPosition = displayA != null ? displayA.transform.position : Vector3.zero;
        lastInstructionDisplayARotation = displayA != null ? displayA.transform.rotation : Quaternion.identity;
        lastInstructionDisplayBPosition = backDisplay.position;
        lastInstructionDisplayBRotation = backDisplay.rotation;
        hasInstructionPanelPose = true;
    }

    private static bool HasSamePose(Transform target, Vector3 position, Quaternion rotation)
    {
        return (target.position - position).sqrMagnitude < 0.000001f
            && Quaternion.Angle(target.rotation, rotation) < 0.01f;
    }

    private void SetInstructionPanelVisible(bool visible)
    {
        if (instructionPanelRoot != null)
        {
            instructionPanelRoot.gameObject.SetActive(visible);
        }
    }

    private string GetTrainingInstruction()
    {
        int currentRound = completedPracticeRounds + 1;
        string readiness = completedPracticeRounds >= MinimumPracticeRounds
            ? string.Empty
            : $"（最低{MinimumPracticeRounds}周）";
        string roundPrefix = $"{currentRound}周目{readiness}　";
        switch (trainingStep)
        {
            case T2TrainingStep.PauseYouTube:
                return roundPrefix + "P01　奥のYouTube動画を一時停止してください。";
            case T2TrainingStep.PlayYouTube:
                return roundPrefix + "P02　奥のYouTube動画を再生してください。";
            case T2TrainingStep.SkipForwardTenSeconds:
                return roundPrefix + "P03　奥のYouTube動画を10秒スキップしてください。";
            case T2TrainingStep.SeekYouTube:
                return roundPrefix + "P04　奥のYouTube動画のシークバーを使用して戻してください。";
            case T2TrainingStep.ScrollComparisonPage:
                return roundPrefix + "P05　手前のWebページを少し下にスクロールしてください。";
            case T2TrainingStep.OpenProductDetail:
                return roundPrefix + "P06　商品詳細ページを1つ開いてください。";
            case T2TrainingStep.AddCandidate:
                return roundPrefix + "P07　候補に追加ボタンを1回押してください。";
            case T2TrainingStep.RemoveCandidate:
                return roundPrefix + "P08　候補リストから商品を1つ削除してください。";
            default:
                return "練習を終了するとタスク選択メニューへ戻ります。";
        }
    }

    private void HandleYouTubeTelemetry(string eventType, string value)
    {
        switch (eventType)
        {
            case "play":
                youtubeOperationCount++;
                youtubePlayCount++;
                EndYouTubePause(Time.time);
                WriteT2Event(
                    "youtube_play",
                    targetDisplayId,
                    youtubePlayCount.ToString(CultureInfo.InvariantCulture),
                    value);
                if (currentPhase == WebViewSessionPhase.Training
                    && trainingStep == T2TrainingStep.PlayYouTube)
                {
                    AdvanceTrainingStep();
                }
                return;
            case "pause":
                youtubeOperationCount++;
                youtubePauseCount++;
                if (youtubePauseStartedAt < 0f)
                {
                    youtubePauseStartedAt = Time.time;
                }

                WriteT2Event(
                    "youtube_pause",
                    targetDisplayId,
                    youtubePauseCount.ToString(CultureInfo.InvariantCulture),
                    value);
                if (currentPhase == WebViewSessionPhase.Training
                    && trainingStep == T2TrainingStep.PauseYouTube)
                {
                    AdvanceTrainingStep();
                }
                else if (currentPhase == WebViewSessionPhase.MainTask
                    && d2vCompletionTime < 0f)
                {
                    HandleStrictD2VPause(value);
                }
                return;
            case "seek":
                youtubeOperationCount++;
                youtubeSeekCount++;
                WriteT2Event(
                    "youtube_seek",
                    targetDisplayId,
                    youtubeSeekCount.ToString(CultureInfo.InvariantCulture),
                    value);

                bool hasSeekDelta = TryParseSeekDelta(value, out float seekDelta);
                if (currentPhase == WebViewSessionPhase.Training
                    && trainingStep == T2TrainingStep.SeekYouTube
                    && (!hasSeekDelta || seekDelta < -0.5f))
                {
                    AdvanceTrainingStep();
                }
                else if (currentPhase == WebViewSessionPhase.Training
                    && trainingStep == T2TrainingStep.SkipForwardTenSeconds
                    && (!hasSeekDelta || (seekDelta >= 8f && seekDelta <= 12.5f)))
                {
                    AdvanceTrainingStep();
                }
                return;
            case "paused_seek":
                WriteT2Event("youtube_paused_seek", targetDisplayId, string.Empty, value);
                if (currentPhase == WebViewSessionPhase.MainTask
                    && d2vCompletionTime < 0f)
                {
                    HandleStrictD2VPause(value);
                }
                return;
        }
    }

    private static bool TryParseSeekDelta(string value, out float delta)
    {
        delta = 0f;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        int separatorIndex = value.LastIndexOf(',');
        return separatorIndex >= 0
            && float.TryParse(value.Substring(separatorIndex + 1), NumberStyles.Float,
                CultureInfo.InvariantCulture, out delta);
    }

    private void HandleStrictD2VPause(string value)
    {
        string targetItemId = GetD2VTargetItemId();
        if (!TryParseVideoTime(value, out float videoTime))
        {
            WriteT2Event("d2v_pause_invalid_time", targetDisplayId, value, targetItemId);
            return;
        }

        string videoTimeText = videoTime.ToString("0.000", CultureInfo.InvariantCulture);
        if (mainStage != T2MainStage.D2V)
        {
            WriteT2Event("d2v_pause_ignored", targetDisplayId, videoTimeText,
                $"stage={mainStage};target={targetItemId}");
            return;
        }

        if (!d2vTargetDetailOpened)
        {
            WriteT2Event("d2v_pause_before_target_open", targetDisplayId, videoTimeText, targetItemId);
            return;
        }

        if (!IsD2VPauseWithinTargetRange(videoTime))
        {
            WriteT2Event("d2v_pause_outside_target", targetDisplayId, videoTimeText,
                $"{targetItemId};range={GetD2VPauseRangeText()}");
            return;
        }

        d2vCompletionTime = Mathf.Max(0f, Time.time - taskStartTime);
        WriteT2Event("d2v_complete", targetDisplayId,
            d2vCompletionTime.ToString("0.000", CultureInfo.InvariantCulture),
            $"{targetItemId};video_time={videoTimeText};range={GetD2VPauseRangeText()}");
    }

    private bool TryParseVideoTime(string value, out float videoTime)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out videoTime);
    }

    private bool IsD2VPauseWithinTargetRange(float videoTime)
    {
        Vector2 range = GetD2VPauseRange();
        float rangeStart = Mathf.Min(range.x, range.y);
        float rangeEnd = Mathf.Max(range.x, range.y);
        return videoTime >= rangeStart && videoTime <= rangeEnd;
    }

    private Vector2 GetD2VPauseRange()
    {
        return d2vPauseRangeSetASeconds;
    }

    private string GetD2VPauseRangeText()
    {
        Vector2 range = GetD2VPauseRange();
        float rangeStart = Mathf.Min(range.x, range.y);
        float rangeEnd = Mathf.Max(range.x, range.y);
        return rangeStart.ToString("0.000", CultureInfo.InvariantCulture)
            + "-"
            + rangeEnd.ToString("0.000", CultureInfo.InvariantCulture);
    }

    private void HandleComparisonPageTelemetry(string eventType, string value)
    {
        switch (eventType)
        {
            case "scroll":
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float delta))
                {
                    float absoluteDelta = Mathf.Abs(delta);
                    webScrollAmount += absoluteDelta;
                    practiceStepScrollAmount += Mathf.Max(0f, delta);
                }
                WriteT2Event("web_scroll", secondaryDisplayId, value, string.Empty);
                if (currentPhase == WebViewSessionPhase.Training
                    && trainingStep == T2TrainingStep.ScrollComparisonPage
                    && practiceStepScrollAmount >= practiceScrollThresholdPixels)
                {
                    AdvanceTrainingStep();
                }
                break;
            case "action":
                WriteT2Event("web_action", secondaryDisplayId, value, string.Empty);
                HandleComparisonAction(value);
                break;
            case "candidate":
                HandleCandidateSelection(value);
                break;
        }
    }

    private void HandleComparisonAction(string action)
    {
        string normalized = action ?? string.Empty;
        if (normalized.StartsWith("product_open:", StringComparison.Ordinal))
        {
            string itemId = normalized.Substring("product_open:".Length).Trim();
            detailPageOpenCount++;
            if (currentPhase == WebViewSessionPhase.Training
                && trainingStep == T2TrainingStep.OpenProductDetail)
            {
                AdvanceTrainingStep();
            }

            if (currentPhase == WebViewSessionPhase.MainTask)
            {
                if (itemId == GetV2DTargetItemId())
                {
                    v2dTargetDetailOpened = true;
                    WriteT2Event("v2d_target_open", secondaryDisplayId, itemId,
                        mainStage.ToString());
                }

                if (itemId == GetD2VTargetItemId())
                {
                    d2vTargetDetailOpened = true;
                    WriteT2Event("d2v_target_open", secondaryDisplayId, itemId, string.Empty);
                }
            }
            return;
        }

        if (normalized.StartsWith("add_candidate:", StringComparison.Ordinal))
        {
            candidateAddCount++;
            string itemId = normalized.Substring("add_candidate:".Length).Trim();
            if (currentPhase == WebViewSessionPhase.Training
                && trainingStep == T2TrainingStep.AddCandidate)
            {
                AdvanceTrainingStep();
            }
            else if (currentPhase == WebViewSessionPhase.MainTask
                && mainStage == T2MainStage.V2D
                && v2dCompletionTime < 0f
                && v2dTargetDetailOpened
                && itemId == GetV2DTargetItemId())
            {
                v2dCompletionTime = Mathf.Max(0f, Time.time - taskStartTime);
                WriteT2Event("v2d_complete", secondaryDisplayId,
                    v2dCompletionTime.ToString("0.000", CultureInfo.InvariantCulture), itemId);
            }
            else if (currentPhase == WebViewSessionPhase.MainTask
                && mainStage == T2MainStage.SemiFree)
            {
                WriteT2Event("semi_free_candidate_added", secondaryDisplayId, itemId,
                    "continue_until_confirmation_unlock");
            }
            return;
        }

        if (normalized.StartsWith("remove_candidate:", StringComparison.Ordinal))
        {
            candidateRemoveCount++;
            if (currentPhase == WebViewSessionPhase.Training
                && trainingStep == T2TrainingStep.RemoveCandidate)
            {
                AdvanceTrainingStep();
            }
        }
    }

    private string GetV2DTargetItemId()
    {
        return "A03";
    }

    private string GetD2VTargetItemId()
    {
        return "A08";
    }

    private void AdvanceTrainingStep()
    {
        if (trainingStep == T2TrainingStep.Completed)
        {
            return;
        }

        T2TrainingStep completedStep = trainingStep;
        completedPracticeStepCount++;
        WriteT2Event(
            "practice_step_complete",
            string.Empty,
            completedStep.ToString(),
            $"completed_steps={completedPracticeStepCount};additional_steps={AdditionalPracticeStepCount}");

        trainingStep++;
        if (trainingStep == T2TrainingStep.ScrollComparisonPage)
        {
            practiceStepScrollAmount = 0f;
        }

        if (trainingStep == T2TrainingStep.Completed)
        {
            completedPracticeRounds++;
            WriteT2Event(
                "practice_round_complete",
                secondaryDisplayId,
                completedPracticeRounds.ToString(CultureInfo.InvariantCulture),
                $"minimum_rounds={MinimumPracticeRounds};completed_steps={completedPracticeStepCount};can_end={CanEndTraining}");
            trainingStep = T2TrainingStep.PauseYouTube;
            practiceStepScrollAmount = 0f;
            WriteT2Event("practice_step", string.Empty, trainingStep.ToString(), GetTrainingInstruction());
        }
        else
        {
            WriteT2Event("practice_step", string.Empty, trainingStep.ToString(), GetTrainingInstruction());
        }

        ApplyCurrentPageInstruction();
    }

    private void MarkPracticeCompleted()
    {
        if (practiceCompletionLogged)
        {
            return;
        }

        practiceCompletionLogged = true;
        float practiceDuration = taskStartTime > 0f
            ? Mathf.Max(0f, Time.time - taskStartTime)
            : 0f;
        pendingResult = "completed";
        taskEndTime = Time.time;
        WriteT2Event(
            "practice_complete",
            secondaryDisplayId,
            practiceDuration.ToString("0.000", CultureInfo.InvariantCulture),
            $"duration_seconds;rounds={completedPracticeRounds};completed_steps={completedPracticeStepCount};"
            + $"additional_steps={AdditionalPracticeStepCount};readiness_confirmed");
        logger?.LogEvent("T2Web_PracticeCompleted", activeCondition, secondaryDisplayId, Vector2.zero);
        FinalizeSessionResult(WebViewSessionPhase.Training, pendingResult);
        taskActive = false;
        ReturnToConditionSelection();
        Debug.Log(
            $"[WebViewSession] Practice completed and returned to task selection. "
            + $"condition={activeCondition}, duration={practiceDuration:0.000}, "
            + $"rounds={completedPracticeRounds}, additionalSteps={AdditionalPracticeStepCount}");
    }

    private void HandleCandidateSelection(string candidate)
    {
        if (currentPhase != WebViewSessionPhase.MainTask)
        {
            RejectCandidate("Main課題中に購入候補を選択してください。");
            return;
        }

        if (mainStage != T2MainStage.Finalize)
        {
            WriteT2Event("candidate_blocked", secondaryDisplayId, candidate, "finalize_stage_required");
            RejectCandidate("V2D、D2V、半自由比較の各タスクを完了してから候補を確定してください。");
            return;
        }

        if (IsCandidateConfirmationLocked(out float unlockRemainingSeconds))
        {
            int remainingSeconds = Mathf.CeilToInt(unlockRemainingSeconds);
            WriteT2Event(
                "candidate_blocked",
                secondaryDisplayId,
                candidate,
                $"minimum_elapsed_time;remaining={remainingSeconds}");
            RejectCandidate(
                $"候補の確定は本番開始から11分30秒後に可能です。あと{FormatCountdown(unlockRemainingSeconds)}お待ちください。");
            ApplyCurrentPageInstruction();
            return;
        }

        if (!string.IsNullOrEmpty(selectedCandidate))
        {
            RejectCandidate("候補はすでに確定されています。");
            return;
        }

        string normalizedCandidate = string.IsNullOrWhiteSpace(candidate)
            ? string.Empty
            : candidate.Trim();
        if (string.IsNullOrEmpty(normalizedCandidate))
        {
            RejectCandidate("候補を認識できませんでした。もう一度選択してください。");
            return;
        }

        bool selectedAfterTimeout = awaitingPostTimeoutSelection;
        if (youtubeOperationCount <= 0 && !selectedAfterTimeout)
        {
            WriteT2Event("candidate_blocked", secondaryDisplayId, normalizedCandidate, "youtube_operation_required");
            RejectCandidate("先に奥のYouTubeを少なくとも1回、再生・停止・シークしてください。");
            return;
        }

        if (youtubeOperationCount <= 0)
        {
            WriteT2Event(
                "post_timeout_youtube_requirement_skipped",
                targetDisplayId,
                normalizedCandidate,
                "main_metrics_already_frozen");
        }

        selectedCandidate = normalizedCandidate;
        if (selectedAfterTimeout)
        {
            postTimeoutFinalSelection = normalizedCandidate;
            postTimeoutSelectionTime = Time.time;
            WriteT2Event(
                "post_timeout_candidate_selected",
                secondaryDisplayId,
                postTimeoutFinalSelection,
                $"selection_time={postTimeoutSelectionTime:0.000};delay={Mathf.Max(0f, postTimeoutSelectionTime - taskEndTime):0.000}");
            WritePostTimeoutSelectionResult();
            awaitingPostTimeoutSelection = false;
        }
        else
        {
            pendingResult = "completed";
            taskEndTime = Time.time;
            WriteT2Event("candidate_selected", secondaryDisplayId, selectedCandidate, string.Empty);
            EmitEndSyncMarkerIfNeeded("candidate_selected");
        }

        FinalizeSessionResult(currentPhase, pendingResult);
        if (selectedAfterTimeout)
        {
            CloseT2LogWriters();
        }
        taskActive = false;
        awaitingPostTaskReset = true;
        ApplyCurrentPageInstruction();
        activeSecondaryWebViewBridge?.TryEvaluateJavaScript(
            "window.t2ConfirmCandidate&&window.t2ConfirmCandidate('"
            + EscapeJavaScriptString(selectedCandidate) + "');");
        logger?.LogEvent(
            selectedAfterTimeout ? "T2Web_PostTimeoutSelectionCompleted" : "T2Web_Completed",
            activeCondition,
            secondaryDisplayId,
            Vector2.zero);
    }

    private bool IsCandidateConfirmationLocked(out float remainingSeconds)
    {
        float unlockSeconds = Mathf.Max(0f, candidateConfirmationUnlockSeconds);
        float elapsed = currentPhase == WebViewSessionPhase.MainTask && taskStartTime > 0f
            ? Mathf.Max(0f, Time.time - taskStartTime)
            : 0f;
        remainingSeconds = Mathf.Max(0f, unlockSeconds - elapsed);
        return unlockSeconds > 0f && (currentPhase != WebViewSessionPhase.MainTask || taskStartTime <= 0f || remainingSeconds > 0f);
    }

    private void TryOpenCandidateListAtUnlock()
    {
        if (candidateListOpenedAtUnlock
            || activeSecondaryWebViewBridge == null
            || !activeSecondaryWebViewBridge.IsReady)
        {
            return;
        }

        const string script =
            "window.__t2OpenCandidateListRequested=true;"
            + "window.t2OpenCandidateList&&window.t2OpenCandidateList();";
        if (!activeSecondaryWebViewBridge.TryEvaluateJavaScript(script))
        {
            return;
        }

        candidateListOpenedAtUnlock = true;
        WriteT2Event(
            "candidate_list_auto_open",
            secondaryDisplayId,
            Mathf.Max(0f, Time.time - taskStartTime).ToString("0.000", CultureInfo.InvariantCulture),
            "confirmation_unlock");
    }

    private void RejectCandidate(string message)
    {
        activeSecondaryWebViewBridge?.TryEvaluateJavaScript(
            "window.t2RejectCandidate&&window.t2RejectCandidate('"
            + EscapeJavaScriptString(message) + "');");
    }

    private void EndYouTubePause(float endTime)
    {
        if (youtubePauseStartedAt < 0f)
        {
            return;
        }

        youtubeTotalPauseTime += Mathf.Max(0f, endTime - youtubePauseStartedAt);
        youtubePauseStartedAt = -1f;
    }

    private void FinalizeSessionResult(
        WebViewSessionPhase endedPhase,
        string result,
        bool keepLogWritersOpen = false)
    {
        if (resultWritten)
        {
            return;
        }

        EndYouTubePause(taskEndTime);
        EnsureT2LogWriters();
        float measurementStartTime = taskStartTime;
        float duration = Mathf.Max(0f, taskEndTime - measurementStartTime);
        t2ResultWriter?.WriteLine(string.Join(",",
            Csv(participantId),
            Csv(ExperimentDataFileNaming.BuildAllocationCode(participantId, activeCondition)),
            Csv(sessionId),
            Csv(runId),
            Csv(ExperimentDataFileNaming.CurrentSchemaVersion),
            Csv(conditionOrder),
            Csv(GetMethodDataLabel()),
            Csv(GetContentSetLabel()),
            Csv(endedPhase == WebViewSessionPhase.Training ? "practice" : "main"),
            measurementStartTime.ToString("0.000", CultureInfo.InvariantCulture),
            taskEndTime.ToString("0.000", CultureInfo.InvariantCulture),
            duration.ToString("0.000", CultureInfo.InvariantCulture),
            endedPhase == WebViewSessionPhase.Training
                ? MinimumPracticeRounds.ToString(CultureInfo.InvariantCulture)
                : string.Empty,
            endedPhase == WebViewSessionPhase.Training
                ? completedPracticeRounds.ToString(CultureInfo.InvariantCulture)
                : string.Empty,
            endedPhase == WebViewSessionPhase.Training
                ? completedPracticeStepCount.ToString(CultureInfo.InvariantCulture)
                : string.Empty,
            endedPhase == WebViewSessionPhase.Training
                ? AdditionalPracticeStepCount.ToString(CultureInfo.InvariantCulture)
                : string.Empty,
            endedPhase == WebViewSessionPhase.Training && result == "completed" ? "1" : "0",
            Csv(string.Equals(result, "timeout", StringComparison.Ordinal) ? string.Empty : selectedCandidate),
            Csv(postTimeoutFinalSelection),
            postTimeoutSelectionTime.ToString("0.000", CultureInfo.InvariantCulture),
            youtubeOperationCount.ToString(CultureInfo.InvariantCulture),
            youtubePauseCount.ToString(CultureInfo.InvariantCulture),
            youtubePlayCount.ToString(CultureInfo.InvariantCulture),
            youtubeSeekCount.ToString(CultureInfo.InvariantCulture),
            youtubeTotalPauseTime.ToString("0.000", CultureInfo.InvariantCulture),
            webScrollAmount.ToString("0.000", CultureInfo.InvariantCulture),
            webClickCount.ToString(CultureInfo.InvariantCulture),
            detailPageOpenCount.ToString(CultureInfo.InvariantCulture),
            candidateAddCount.ToString(CultureInfo.InvariantCulture),
            candidateRemoveCount.ToString(CultureInfo.InvariantCulture),
            v2dCompletionTime.ToString("0.000", CultureInfo.InvariantCulture),
            d2vCompletionTime.ToString("0.000", CultureInfo.InvariantCulture),
            displaySwitchCount.ToString(CultureInfo.InvariantCulture),
            controllerMovementAmount.ToString("0.000000", CultureInfo.InvariantCulture),
            controllerRotationAmount.ToString("0.000", CultureInfo.InvariantCulture),
            clutchCount.ToString(CultureInfo.InvariantCulture),
            Csv(result),
            Csv(resultNote)));
        t2ResultWriter?.Flush();
        resultWritten = true;
        WriteT2Event(
            "task_end",
            string.Empty,
            result,
            $"duration={duration:0.000};candidate={selectedCandidate};post_timeout_candidate={postTimeoutFinalSelection}");
        EmitEndSyncMarkerIfNeeded($"finalize_result={result}");
        if (keepLogWritersOpen)
        {
            t2EventWriter?.Flush();
            t2ResultWriter?.Flush();
        }
        else
        {
            CloseT2LogWriters();
        }
    }

    private void EmitStartSyncMarkerIfNeeded()
    {
        if (startSyncMarkerLogged)
        {
            return;
        }

        startSyncMarkerLogged = true;
        GetSyncMarkerController()?.MarkStart(
            logger,
            activeCondition,
            $"T2;phase={currentPhase};contentSet={selectedContentSet}");
        WriteT2Event("sync_marker", targetDisplayId, "START",
            Time.time.ToString("0.000", CultureInfo.InvariantCulture));
    }

    private void EmitEndSyncMarkerIfNeeded(string reason)
    {
        if (!startSyncMarkerLogged || endSyncMarkerLogged)
        {
            return;
        }

        endSyncMarkerLogged = true;
        GetSyncMarkerController()?.MarkEnd(
            logger,
            activeCondition,
            $"T2;phase={currentPhase};contentSet={selectedContentSet};reason={reason}");
        WriteT2Event("sync_marker", !string.IsNullOrEmpty(secondaryDisplayId) ? secondaryDisplayId : targetDisplayId, "END",
            Time.time.ToString("0.000", CultureInfo.InvariantCulture));
    }

    private SyncMarkerController GetSyncMarkerController()
    {
        if (syncMarkerController == null)
        {
            syncMarkerController = SyncMarkerController.GetOrCreate();
        }

        return syncMarkerController;
    }

    private void EnsureT2LogWriters()
    {
        if (t2EventWriter != null && t2ResultWriter != null)
        {
            return;
        }

        string directory = Path.Combine(Application.persistentDataPath, "Logs", "T2");
        Directory.CreateDirectory(directory);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
        string phaseCode = currentPhase == WebViewSessionPhase.Training ? "Practice" : "Main";
        t2EventWriter = new StreamWriter(Path.Combine(
            directory,
            ExperimentDataFileNaming.BuildCsvFileName(
                participantId,
                activeCondition,
                "T2",
                "events",
                stamp,
                phaseCode,
                runId)));
        t2EventWriter.WriteLine("timestamp,participant_id,allocation_code,session_id,run_id,schema_version,condition_order,method,content_set,task_phase,event_type,display_id,value,details");
        t2ResultWriter = new StreamWriter(Path.Combine(
            directory,
            ExperimentDataFileNaming.BuildCsvFileName(
                participantId,
                activeCondition,
                "T2",
                "results",
                stamp,
                phaseCode,
                runId)));
        t2ResultWriter.WriteLine(
            "participant_id,allocation_code,session_id,run_id,schema_version,condition_order,method,content_set,task_phase,task_start_time,task_end_time,duration," +
            "minimum_practice_rounds,completed_practice_rounds,completed_practice_steps,additional_practice_steps,readiness_confirmed,selected_candidate," +
            "post_timeout_final_selection,post_timeout_selection_time," +
            "youtube_operation_count,youtube_pause_count,youtube_play_count,youtube_seek_count,youtube_total_pause_time," +
            "web_scroll_amount,web_click_count,detail_page_open_count,candidate_add_count,candidate_remove_count,v2d_completion_time,d2v_completion_time," +
            "display_switch_count,controller_movement_amount,controller_rotation_amount,clutch_count,result,note");
        t2PostTimeoutSelectionLogPath = Path.Combine(
            directory,
            ExperimentDataFileNaming.BuildCsvFileName(
                participantId,
                activeCondition,
                "T2",
                "post_timeout_selections",
                stamp,
                phaseCode,
                runId));
    }

    private void WritePostTimeoutSelectionResult()
    {
        EnsureT2LogWriters();
        if (string.IsNullOrWhiteSpace(t2PostTimeoutSelectionLogPath))
        {
            return;
        }

        if (t2PostTimeoutSelectionWriter == null)
        {
            bool writeHeader = !File.Exists(t2PostTimeoutSelectionLogPath)
                || new FileInfo(t2PostTimeoutSelectionLogPath).Length == 0;
            t2PostTimeoutSelectionWriter = new StreamWriter(
                t2PostTimeoutSelectionLogPath,
                true);
            if (writeHeader)
            {
                t2PostTimeoutSelectionWriter.WriteLine(
                    "participant_id,allocation_code,session_id,run_id,schema_version,condition_order,method,content_set,task_end_time," +
                    "post_timeout_final_selection,post_timeout_selection_time,selection_delay_seconds," +
                    "youtube_operation_count,timestamp");
            }
        }

        t2PostTimeoutSelectionWriter.WriteLine(string.Join(",",
            Csv(participantId),
            Csv(ExperimentDataFileNaming.BuildAllocationCode(participantId, activeCondition)),
            Csv(sessionId),
            Csv(runId),
            Csv(ExperimentDataFileNaming.CurrentSchemaVersion),
            Csv(conditionOrder),
            Csv(GetMethodDataLabel()),
            Csv(GetContentSetLabel()),
            taskEndTime.ToString("0.000", CultureInfo.InvariantCulture),
            Csv(postTimeoutFinalSelection),
            postTimeoutSelectionTime.ToString("0.000", CultureInfo.InvariantCulture),
            Mathf.Max(0f, postTimeoutSelectionTime - taskEndTime)
                .ToString("0.000", CultureInfo.InvariantCulture),
            youtubeOperationCount.ToString(CultureInfo.InvariantCulture),
            DateTime.Now.ToString("O", CultureInfo.InvariantCulture)));
        t2PostTimeoutSelectionWriter.Flush();
    }

    private void WriteT2Event(string eventType, string displayId, string value, string details)
    {
        EnsureT2LogWriters();
        t2EventWriter?.WriteLine(string.Join(",",
            Time.time.ToString("0.000", CultureInfo.InvariantCulture),
            Csv(participantId),
            Csv(ExperimentDataFileNaming.BuildAllocationCode(participantId, activeCondition)),
            Csv(sessionId),
            Csv(runId),
            Csv(ExperimentDataFileNaming.CurrentSchemaVersion),
            Csv(conditionOrder),
            Csv(GetMethodDataLabel()),
            Csv(GetContentSetLabel()),
            Csv(currentPhase == WebViewSessionPhase.Training ? "practice" : "main"),
            Csv(eventType),
            Csv(displayId),
            Csv(value),
            Csv(details)));
        t2EventWriter?.Flush();
    }

    private void CloseT2LogWriters()
    {
        t2EventWriter?.Flush();
        t2EventWriter?.Dispose();
        t2EventWriter = null;
        t2ResultWriter?.Flush();
        t2ResultWriter?.Dispose();
        t2ResultWriter = null;
        t2PostTimeoutSelectionWriter?.Flush();
        t2PostTimeoutSelectionWriter?.Dispose();
        t2PostTimeoutSelectionWriter = null;
    }

    private string GetMethodLabel()
    {
        switch (activeCondition)
        {
            case InteractionCondition.GazeRay:
                return "GazeRay";
            case InteractionCondition.ExplicitDisplayFocus:
                return "Proposed";
            default:
                return "Ray";
        }
    }

    private string GetMethodDataLabel()
    {
        return activeCondition.ToString();
    }

    private string GetContentSetLabel()
    {
        return "A";
    }

    private static string NormalizeId(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string Csv(string value)
    {
        string safe = value ?? string.Empty;
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }

    private static string EscapeJavaScriptString(string value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }

    private TLabWebViewDisplayBridge ResolveNavigationWebViewBridge(out string displayId)
    {
        displayId = targetDisplayId;
        DisplaySurface inputDisplay = null;

        if (inputManager != null
            && inputManager.CurrentCondition == InteractionCondition.ExplicitDisplayFocus)
        {
            inputDisplay = displayManager != null ? displayManager.FocusedDisplay : null;
        }
        else if (displayManager != null && displayManager.HasCurrentRaycastHit)
        {
            inputDisplay = displayManager.CurrentRaycastHit.Display;
        }

        if (inputDisplay != null)
        {
            TLabWebViewDisplayBridge inputBridge = inputDisplay.GetComponent<TLabWebViewDisplayBridge>();
            if (inputBridge != null
                && (ReferenceEquals(inputBridge, activeWebViewBridge)
                    || ReferenceEquals(inputBridge, activeSecondaryWebViewBridge)))
            {
                displayId = inputDisplay.name;
                return inputBridge;
            }
        }

        if (activeWebViewBridge != null)
        {
            return activeWebViewBridge;
        }

        if (activeSecondaryWebViewBridge != null)
        {
            displayId = secondaryDisplayId;
            return activeSecondaryWebViewBridge;
        }

        return ResolveTargetWebViewBridge();
    }

    private void DisableNonSessionWebViews(
        TLabWebViewDisplayBridge primaryBridge,
        TLabWebViewDisplayBridge secondaryBridge)
    {
        TLabWebViewDisplayBridge[] bridges = FindObjectsOfType<TLabWebViewDisplayBridge>(true);
        for (int index = 0; index < bridges.Length; index++)
        {
            TLabWebViewDisplayBridge bridge = bridges[index];
            if (!ReferenceEquals(bridge, primaryBridge)
                && !ReferenceEquals(bridge, secondaryBridge))
            {
                bridge.DisableWebView();
            }
        }
    }

    private void DisableSessionWebViews()
    {
        DisableDisplayWebViews(FindTargetDisplay());
        DisableDisplayWebViews(FindDisplay(secondaryDisplayId));
        activeWebViewBridge?.DisableWebView();
        activeSecondaryWebViewBridge?.DisableWebView();
        activeWebViewBridge = null;
        activeSecondaryWebViewBridge = null;
    }

    private static void DisableDisplayWebViews(DisplaySurface display)
    {
        if (display == null)
        {
            return;
        }

        TLabWebViewDisplayBridge[] bridges = display.GetComponents<TLabWebViewDisplayBridge>();
        for (int index = 0; index < bridges.Length; index++)
        {
            bridges[index].DisableWebView();
        }
    }

    private DisplaySurface FindTargetDisplay()
    {
        DisplaySurface display = FindDisplay(targetDisplayId);
        if (display != null)
        {
            return display;
        }

        TLabWebViewDisplayBridge bridge = ResolveTargetWebViewBridge();
        return bridge != null ? bridge.GetComponent<DisplaySurface>() : null;
    }

    private DisplaySurface FindDisplay(string displayId)
    {
        ResolveDisplayReferences();
        if (displayA != null && displayA.name == displayId)
        {
            return displayA;
        }

        if (displayB != null && displayB.name == displayId)
        {
            return displayB;
        }

        if (displayManager == null || displayManager.Displays == null)
        {
            return null;
        }

        for (int index = 0; index < displayManager.Displays.Length; index++)
        {
            DisplaySurface display = displayManager.Displays[index];
            if (display != null && display.name == displayId)
            {
                return display;
            }
        }

        return null;
    }

    private void ResolveReferences()
    {
        if (inputManager == null)
        {
            inputManager = PrototypeInputManager.Instance != null
                ? PrototypeInputManager.Instance
                : FindObjectOfType<PrototypeInputManager>();
        }

        if (experimentManager == null)
        {
            experimentManager = FindObjectOfType<ExperimentManager>();
        }

        if (displayManager == null)
        {
            displayManager = DisplayManager.Instance != null
                ? DisplayManager.Instance
                : FindObjectOfType<DisplayManager>();
        }

        if (logger == null)
        {
            logger = FindObjectOfType<Logger>();
        }

        if (virtualCursorController == null)
        {
            virtualCursorController = FindObjectOfType<VirtualCursorController>();
        }

        if (raycastPointer == null)
        {
            raycastPointer = FindObjectOfType<RaycastPointer>();
        }

        if (hmdCamera == null)
        {
            hmdCamera = Camera.main;
        }

        ResolveDisplayReferences();

        ResolveTargetWebViewBridge();
        ResolveSecondaryWebViewBridge();
    }

    private TLabWebViewDisplayBridge ResolveTargetWebViewBridge()
    {
        ResolveDisplayReferences();
        DisplaySurface targetDisplay = FindDisplay(targetDisplayId);

        if (targetDisplay != null)
        {
            targetTLabWebViewBridge = targetDisplay.GetComponent<TLabWebViewDisplayBridge>();

            if (targetTLabWebViewBridge == null
                && autoAddTLabBridgeToTargetDisplay)
            {
                targetTLabWebViewBridge = targetDisplay.gameObject.AddComponent<TLabWebViewDisplayBridge>();
                Debug.Log($"[WebViewSession] Added TLabWebViewDisplayBridge to {targetDisplay.name}");
            }
        }

        activeWebViewBridge = targetTLabWebViewBridge;
        return activeWebViewBridge;
    }

    private TLabWebViewDisplayBridge ResolveSecondaryWebViewBridge()
    {
        if (!enableSecondaryWebView)
        {
            activeSecondaryWebViewBridge = null;
            return null;
        }

        ResolveDisplayReferences();
        DisplaySurface targetDisplay = FindDisplay(targetDisplayId);
        DisplaySurface secondaryDisplay = FindDisplay(secondaryDisplayId);
        if (secondaryDisplay == null || secondaryDisplay == targetDisplay)
        {
            activeSecondaryWebViewBridge = null;
            return null;
        }

        TLabWebViewDisplayBridge secondaryTLabBridge = secondaryDisplay.GetComponent<TLabWebViewDisplayBridge>();
        if (secondaryTLabBridge == null
            && autoAddTLabBridgeToSecondaryDisplay)
        {
            secondaryTLabBridge = secondaryDisplay.gameObject.AddComponent<TLabWebViewDisplayBridge>();
            Debug.Log($"[WebViewSession] Added TLabWebViewDisplayBridge to {secondaryDisplay.name}");
        }

        activeSecondaryWebViewBridge = secondaryTLabBridge;
        return activeSecondaryWebViewBridge;
    }

    private void ResolveDisplayReferences()
    {
        if (displayManager != null && displayManager.Displays != null)
        {
            for (int index = 0; index < displayManager.Displays.Length; index++)
            {
                DisplaySurface display = displayManager.Displays[index];
                if (display == null)
                {
                    continue;
                }

                if (displayA == null && display.name == "Display_A_Front")
                {
                    displayA = display;
                }

                if (displayB == null && display.name == "Display_B_Back")
                {
                    displayB = display;
                }
            }
        }
    }
}
