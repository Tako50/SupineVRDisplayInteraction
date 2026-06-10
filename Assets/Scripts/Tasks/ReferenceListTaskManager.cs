using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public enum ReferenceListTaskPhase
{
    ConditionSelection,
    Training,
    MainTask
}

public enum ReferenceListResultType
{
    Correct,
    DisplayError,
    TargetError,
    Miss
}

[DisallowMultipleComponent]
/// <summary>
/// T2-A「参照しながらリスト選択」を管理する。
/// 片方の表示に選択対象を提示し、もう片方のスクロール可能なリストから同じ項目を選ばせる。
/// </summary>
public class ReferenceListTaskManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private ExperimentManager experimentManager;
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private Logger logger;
    [SerializeField] private DisplaySurface displayA;
    [SerializeField] private DisplaySurface displayB;

    [Header("Session")]
    [SerializeField] private string participantId = "P000";
    [SerializeField] private string sessionId = "S001";

    [Header("Trials")]
    [SerializeField] private int trainingTrialCount = 4;
    [SerializeField] private int mainTrialsPerDisplay = 5;

    [Header("List")]
    [SerializeField] private int itemCount = 40;
    [SerializeField] private float itemHeightPixels = 44f;
    [SerializeField] private float rowWidthPixels = 430f;
    [SerializeField] private float scrollGestureTimeoutSeconds = 0.22f;

    [Header("Visuals")]
    [SerializeField] private Color instructionPanelColor = new Color(0.06f, 0.08f, 0.14f, 0.96f);
    [SerializeField] private Color listRowColor = new Color(0.12f, 0.16f, 0.24f, 0.98f);
    [SerializeField] private Color alternatingRowColor = new Color(0.16f, 0.21f, 0.30f, 0.98f);
    [SerializeField] private Color rowTextColor = Color.white;

    private readonly List<TrialConfig> trials = new List<TrialConfig>();
    private readonly Dictionary<DisplaySurface, DisplayUi> displayUis =
        new Dictionary<DisplaySurface, DisplayUi>();

    private InteractionCondition selectedCondition = InteractionCondition.RaycastBaseline;
    private DisplayLayoutPreset selectedLayout = DisplayLayoutPreset.StrongOcclusion;
    private InteractionCondition activeCondition = InteractionCondition.RaycastBaseline;
    private ReferenceListTaskPhase currentPhase = ReferenceListTaskPhase.ConditionSelection;

    private int currentTrialListIndex = -1;
    private int attemptIndex;
    private int wrongDisplayScrollCount;
    private float totalScrollAmount;
    private float wrongDisplayScrollAmount;
    private float trialStartTime;
    private bool trialRunning;

    private string currentGestureDisplayId = string.Empty;
    private bool currentGestureIsWrong;
    private float currentGestureAmount;
    private float lastScrollEventTime = float.NegativeInfinity;

    private StreamWriter resultsWriter;
    private StreamWriter eventsWriter;
    private string resultsPath = string.Empty;
    private string eventsPath = string.Empty;

    public ReferenceListTaskPhase CurrentPhase => currentPhase;
    public bool IsTaskRunning => currentPhase != ReferenceListTaskPhase.ConditionSelection;
    public bool IsTrialRunning => trialRunning;
    public string ResultsPath => resultsPath;
    public string EventsPath => eventsPath;

    private struct TrialConfig
    {
        public int TrialIndex;
        public string TargetDisplayId;
        public string InstructionDisplayId;
        public int TargetItemIndex;
        public string TargetItemId;
    }

    private sealed class DisplayUi
    {
        public GameObject InstructionRoot;
        public Text InstructionText;
        public GameObject ListRoot;
        public readonly List<RectTransform> Rows = new List<RectTransform>();
        public readonly List<string> ItemIds = new List<string>();
        public Text ExistingScrollText;
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (trialRunning
            && !string.IsNullOrEmpty(currentGestureDisplayId)
            && Time.time - lastScrollEventTime >= scrollGestureTimeoutSeconds)
        {
            EndScrollGesture(Time.time);
        }
    }

    private void OnDisable()
    {
        EndScrollGesture(Time.time);
        CloseLogs();
    }

    public void SetSelectedCondition(InteractionCondition condition)
    {
        if (!IsTaskRunning)
        {
            selectedCondition = condition;
        }
    }

    public void SetSelectedLayout(DisplayLayoutPreset layout)
    {
        if (!IsTaskRunning)
        {
            selectedLayout = layout;
        }
    }

    public void SetParticipantId(string value)
    {
        participantId = string.IsNullOrWhiteSpace(value) ? "P000" : value.Trim();
    }

    public void SetSessionId(string value)
    {
        sessionId = string.IsNullOrWhiteSpace(value) ? "S001" : value.Trim();
    }

    public void BeginTrainingTask()
    {
        BeginTask(ReferenceListTaskPhase.Training);
    }

    public void BeginMainTask()
    {
        BeginTask(ReferenceListTaskPhase.MainTask);
    }

    public void ReturnToConditionSelection()
    {
        if (currentPhase == ReferenceListTaskPhase.ConditionSelection)
        {
            return;
        }

        EndScrollGesture(Time.time);
        trialRunning = false;
        currentPhase = ReferenceListTaskPhase.ConditionSelection;
        currentTrialListIndex = -1;
        trials.Clear();

        if (inputManager != null)
        {
            inputManager.UnlockCondition();
        }

        SetTaskUiActive(false);
        CloseLogs();

        if (displayManager != null)
        {
            displayManager.SetAllDisplayContentMode(DisplayContentMode.ConditionSelection);
        }

        if (logger != null)
        {
            logger.LogEvent("T2A_TaskReturn", activeCondition, string.Empty, Vector2.zero);
        }
    }

    public void HandleScroll(string displayId, float appliedScroll, float eventTime)
    {
        if (!trialRunning || Mathf.Approximately(appliedScroll, 0f))
        {
            return;
        }

        TrialConfig trial = trials[currentTrialListIndex];
        bool wrongDisplay = !string.Equals(
            displayId,
            trial.TargetDisplayId,
            StringComparison.Ordinal);

        bool startsNewGesture =
            string.IsNullOrEmpty(currentGestureDisplayId)
            || !string.Equals(currentGestureDisplayId, displayId, StringComparison.Ordinal)
            || eventTime - lastScrollEventTime >= scrollGestureTimeoutSeconds;

        if (startsNewGesture)
        {
            EndScrollGesture(eventTime);
            currentGestureDisplayId = displayId ?? string.Empty;
            currentGestureIsWrong = wrongDisplay;
            currentGestureAmount = 0f;

            if (wrongDisplay)
            {
                wrongDisplayScrollCount++;
            }
        }

        float absoluteAmount = Mathf.Abs(appliedScroll);
        currentGestureAmount += absoluteAmount;
        totalScrollAmount += absoluteAmount;
        if (wrongDisplay)
        {
            wrongDisplayScrollAmount += absoluteAmount;
        }

        lastScrollEventTime = eventTime;
    }

    public void HandleClick(
        string clickedDisplayId,
        Vector2 normalizedPosition,
        InteractionCondition condition,
        float clickTime)
    {
        if (!trialRunning)
        {
            return;
        }

        EndScrollGesture(clickTime);

        TrialConfig trial = trials[currentTrialListIndex];
        attemptIndex++;

        string clickedItemId = string.Empty;
        ReferenceListResultType resultType;
        bool displayError = false;
        bool targetError = false;

        if (string.IsNullOrEmpty(clickedDisplayId) || clickedDisplayId == "None")
        {
            resultType = ReferenceListResultType.Miss;
        }
        else if (!string.Equals(clickedDisplayId, trial.TargetDisplayId, StringComparison.Ordinal))
        {
            resultType = ReferenceListResultType.DisplayError;
            displayError = true;
        }
        else
        {
            DisplaySurface clickedDisplay = FindDisplay(clickedDisplayId);
            clickedItemId = FindClickedItemId(clickedDisplay, normalizedPosition);
            if (string.IsNullOrEmpty(clickedItemId))
            {
                resultType = ReferenceListResultType.Miss;
            }
            else if (!string.Equals(clickedItemId, trial.TargetItemId, StringComparison.Ordinal))
            {
                resultType = ReferenceListResultType.TargetError;
                targetError = true;
            }
            else
            {
                resultType = ReferenceListResultType.Correct;
            }
        }

        bool isCorrect = resultType == ReferenceListResultType.Correct;
        WriteResultRow(
            trial,
            clickedDisplayId,
            clickedItemId,
            resultType,
            isCorrect,
            displayError,
            targetError,
            clickTime);

        if (logger != null)
        {
            logger.LogEvent(
                "T2A_Click_" + resultType,
                condition,
                clickedDisplayId,
                normalizedPosition);
        }

        if (isCorrect)
        {
            StartNextTrial();
        }
    }

    private void BeginTask(ReferenceListTaskPhase phase)
    {
        ResolveReferences();
        if (inputManager == null || displayA == null || displayB == null)
        {
            Debug.LogError("ReferenceListTaskManager requires input and two displays.");
            return;
        }

        activeCondition = selectedCondition;
        currentPhase = phase;
        inputManager.LockCondition(activeCondition);
        experimentManager?.ApplyLayout(selectedLayout);

        BuildTrials(phase);
        PrepareTaskUi();
        OpenLogs();
        currentTrialListIndex = -1;
        trialRunning = false;

        if (logger != null)
        {
            logger.LogEvent(
                phase == ReferenceListTaskPhase.Training ? "T2A_TrainingBegin" : "T2A_MainBegin",
                activeCondition,
                string.Empty,
                Vector2.zero);
        }

        StartNextTrial();
    }

    private void BuildTrials(ReferenceListTaskPhase phase)
    {
        trials.Clear();
        int count = phase == ReferenceListTaskPhase.Training
            ? Mathf.Max(2, trainingTrialCount)
            : Mathf.Max(1, mainTrialsPerDisplay) * 2;

        for (int index = 0; index < count; index++)
        {
            bool targetFront = index % 2 == 0;
            DisplaySurface targetDisplay = targetFront ? displayA : displayB;
            DisplaySurface instructionDisplay = targetFront ? displayB : displayA;
            int targetItemIndex = (index * 11 + (targetFront ? 3 : 17)) % Mathf.Max(1, itemCount);

            trials.Add(new TrialConfig
            {
                TrialIndex = index + 1,
                TargetDisplayId = targetDisplay.name,
                InstructionDisplayId = instructionDisplay.name,
                TargetItemIndex = targetItemIndex,
                TargetItemId = BuildItemId(targetItemIndex)
            });
        }
    }

    private void StartNextTrial()
    {
        currentTrialListIndex++;
        if (currentTrialListIndex >= trials.Count)
        {
            if (currentPhase == ReferenceListTaskPhase.Training)
            {
                currentTrialListIndex = 0;
            }
            else
            {
                ReturnToConditionSelection();
                return;
            }
        }

        TrialConfig trial = trials[currentTrialListIndex];
        attemptIndex = 0;
        wrongDisplayScrollCount = 0;
        totalScrollAmount = 0f;
        wrongDisplayScrollAmount = 0f;
        currentGestureDisplayId = string.Empty;
        currentGestureAmount = 0f;
        lastScrollEventTime = float.NegativeInfinity;
        trialStartTime = Time.time;
        trialRunning = true;

        displayA.ResetScroll();
        displayB.ResetScroll();
        ShowTrial(trial);
        WriteEventRow("TrialStart", trial, string.Empty, 0f, false, trialStartTime);
    }

    private void PrepareTaskUi()
    {
        if (displayManager != null)
        {
            displayManager.SetAllDisplayContentMode(DisplayContentMode.ScrollTask);
        }

        EnsureDisplayUi(displayA);
        EnsureDisplayUi(displayB);

        float scrollRange = Mathf.Max(420f, itemCount * itemHeightPixels * 0.5f);
        displayA.SetMaxScrollPixels(scrollRange);
        displayB.SetMaxScrollPixels(scrollRange);
        SetTaskUiActive(true);
    }

    private void EnsureDisplayUi(DisplaySurface display)
    {
        if (display == null || displayUis.ContainsKey(display))
        {
            return;
        }

        DisplayUi ui = new DisplayUi();
        ui.ExistingScrollText = display.ScrollContent != null
            ? display.ScrollContent.GetComponent<Text>()
            : null;

        ui.InstructionRoot = CreateInstructionRoot(display, out Text instructionText);
        ui.InstructionText = instructionText;
        ui.ListRoot = CreateListRoot(display, ui.Rows, ui.ItemIds);
        displayUis.Add(display, ui);
    }

    private GameObject CreateInstructionRoot(DisplaySurface display, out Text instructionText)
    {
        GameObject root = new GameObject("T2A_Instruction", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(display.WorldSpaceCanvas.transform, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.08f, 0.28f);
        rect.anchorMax = new Vector2(0.92f, 0.72f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = instructionPanelColor;

        GameObject textObject = new GameObject("InstructionText", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(root.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(24f, 18f);
        textRect.offsetMax = new Vector2(-24f, -18f);

        instructionText = textObject.GetComponent<Text>();
        instructionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        instructionText.fontSize = 32;
        instructionText.alignment = TextAnchor.MiddleCenter;
        instructionText.color = Color.white;
        instructionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        instructionText.verticalOverflow = VerticalWrapMode.Overflow;
        return root;
    }

    private GameObject CreateListRoot(
        DisplaySurface display,
        List<RectTransform> rows,
        List<string> itemIds)
    {
        Transform parent = display.ScrollContent != null
            ? display.ScrollContent
            : display.WorldSpaceCanvas.transform;

        GameObject root = new GameObject("T2A_List", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(rowWidthPixels, itemCount * itemHeightPixels);
        rootRect.anchoredPosition = Vector2.zero;

        for (int index = 0; index < itemCount; index++)
        {
            string itemId = BuildItemId(index);
            GameObject row = new GameObject(
                "Item_" + itemId,
                typeof(RectTransform),
                typeof(Image));
            row.transform.SetParent(root.transform, false);

            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 0.5f);
            rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.sizeDelta = new Vector2(rowWidthPixels, itemHeightPixels - 4f);
            rowRect.anchoredPosition = new Vector2(
                0f,
                ((itemCount - 1) * 0.5f - index) * itemHeightPixels);
            row.GetComponent<Image>().color =
                index % 2 == 0 ? listRowColor : alternatingRowColor;

            GameObject label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(row.transform, false);
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(18f, 0f);
            labelRect.offsetMax = new Vector2(-18f, 0f);

            Text labelText = label.GetComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 24;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.color = rowTextColor;
            labelText.text = itemId + "    Reference item";

            rows.Add(rowRect);
            itemIds.Add(itemId);
        }

        return root;
    }

    private void ShowTrial(TrialConfig trial)
    {
        foreach (KeyValuePair<DisplaySurface, DisplayUi> entry in displayUis)
        {
            bool isTarget = string.Equals(
                entry.Key.name,
                trial.TargetDisplayId,
                StringComparison.Ordinal);
            bool isInstruction = string.Equals(
                entry.Key.name,
                trial.InstructionDisplayId,
                StringComparison.Ordinal);

            entry.Value.ListRoot.SetActive(isTarget);
            entry.Value.InstructionRoot.SetActive(isInstruction);
            if (isInstruction)
            {
                entry.Value.InstructionText.text =
                    "Find and select\n\n" + trial.TargetItemId;
            }
        }
    }

    private void SetTaskUiActive(bool active)
    {
        foreach (DisplayUi ui in displayUis.Values)
        {
            if (ui.ExistingScrollText != null)
            {
                ui.ExistingScrollText.enabled = !active;
            }

            if (!active)
            {
                ui.InstructionRoot.SetActive(false);
                ui.ListRoot.SetActive(false);
            }
        }
    }

    private string FindClickedItemId(DisplaySurface display, Vector2 normalizedPosition)
    {
        if (display == null || !displayUis.TryGetValue(display, out DisplayUi ui))
        {
            return string.Empty;
        }

        RectTransform canvasRect = display.WorldSpaceCanvas.GetComponent<RectTransform>();
        Vector2 canvasPoint = new Vector2(
            (Mathf.Clamp01(normalizedPosition.x) - 0.5f) * canvasRect.rect.width,
            (Mathf.Clamp01(normalizedPosition.y) - 0.5f) * canvasRect.rect.height);

        Vector3[] corners = new Vector3[4];
        for (int index = 0; index < ui.Rows.Count; index++)
        {
            RectTransform row = ui.Rows[index];
            if (!row.gameObject.activeInHierarchy)
            {
                continue;
            }

            row.GetWorldCorners(corners);
            Vector2 minimum = canvasRect.InverseTransformPoint(corners[0]);
            Vector2 maximum = canvasRect.InverseTransformPoint(corners[2]);
            Rect bounds = Rect.MinMaxRect(
                Mathf.Min(minimum.x, maximum.x),
                Mathf.Min(minimum.y, maximum.y),
                Mathf.Max(minimum.x, maximum.x),
                Mathf.Max(minimum.y, maximum.y));

            if (bounds.Contains(canvasPoint))
            {
                return ui.ItemIds[index];
            }
        }

        return string.Empty;
    }

    private void EndScrollGesture(float eventTime)
    {
        if (string.IsNullOrEmpty(currentGestureDisplayId) || currentTrialListIndex < 0)
        {
            return;
        }

        WriteEventRow(
            "ScrollGesture",
            trials[currentTrialListIndex],
            currentGestureDisplayId,
            currentGestureAmount,
            currentGestureIsWrong,
            eventTime);

        currentGestureDisplayId = string.Empty;
        currentGestureIsWrong = false;
        currentGestureAmount = 0f;
    }

    private void OpenLogs()
    {
        CloseLogs();
        string directory = Path.Combine(Application.persistentDataPath, "Logs");
        Directory.CreateDirectory(directory);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string safeParticipant = SanitizeFilePart(participantId);
        string safeSession = SanitizeFilePart(sessionId);

        resultsPath = Path.Combine(
            directory,
            $"t2a_results_{safeParticipant}_{safeSession}_{stamp}.csv");
        eventsPath = Path.Combine(
            directory,
            $"t2a_events_{safeParticipant}_{safeSession}_{stamp}.csv");

        resultsWriter = new StreamWriter(resultsPath, false);
        resultsWriter.WriteLine(
            "participantId,sessionId,taskPhase,condition,layoutPreset,trialIndex,"
            + "targetDisplayId,instructionDisplayId,targetItemId,targetItemIndex,"
            + "attemptIndex,clickedDisplayId,clickedItemId,resultType,isCorrect,"
            + "displayError,targetError,wrongDisplayScrollCount,totalScrollAmount,"
            + "wrongDisplayScrollAmount,trialStartTime,clickTime,timestamp");
        resultsWriter.Flush();

        eventsWriter = new StreamWriter(eventsPath, false);
        eventsWriter.WriteLine(
            "participantId,sessionId,taskPhase,condition,trialIndex,eventType,"
            + "targetDisplayId,actualDisplayId,scrollAmount,wrongDisplay,eventTime,timestamp");
        eventsWriter.Flush();
    }

    private void CloseLogs()
    {
        if (resultsWriter != null)
        {
            resultsWriter.Flush();
            resultsWriter.Dispose();
            resultsWriter = null;
        }

        if (eventsWriter != null)
        {
            eventsWriter.Flush();
            eventsWriter.Dispose();
            eventsWriter = null;
        }
    }

    private void WriteResultRow(
        TrialConfig trial,
        string clickedDisplayId,
        string clickedItemId,
        ReferenceListResultType resultType,
        bool isCorrect,
        bool displayError,
        bool targetError,
        float clickTime)
    {
        if (resultsWriter == null)
        {
            return;
        }

        resultsWriter.WriteLine(string.Join(",",
            Csv(participantId),
            Csv(sessionId),
            currentPhase.ToString(),
            activeCondition.ToString(),
            selectedLayout.ToString(),
            trial.TrialIndex.ToString(CultureInfo.InvariantCulture),
            Csv(trial.TargetDisplayId),
            Csv(trial.InstructionDisplayId),
            Csv(trial.TargetItemId),
            trial.TargetItemIndex.ToString(CultureInfo.InvariantCulture),
            attemptIndex.ToString(CultureInfo.InvariantCulture),
            Csv(clickedDisplayId),
            Csv(clickedItemId),
            resultType.ToString(),
            isCorrect ? "1" : "0",
            displayError ? "1" : "0",
            targetError ? "1" : "0",
            wrongDisplayScrollCount.ToString(CultureInfo.InvariantCulture),
            totalScrollAmount.ToString("F4", CultureInfo.InvariantCulture),
            wrongDisplayScrollAmount.ToString("F4", CultureInfo.InvariantCulture),
            trialStartTime.ToString("F4", CultureInfo.InvariantCulture),
            clickTime.ToString("F4", CultureInfo.InvariantCulture),
            DateTime.Now.ToString("O", CultureInfo.InvariantCulture)));
        resultsWriter.Flush();
    }

    private void WriteEventRow(
        string eventType,
        TrialConfig trial,
        string actualDisplayId,
        float scrollAmount,
        bool wrongDisplay,
        float eventTime)
    {
        if (eventsWriter == null)
        {
            return;
        }

        eventsWriter.WriteLine(string.Join(",",
            Csv(participantId),
            Csv(sessionId),
            currentPhase.ToString(),
            activeCondition.ToString(),
            trial.TrialIndex.ToString(CultureInfo.InvariantCulture),
            Csv(eventType),
            Csv(trial.TargetDisplayId),
            Csv(actualDisplayId),
            scrollAmount.ToString("F4", CultureInfo.InvariantCulture),
            wrongDisplay ? "1" : "0",
            eventTime.ToString("F4", CultureInfo.InvariantCulture),
            DateTime.Now.ToString("O", CultureInfo.InvariantCulture)));
        eventsWriter.Flush();
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

        if (displayA == null || displayB == null)
        {
            DisplaySurface[] displays = FindObjectsOfType<DisplaySurface>();
            if (displayA == null && displays.Length > 0)
            {
                displayA = displays[0];
            }

            if (displayB == null && displays.Length > 1)
            {
                displayB = displays[1];
            }
        }
    }

    private DisplaySurface FindDisplay(string displayId)
    {
        if (displayA != null && displayA.name == displayId)
        {
            return displayA;
        }

        if (displayB != null && displayB.name == displayId)
        {
            return displayB;
        }

        return null;
    }

    private static string BuildItemId(int index)
    {
        int normalizedIndex = Mathf.Max(0, index);
        char group = (char)('A' + normalizedIndex / 10);
        int number = normalizedIndex % 10 + 1;
        return group + "-" + number.ToString("00", CultureInfo.InvariantCulture);
    }

    private static string SanitizeFilePart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        char[] invalid = Path.GetInvalidFileNameChars();
        string result = value.Trim();
        for (int index = 0; index < invalid.Length; index++)
        {
            result = result.Replace(invalid[index], '_');
        }

        return result;
    }

    private static string Csv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
