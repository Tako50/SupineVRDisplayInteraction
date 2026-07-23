using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
/// <summary>
/// VR内の条件選択、T1のTraining/Main開始、T2統合タスク開始、End/Abortボタンを生成する。
/// 条件選択中はディスプレイ内メニュー、タスク中は誤操作しにくい外部ボタンとして扱う。
/// </summary>
public class VRTaskMenuManager : MonoBehaviour
{
    private const string RaycastingMethodLabel = "Raycasting";
    private const string GazeRayMethodLabel = "Gaze + Ray";
    private const string GazeJoystickMethodLabel = "Gaze + Joystick";

    private enum PrototypeTask
    {
        T1FocusPointing,
        T2WebBrowsing
    }

    private enum MenuAction
    {
        SelectT1FocusPointing,
        SelectT2WebBrowsing,
        SelectRaycastBaseline,
        SelectGazeRay,
        SelectExplicitDisplayFocus,
        SelectUpDownDepth,
        SelectLeftRight,
        SelectUpDown,
        BeginTraining,
        BeginMain,
        EndTraining,
        AbortMainTask
    }

    private struct MenuButton
    {
        public MenuAction Action;
        public Rect NormalizedRect;
        public GameObject Root;
        public Image Image;
        public Text Text;
        public BoxCollider WorldCollider;
    }

    private struct MenuButtonSpec
    {
        public MenuAction Action;
        public string Label;
        public Rect NormalizedRect;

        public MenuButtonSpec(MenuAction action, string label, Rect normalizedRect)
        {
            Action = action;
            Label = label;
            NormalizedRect = normalizedRect;
        }
    }

    private static readonly MenuButtonSpec[] SelectionButtonSpecs =
    {
        new MenuButtonSpec(MenuAction.SelectT1FocusPointing, "T1 Target Selection", new Rect(0.06f, 0.650f, 0.42f, 0.085f)),
        new MenuButtonSpec(MenuAction.SelectT2WebBrowsing, "T2 Web Browsing", new Rect(0.52f, 0.650f, 0.42f, 0.085f)),
        new MenuButtonSpec(MenuAction.SelectRaycastBaseline, RaycastingMethodLabel, new Rect(0.06f, 0.455f, 0.27f, 0.085f)),
        new MenuButtonSpec(MenuAction.SelectGazeRay, GazeRayMethodLabel, new Rect(0.365f, 0.455f, 0.27f, 0.085f)),
        new MenuButtonSpec(MenuAction.SelectExplicitDisplayFocus, GazeJoystickMethodLabel, new Rect(0.67f, 0.455f, 0.27f, 0.085f))
    };

    private static readonly MenuButtonSpec[] LayoutButtonSpecs =
    {
        new MenuButtonSpec(MenuAction.SelectUpDownDepth, "A  Left / Right", new Rect(0.06f, 0.270f, 0.28f, 0.060f)),
        new MenuButtonSpec(MenuAction.SelectLeftRight, "B  Up / Down", new Rect(0.36f, 0.270f, 0.28f, 0.060f)),
        new MenuButtonSpec(MenuAction.SelectUpDown, "C  Up / Down + Depth", new Rect(0.66f, 0.270f, 0.28f, 0.060f))
    };

    private static readonly MenuButtonSpec[] TaskControlButtonSpecs =
    {
        new MenuButtonSpec(MenuAction.BeginTraining, "START TRAINING", new Rect(0.06f, 0.045f, 0.42f, 0.105f)),
        new MenuButtonSpec(MenuAction.BeginMain, "START MAIN TASK", new Rect(0.52f, 0.045f, 0.42f, 0.105f)),
        new MenuButtonSpec(MenuAction.EndTraining, "End Training", new Rect(0.68f, 1.34f, 0.28f, 0.09f)),
        new MenuButtonSpec(MenuAction.AbortMainTask, "Abort Main", new Rect(0.72f, 1.34f, 0.24f, 0.08f))
    };

    private static readonly Rect T1TrainingStartRect = new Rect(0.06f, 0.045f, 0.42f, 0.105f);
    private static readonly Rect T1MainStartRect = new Rect(0.52f, 0.045f, 0.42f, 0.105f);
    private static readonly Rect T2UnifiedStartRect = new Rect(0.06f, 0.045f, 0.88f, 0.105f);

    [Header("References")]
    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private ExperimentManager experimentManager;
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private FocusPointingTaskManager focusPointingTaskManager;
    [SerializeField] private WebViewSessionManager webViewSessionManager;
    [SerializeField] private RaycastPointer raycastPointer;
    [SerializeField] private GazeProvider gazeProvider;
    [SerializeField] private GazeDisplayFocusManager gazeDisplayFocusManager;

    [Header("Menu")]
    [SerializeField] private string menuDisplayId = "Display_B_Back";
    [SerializeField] private bool showMenuInConditionSelection = true;
    [SerializeField] private bool showLayoutButtons = false;
    [SerializeField] private bool applySelectedLayout = false;
    [SerializeField] private Color panelColor = new Color(0.025f, 0.035f, 0.055f, 0.96f);
    [SerializeField] private Color statusCardColor = new Color(0.06f, 0.09f, 0.14f, 0.98f);
    [SerializeField] private Color buttonColor = new Color(0.10f, 0.14f, 0.20f, 0.98f);
    [SerializeField] private Color selectedButtonColor = new Color(0.08f, 0.46f, 0.78f, 1f);
    [SerializeField] private Color startButtonColor = new Color(0.08f, 0.62f, 0.40f, 1f);
    [SerializeField] private Color stopButtonColor = new Color(0.78f, 0.24f, 0.22f, 1f);
    [SerializeField] private Color hoveredButtonColor = new Color(0.24f, 0.66f, 0.95f, 1f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color secondaryTextColor = new Color(0.66f, 0.76f, 0.86f, 1f);
    [SerializeField] private Color outlineColor = new Color(0.28f, 0.42f, 0.56f, 0.72f);

    private readonly List<MenuButton> buttons = new List<MenuButton>();
    private RectTransform menuRoot;
    private Image menuPanel;
    private GameObject selectionChrome;
    private GameObject layoutSection;
    private Text layoutSectionText;
    private Text statusText;
    private InteractionCondition selectedCondition = InteractionCondition.RaycastBaseline;
    private DisplayLayoutPreset selectedLayout = DisplayLayoutPreset.UpDownDepth;
    private PrototypeTask selectedTask = PrototypeTask.T1FocusPointing;
    private T2ContentSet selectedT2ContentSet = T2ContentSet.ACampGear2024;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();
        SyncSelectionsFromManagers();
        EnsureMenu();
        ApplySelectionsToManagers();
        SetConditionSelectionContentMode();
    }

    private void Update()
    {
        ResolveReferences();
        EnsureMenu();
        SyncConditionFromInput();
        UpdateVisibility();
        UpdateVisualState();
        SetConditionSelectionContentMode();
    }

    public bool TryHandleClick(string displayId, Vector2 normalizedPosition)
    {
        if (!IsMenuVisible() || displayId != menuDisplayId)
        {
            return false;
        }

        if (!TryConvertDisplayToMenuNormalized(normalizedPosition, out Vector2 menuNormalizedPosition))
        {
            return false;
        }

        for (int i = 0; i < buttons.Count; i++)
        {
            MenuButton button = buttons[i];
            if (button.Root == null || !button.Root.activeInHierarchy)
            {
                continue;
            }

            if (!button.NormalizedRect.Contains(menuNormalizedPosition))
            {
                continue;
            }

            ExecuteAction(button.Action);
            return true;
        }

        return false;
    }

    public bool TryHandleWorldClick(Ray ray, float maxDistance = 10f)
    {
        // End Training / Abort Mainはディスプレイ外に置くため、表示内正規化座標ではなくColliderで判定する。
        if (!IsMenuVisible() || ray.direction == Vector3.zero)
        {
            return false;
        }

        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            return false;
        }

        for (int i = 0; i < buttons.Count; i++)
        {
            MenuButton button = buttons[i];
            if (button.Root == null || !button.Root.activeInHierarchy || button.WorldCollider == null)
            {
                continue;
            }

            if (hit.collider != button.WorldCollider)
            {
                continue;
            }

            ExecuteAction(button.Action);
            return true;
        }

        return false;
    }

    private void ExecuteAction(MenuAction action)
    {
        switch (action)
        {
            case MenuAction.SelectT1FocusPointing:
                selectedTask = PrototypeTask.T1FocusPointing;
                break;
            case MenuAction.SelectT2WebBrowsing:
                selectedTask = PrototypeTask.T2WebBrowsing;
                break;
            case MenuAction.SelectRaycastBaseline:
                selectedCondition = InteractionCondition.RaycastBaseline;
                ApplySelectionsToManagers();
                break;
            case MenuAction.SelectGazeRay:
                selectedCondition = InteractionCondition.GazeRay;
                ApplySelectionsToManagers();
                break;
            case MenuAction.SelectExplicitDisplayFocus:
                selectedCondition = InteractionCondition.ExplicitDisplayFocus;
                ApplySelectionsToManagers();
                break;
            case MenuAction.SelectUpDownDepth:
                selectedLayout = DisplayLayoutPreset.LeftRight;
                ApplySelectionsToManagers();
                break;
            case MenuAction.SelectLeftRight:
                selectedLayout = DisplayLayoutPreset.UpDown;
                ApplySelectionsToManagers();
                break;
            case MenuAction.SelectUpDown:
                selectedLayout = DisplayLayoutPreset.UpDownDepth;
                ApplySelectionsToManagers();
                break;
            case MenuAction.BeginTraining:
                if (!CanStartSelectedCondition())
                {
                    break;
                }
                ApplySelectionsToManagers();
                if (selectedTask == PrototypeTask.T2WebBrowsing)
                {
                    webViewSessionManager?.BeginTrainingTask();
                }
                else
                {
                    focusPointingTaskManager?.BeginTrainingTask();
                }
                break;
            case MenuAction.BeginMain:
                if (!CanStartSelectedCondition())
                {
                    break;
                }
                ApplySelectionsToManagers();
                if (selectedTask == PrototypeTask.T2WebBrowsing)
                {
                    webViewSessionManager?.BeginUnifiedTask();
                }
                else
                {
                    focusPointingTaskManager?.BeginMainTask();
                }
                break;
            case MenuAction.EndTraining:
                if (focusPointingTaskManager != null
                    && focusPointingTaskManager.CurrentPhase == FocusPointingTaskPhase.Training
                    && !focusPointingTaskManager.CanEndTraining)
                {
                    break;
                }
                ReturnActiveTaskToSelection();
                break;
            case MenuAction.AbortMainTask:
                ReturnActiveTaskToSelection();
                break;
        }

        UpdateVisualState();
        Debug.Log(
            $"[VRTaskMenu] action={action}, task={selectedTask}, condition={selectedCondition}, "
            + $"highlightEnabled=True, baselineRay=Long, explicitRay=Off, "
            + $"layout={selectedLayout}, t2ContentSet={selectedT2ContentSet}");
    }

    private void ApplySelectionsToManagers()
    {
        if (focusPointingTaskManager != null)
        {
            focusPointingTaskManager.SetSelectedCondition(selectedCondition);
            focusPointingTaskManager.SetSelectedLayout(selectedLayout);
        }

        if (webViewSessionManager != null)
        {
            selectedT2ContentSet = T2ContentSet.ACampGear2024;
            webViewSessionManager.SetSelectedCondition(selectedCondition);
            webViewSessionManager.SetSelectedLayout(DisplayLayoutPreset.UpDownDepth);
            webViewSessionManager.SetSelectedContentSet(T2ContentSet.ACampGear2024);
        }

        if (inputManager != null && !inputManager.IsConditionLocked)
        {
            inputManager.SetCondition(selectedCondition);
        }

        if (experimentManager != null)
        {
            experimentManager.SetHighlightEnabled(true);
            experimentManager.ApplyRayVisualForCurrentCondition();
        }
        else
        {
            gazeDisplayFocusManager?.SetHighlightEnabled(true);
            raycastPointer?.SetRayVisualSettings(
                selectedCondition == InteractionCondition.RaycastBaseline
                    || selectedCondition == InteractionCondition.GazeRay,
                RayVisualLengthLevel.Long);
        }

        if (applySelectedLayout && experimentManager != null && (inputManager == null || !inputManager.IsConditionLocked))
        {
            experimentManager.ApplyLayout(selectedTask == PrototypeTask.T2WebBrowsing
                ? DisplayLayoutPreset.UpDownDepth
                : selectedLayout);
        }
    }

    private void SyncSelectionsFromManagers()
    {
        if (inputManager != null)
        {
            selectedCondition = inputManager.CurrentCondition;
        }

        if (experimentManager != null)
        {
            selectedLayout = focusPointingTaskManager != null
                ? focusPointingTaskManager.SelectedLayout
                : experimentManager.CurrentLayout;
        }

        selectedT2ContentSet = T2ContentSet.ACampGear2024;
    }

    private void SyncConditionFromInput()
    {
        if (inputManager == null || inputManager.IsConditionLocked || IsTaskRunning())
        {
            return;
        }

        if (selectedCondition != inputManager.CurrentCondition)
        {
            selectedCondition = inputManager.CurrentCondition;
            if (focusPointingTaskManager != null)
            {
                focusPointingTaskManager.SetSelectedCondition(selectedCondition);
            }

            webViewSessionManager?.SetSelectedCondition(selectedCondition);
        }
    }

    private void EnsureMenu()
    {
        if (menuRoot != null)
        {
            RestoreDecorationBindings();
            if (buttons.Count == 0)
            {
                RestoreButtonBindings();
            }

            return;
        }

        if (displayManager == null)
        {
            return;
        }

        DisplaySurface display = FindMenuDisplay();
        if (display == null || display.WorldSpaceCanvas == null)
        {
            return;
        }

        GameObject rootObject = new GameObject("VR_ConditionTaskMenu", typeof(RectTransform));
        rootObject.transform.SetParent(display.WorldSpaceCanvas.transform, false);
        menuRoot = rootObject.GetComponent<RectTransform>();
        menuRoot.SetAsLastSibling();
        menuRoot.anchorMin = new Vector2(0.5f, 0.5f);
        menuRoot.anchorMax = new Vector2(0.5f, 0.5f);
        menuRoot.pivot = new Vector2(0.5f, 0.5f);
        menuRoot.anchoredPosition = Vector2.zero;
        menuRoot.sizeDelta = display.GetCanvasSize() * 0.92f;

        menuPanel = rootObject.AddComponent<Image>();
        menuPanel.color = panelColor;
        menuPanel.raycastTarget = false;
        AddOutline(rootObject, outlineColor, new Vector2(2f, -2f));

        RectTransform chrome = CreateRectChild(menuRoot, "SelectionChrome", new Rect(0f, 0f, 1f, 1f));
        selectionChrome = chrome.gameObject;

        Text title = CreateText(
            chrome,
            "Title",
            "EXPERIMENT SETUP",
            new Rect(0.06f, 0.91f, 0.88f, 0.07f),
            20,
            TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold;

        RectTransform statusCard = CreatePanel(
            chrome,
            "StatusCard",
            new Rect(0.06f, 0.80f, 0.88f, 0.105f),
            statusCardColor);
        statusText = CreateText(
            statusCard,
            "Status",
            "Select a task and interaction method",
            new Rect(0.025f, 0.08f, 0.95f, 0.84f),
            14,
            TextAnchor.MiddleLeft);

        CreateSectionLabel(chrome, "TaskSection", "TASK", new Rect(0.06f, 0.745f, 0.88f, 0.035f));
        CreateSectionLabel(chrome, "MethodSection", "INTERACTION METHOD", new Rect(0.06f, 0.550f, 0.88f, 0.035f));
        layoutSectionText = CreateSectionLabel(
            chrome,
            "LayoutSection",
            "DISPLAY LAYOUT",
            new Rect(0.06f, 0.340f, 0.88f, 0.035f));
        layoutSection = layoutSectionText.gameObject;
        CreateSectionLabel(chrome, "RunSection", "BEGIN SESSION", new Rect(0.06f, 0.170f, 0.88f, 0.035f));

        BuildButtonBindings(true);

        UpdateVisibility();
        UpdateVisualState();
    }

    private void RestoreDecorationBindings()
    {
        if (menuRoot == null)
        {
            return;
        }

        if (menuPanel == null)
        {
            menuPanel = menuRoot.GetComponent<Image>();
        }

        Transform chrome = menuRoot.Find("SelectionChrome");
        if (chrome == null)
        {
            return;
        }

        selectionChrome = chrome.gameObject;
        statusText = statusText != null ? statusText : chrome.Find("StatusCard/Status")?.GetComponent<Text>();
        layoutSection = chrome.Find("LayoutSection")?.gameObject;
        layoutSectionText = layoutSection != null ? layoutSection.GetComponent<Text>() : null;
    }

    private void RestoreButtonBindings()
    {
        BuildButtonBindings(false);
    }

    private void BuildButtonBindings(bool createObjects)
    {
        buttons.Clear();
        AddButtonSpecs(SelectionButtonSpecs, createObjects);
        if (showLayoutButtons)
        {
            AddButtonSpecs(LayoutButtonSpecs, createObjects);
        }

        AddButtonSpecs(TaskControlButtonSpecs, createObjects);
    }

    private void AddButtonSpecs(MenuButtonSpec[] specs, bool createObjects)
    {
        for (int i = 0; i < specs.Length; i++)
        {
            MenuButtonSpec spec = specs[i];
            if (createObjects)
            {
                CreateButton(spec.Action, spec.Label, spec.NormalizedRect);
            }
            else
            {
                AddExistingButton(spec.Action, spec.Label, spec.NormalizedRect);
            }
        }
    }

    private void AddExistingButton(MenuAction action, string objectName, Rect normalizedRect)
    {
        Transform child = menuRoot.Find(objectName);
        if (child == null)
        {
            return;
        }

        buttons.Add(new MenuButton
        {
            Action = action,
            NormalizedRect = normalizedRect,
            Root = child.gameObject,
            Image = child.GetComponent<Image>(),
            Text = child.GetComponentInChildren<Text>(true),
            WorldCollider = child.GetComponent<BoxCollider>()
        });
    }

    private void CreateButton(MenuAction action, string label, Rect normalizedRect)
    {
        GameObject buttonObject = new GameObject(label, typeof(RectTransform));
        buttonObject.transform.SetParent(menuRoot, false);
        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        ApplyNormalizedRect(rectTransform, normalizedRect);

        Image image = buttonObject.AddComponent<Image>();
        image.color = buttonColor;
        AddOutline(buttonObject, outlineColor, new Vector2(1f, -1f));

        Text text = CreateText(
            rectTransform,
            "Label",
            label,
            new Rect(0.025f, 0.04f, 0.95f, 0.92f),
            GetButtonFontSize(action),
            TextAnchor.MiddleCenter);
        text.fontStyle = FontStyle.Bold;
        BoxCollider worldCollider = ShouldUseWorldCollider(action) ? ConfigureWorldCollider(buttonObject, normalizedRect) : null;

        buttons.Add(new MenuButton
        {
            Action = action,
            NormalizedRect = normalizedRect,
            Root = buttonObject,
            Image = image,
            Text = text,
            WorldCollider = worldCollider
        });
    }

    private BoxCollider ConfigureWorldCollider(GameObject buttonObject, Rect normalizedRect)
    {
        BoxCollider boxCollider = buttonObject.AddComponent<BoxCollider>();
        boxCollider.isTrigger = true;

        Vector2 rootSize = menuRoot != null ? menuRoot.sizeDelta : Vector2.one;
        boxCollider.size = new Vector3(
            Mathf.Max(1f, rootSize.x * normalizedRect.width),
            Mathf.Max(1f, rootSize.y * normalizedRect.height),
            20f);
        boxCollider.center = Vector3.zero;
        return boxCollider;
    }

    private RectTransform CreatePanel(RectTransform parent, string name, Rect normalizedRect, Color color)
    {
        RectTransform panel = CreateRectChild(parent, name, normalizedRect);
        Image image = panel.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        AddOutline(panel.gameObject, outlineColor, new Vector2(1f, -1f));
        return panel;
    }

    private Text CreateSectionLabel(RectTransform parent, string name, string text, Rect normalizedRect)
    {
        Text section = CreateText(parent, name, text, normalizedRect, 12, TextAnchor.MiddleLeft);
        section.color = secondaryTextColor;
        section.fontStyle = FontStyle.Bold;
        return section;
    }

    private Text CreateText(RectTransform parent, string name, string text, Rect normalizedRect, int fontSize, TextAnchor alignment)
    {
        RectTransform rectTransform = CreateRectChild(parent, name, normalizedRect);

        Text uiText = rectTransform.gameObject.AddComponent<Text>();
        uiText.text = text;
        uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        uiText.fontSize = fontSize;
        uiText.alignment = alignment;
        uiText.color = textColor;
        uiText.horizontalOverflow = HorizontalWrapMode.Wrap;
        uiText.verticalOverflow = VerticalWrapMode.Truncate;
        uiText.raycastTarget = false;
        return uiText;
    }

    private static RectTransform CreateRectChild(RectTransform parent, string name, Rect normalizedRect)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        RectTransform rectTransform = child.GetComponent<RectTransform>();
        ApplyNormalizedRect(rectTransform, normalizedRect);
        return rectTransform;
    }

    private static void AddOutline(GameObject target, Color color, Vector2 distance)
    {
        Outline outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    private static int GetButtonFontSize(MenuAction action)
    {
        switch (action)
        {
            case MenuAction.SelectT1FocusPointing:
            case MenuAction.SelectT2WebBrowsing:
            case MenuAction.SelectGazeRay:
            case MenuAction.SelectExplicitDisplayFocus:
                return 19;
            case MenuAction.SelectUpDownDepth:
            case MenuAction.SelectLeftRight:
            case MenuAction.SelectUpDown:
            default:
                return 21;
        }
    }

    private static void ApplyNormalizedRect(RectTransform rectTransform, Rect normalizedRect)
    {
        rectTransform.anchorMin = new Vector2(normalizedRect.xMin, normalizedRect.yMin);
        rectTransform.anchorMax = new Vector2(normalizedRect.xMax, normalizedRect.yMax);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    private bool TryConvertDisplayToMenuNormalized(
        Vector2 displayNormalizedPosition,
        out Vector2 menuNormalizedPosition)
    {
        menuNormalizedPosition = Vector2.zero;
        DisplaySurface display = FindMenuDisplay();
        if (display == null || display.WorldSpaceCanvas == null || menuRoot == null)
        {
            return false;
        }

        RectTransform canvasRect = display.WorldSpaceCanvas.GetComponent<RectTransform>();
        if (canvasRect == null || menuRoot.rect.width <= 0f || menuRoot.rect.height <= 0f)
        {
            return false;
        }

        Vector2 canvasPoint = display.NormalizedToCanvasPosition(displayNormalizedPosition);
        Vector3 worldPoint = canvasRect.TransformPoint(canvasPoint);
        Vector2 menuPoint = menuRoot.InverseTransformPoint(worldPoint);

        menuNormalizedPosition = new Vector2(
            Mathf.InverseLerp(menuRoot.rect.xMin, menuRoot.rect.xMax, menuPoint.x),
            Mathf.InverseLerp(menuRoot.rect.yMin, menuRoot.rect.yMax, menuPoint.y));
        return true;
    }

    private void UpdateVisibility()
    {
        if (menuRoot != null)
        {
            menuRoot.gameObject.SetActive(IsMenuVisible());
            if (menuRoot.gameObject.activeSelf)
            {
                menuRoot.SetAsLastSibling();
                if (displayManager != null && IsConditionSelectionVisible())
                {
                    displayManager.SetAllDisplayContentMode(DisplayContentMode.ConditionSelection);
                }
            }
        }
    }

    private bool IsMenuVisible()
    {
        // タスク中の終了操作は左側の専用ExitPanelControllerへ分離する。
        return showMenuInConditionSelection && IsConditionSelectionVisible();
    }

    public bool IsTaskRunning()
    {
        return (focusPointingTaskManager != null
                && focusPointingTaskManager.CurrentPhase != FocusPointingTaskPhase.ConditionSelection)
            || (webViewSessionManager != null
                && webViewSessionManager.CurrentPhase != WebViewSessionPhase.ConditionSelection);
    }

    private bool IsConditionSelectionVisible()
    {
        bool t1Idle = focusPointingTaskManager == null
            || focusPointingTaskManager.CurrentPhase == FocusPointingTaskPhase.ConditionSelection;
        bool webIdle = webViewSessionManager == null
            || webViewSessionManager.CurrentPhase == WebViewSessionPhase.ConditionSelection;
        return t1Idle && webIdle;
    }

    private bool IsTrainingVisible()
    {
        return (focusPointingTaskManager != null
                && focusPointingTaskManager.CurrentPhase == FocusPointingTaskPhase.Training)
            || (webViewSessionManager != null
                && webViewSessionManager.CurrentPhase == WebViewSessionPhase.Training);
    }

    private bool IsMainTaskVisible()
    {
        return (focusPointingTaskManager != null
                && focusPointingTaskManager.CurrentPhase == FocusPointingTaskPhase.MainTask)
            || (webViewSessionManager != null
                && webViewSessionManager.CurrentPhase == WebViewSessionPhase.MainTask);
    }

    private void SetConditionSelectionContentMode()
    {
        if (displayManager == null || !IsConditionSelectionVisible() || !IsMenuVisible())
        {
            return;
        }

        displayManager.SetAllDisplayContentMode(DisplayContentMode.ConditionSelection);
    }

    private void UpdateVisualState()
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            MenuButton button = buttons[i];
            if (button.Action == MenuAction.BeginTraining)
            {
                SetButtonRect(ref button, T1TrainingStartRect);
                if (button.Text != null)
                {
                    button.Text.text = "START TRAINING";
                }
            }
            else if (button.Action == MenuAction.BeginMain)
            {
                bool unifiedT2 = selectedTask == PrototypeTask.T2WebBrowsing;
                SetButtonRect(ref button, unifiedT2 ? T2UnifiedStartRect : T1MainStartRect);
                if (button.Text != null)
                {
                    button.Text.text = unifiedT2 ? "START T2 TASK" : "START MAIN TASK";
                }
            }

            bool shouldShow = ShouldShowButton(button.Action);
            if (button.Root != null)
            {
                button.Root.SetActive(shouldShow);
            }

            bool selected = IsSelected(button.Action);
            bool start = button.Action == MenuAction.BeginTraining || button.Action == MenuAction.BeginMain;
            bool stop = button.Action == MenuAction.AbortMainTask;
            if (button.Image != null)
            {
                bool hovered = IsWorldButtonHovered(button);
                button.Image.color = hovered
                    ? hoveredButtonColor
                    : selected
                        ? selectedButtonColor
                        : stop
                            ? stopButtonColor
                            : start || button.Action == MenuAction.EndTraining
                                ? startButtonColor
                                : buttonColor;
            }

            buttons[i] = button;
        }

        bool selectionVisible = IsConditionSelectionVisible();
        if (selectionChrome != null)
        {
            selectionChrome.SetActive(selectionVisible);
        }

        if (layoutSection != null)
        {
            layoutSection.SetActive(selectionVisible
                && selectedTask == PrototypeTask.T1FocusPointing
                && showLayoutButtons
                && !(focusPointingTaskManager?.UsesFixedMainTaskSequence ?? false));
        }

        if (layoutSectionText != null)
        {
            layoutSectionText.text = "T1 TASK";
        }

        if (statusText != null)
        {
            string orderSummary = selectedTask == PrototypeTask.T1FocusPointing
                ? $"  |  {focusPointingTaskManager?.GetTargetOrderSummary(selectedCondition) ?? "Main List A / Training List G"}"
                : $"  |  {GetT2ContentSetDisplayName(selectedT2ContentSet)}";
            DisplayLayoutPreset activeLayout = selectedTask == PrototypeTask.T2WebBrowsing
                ? DisplayLayoutPreset.UpDownDepth
                : selectedLayout;
            string layoutLabel = selectedTask == PrototypeTask.T1FocusPointing
                && (focusPointingTaskManager?.UsesFixedMainTaskSequence ?? false)
                    ? "Task A → B → C"
                    : GetLayoutDisplayName(activeLayout);
            statusText.text =
                $"{GetTaskDisplayName(selectedTask)}  |  {GetConditionDisplayName(selectedCondition)}" +
                $"{orderSummary}  |  {layoutLabel}\n" +
                $"Visual feedback: Highlight ON  |  Controller Ray " +
                $"{(selectedCondition == InteractionCondition.ExplicitDisplayFocus ? "OFF" : "Long")}";
        }

        if (menuPanel != null)
        {
            menuPanel.enabled = selectionVisible;
        }
    }

    private static string GetTaskDisplayName(PrototypeTask task)
    {
        return task == PrototypeTask.T2WebBrowsing
            ? "T2 Web Browsing"
            : "T1 Target Selection";
    }

    private static string GetConditionDisplayName(InteractionCondition condition)
    {
        switch (condition)
        {
            case InteractionCondition.GazeRay:
                return GazeRayMethodLabel;
            case InteractionCondition.ExplicitDisplayFocus:
                return GazeJoystickMethodLabel;
            default:
                return RaycastingMethodLabel;
        }
    }

    private static string GetT2ContentSetDisplayName(T2ContentSet contentSet)
    {
        return "2024 Content";
    }

    private static void SetButtonRect(ref MenuButton button, Rect normalizedRect)
    {
        button.NormalizedRect = normalizedRect;
        if (button.Root != null)
        {
            ApplyNormalizedRect(button.Root.GetComponent<RectTransform>(), normalizedRect);
        }
    }

    private static string GetLayoutDisplayName(DisplayLayoutPreset layout)
    {
        switch (layout)
        {
            case DisplayLayoutPreset.LeftRight:
                return "Task A";
            case DisplayLayoutPreset.UpDown:
                return "Task B";
            case DisplayLayoutPreset.UpDownDepth:
                return "Task C";
            default:
                return "Legacy Up/Down + Depth";
        }
    }

    private bool ShouldShowButton(MenuAction action)
    {
        if (IsTrainingVisible())
        {
            if (action != MenuAction.EndTraining)
            {
                return false;
            }

            return focusPointingTaskManager == null
                || focusPointingTaskManager.CurrentPhase != FocusPointingTaskPhase.Training
                || focusPointingTaskManager.CanEndTraining;
        }

        if (IsMainTaskVisible())
        {
            return action == MenuAction.AbortMainTask;
        }

        if (IsConditionSelectionVisible())
        {
            if (action == MenuAction.EndTraining || action == MenuAction.AbortMainTask)
            {
                return false;
            }

            if (action == MenuAction.BeginTraining && selectedTask == PrototypeTask.T2WebBrowsing)
            {
                return false;
            }

            if (action == MenuAction.SelectUpDownDepth
                || action == MenuAction.SelectLeftRight
                || action == MenuAction.SelectUpDown)
            {
                return selectedTask == PrototypeTask.T1FocusPointing
                    && showLayoutButtons
                    && !(focusPointingTaskManager?.UsesFixedMainTaskSequence ?? false);
            }

            return true;
        }

        return false;
    }

    private static bool ShouldUseWorldCollider(MenuAction action)
    {
        return action == MenuAction.EndTraining || action == MenuAction.AbortMainTask;
    }

    private bool IsWorldButtonHovered(MenuButton button)
    {
        if (button.Root == null
            || !button.Root.activeInHierarchy
            || button.WorldCollider == null)
        {
            return false;
        }

        if (!TryGetHoverRay(out Ray ray))
        {
            return false;
        }

        return ray.direction != Vector3.zero
            && Physics.Raycast(ray, out RaycastHit hit, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)
            && hit.collider == button.WorldCollider;
    }

    private bool TryGetHoverRay(out Ray ray)
    {
        if (inputManager != null
            && inputManager.CurrentCondition == InteractionCondition.ExplicitDisplayFocus
            && gazeProvider != null)
        {
            ray = gazeProvider.GetGazeRay();
            return ray.direction != Vector3.zero;
        }

        if (raycastPointer != null)
        {
            ray = raycastPointer.CurrentRay;
            return ray.direction != Vector3.zero;
        }

        ray = default;
        return false;
    }

    private bool IsSelected(MenuAction action)
    {
        return (action == MenuAction.SelectT1FocusPointing && selectedTask == PrototypeTask.T1FocusPointing)
            || (action == MenuAction.SelectT2WebBrowsing && selectedTask == PrototypeTask.T2WebBrowsing)
            || (action == MenuAction.SelectRaycastBaseline && selectedCondition == InteractionCondition.RaycastBaseline)
            || (action == MenuAction.SelectGazeRay && selectedCondition == InteractionCondition.GazeRay)
            || (action == MenuAction.SelectExplicitDisplayFocus && selectedCondition == InteractionCondition.ExplicitDisplayFocus)
            || (action == MenuAction.SelectUpDownDepth && selectedLayout == DisplayLayoutPreset.LeftRight)
            || (action == MenuAction.SelectLeftRight && selectedLayout == DisplayLayoutPreset.UpDown)
            || (action == MenuAction.SelectUpDown && selectedLayout == DisplayLayoutPreset.UpDownDepth);
    }

    private bool CanStartSelectedCondition()
    {
        if (selectedCondition != InteractionCondition.GazeRay)
        {
            return true;
        }

        if (gazeProvider == null)
        {
            Debug.LogError("[VRTaskMenu] GazeRay cannot start because GazeProvider is missing.");
            return false;
        }

        if (gazeProvider.ConfiguredGazeSource != GazeSource.EyeTracking
            || gazeProvider.IsEyeTrackingAvailable())
        {
            return true;
        }

        Debug.LogError("[VRTaskMenu] GazeRay experiment cannot start because Eye Tracking is unavailable. Select a development gaze source only for Editor debugging.");
        return false;
    }

    private DisplaySurface FindMenuDisplay()
    {
        if (displayManager == null || displayManager.Displays == null)
        {
            return null;
        }

        DisplaySurface fallback = null;
        for (int i = 0; i < displayManager.Displays.Length; i++)
        {
            DisplaySurface display = displayManager.Displays[i];
            if (display == null)
            {
                continue;
            }

            fallback = fallback == null ? display : fallback;
            if (display.name == menuDisplayId)
            {
                return display;
            }
        }

        return fallback;
    }

    private void ResolveReferences()
    {
        if (inputManager == null)
        {
            inputManager = PrototypeInputManager.Instance != null ? PrototypeInputManager.Instance : FindObjectOfType<PrototypeInputManager>();
        }

        if (experimentManager == null)
        {
            experimentManager = FindObjectOfType<ExperimentManager>();
        }

        if (displayManager == null)
        {
            displayManager = DisplayManager.Instance != null ? DisplayManager.Instance : FindObjectOfType<DisplayManager>();
        }

        if (focusPointingTaskManager == null)
        {
            focusPointingTaskManager = FindObjectOfType<FocusPointingTaskManager>();
        }

        if (webViewSessionManager == null)
        {
            webViewSessionManager = FindObjectOfType<WebViewSessionManager>();
        }

        if (raycastPointer == null)
        {
            raycastPointer = FindObjectOfType<RaycastPointer>();
        }

        if (gazeProvider == null)
        {
            gazeProvider = FindObjectOfType<GazeProvider>();
        }

        if (gazeDisplayFocusManager == null)
        {
            gazeDisplayFocusManager = FindObjectOfType<GazeDisplayFocusManager>();
        }
    }

    public void ReturnActiveTaskToSelection()
    {
        if (focusPointingTaskManager != null
            && focusPointingTaskManager.CurrentPhase != FocusPointingTaskPhase.ConditionSelection)
        {
            focusPointingTaskManager.ReturnToConditionSelection();
        }

        if (webViewSessionManager != null
            && webViewSessionManager.CurrentPhase != WebViewSessionPhase.ConditionSelection)
        {
            webViewSessionManager.ReturnToConditionSelection();
        }
    }
}
