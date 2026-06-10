using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
/// <summary>
/// VR内の条件選択・Training/Main開始・End/Abortボタンを生成する。
/// 条件選択中はディスプレイ内メニュー、タスク中は誤操作しにくい外部ボタンとして扱う。
/// </summary>
public class VRTaskMenuManager : MonoBehaviour
{
    private enum PrototypeTask
    {
        T1FocusPointing,
        T2AReferenceList
    }

    private enum MenuAction
    {
        SelectT1FocusPointing,
        SelectT2AReferenceList,
        SelectT1FixedOrder,
        SelectT1RandomOrder,
        SelectRaycastBaseline,
        SelectExplicitDisplayFocus,
        SelectNoOcclusion,
        SelectPartialOcclusion,
        SelectStrongOcclusion,
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

    [Header("References")]
    [SerializeField] private PrototypeInputManager inputManager;
    [SerializeField] private ExperimentManager experimentManager;
    [SerializeField] private DisplayManager displayManager;
    [SerializeField] private FocusPointingTaskManager focusPointingTaskManager;
    [SerializeField] private ReferenceListTaskManager referenceListTaskManager;
    [SerializeField] private RaycastPointer raycastPointer;
    [SerializeField] private GazeProvider gazeProvider;

    [Header("Menu")]
    [SerializeField] private string menuDisplayId = "Display_B_Back";
    [SerializeField] private bool showMenuInConditionSelection = true;
    [SerializeField] private bool showLayoutButtons = false;
    [SerializeField] private bool applySelectedLayout = false;
    [SerializeField] private Color panelColor = new Color(0.04f, 0.05f, 0.06f, 0.82f);
    [SerializeField] private Color buttonColor = new Color(0.16f, 0.18f, 0.20f, 0.92f);
    [SerializeField] private Color selectedButtonColor = new Color(0.15f, 0.48f, 0.85f, 0.95f);
    [SerializeField] private Color startButtonColor = new Color(0.10f, 0.70f, 0.32f, 0.95f);
    [SerializeField] private Color hoveredButtonColor = Color.yellow;
    [SerializeField] private Color textColor = Color.white;

    private readonly List<MenuButton> buttons = new List<MenuButton>();
    private RectTransform menuRoot;
    private Image menuPanel;
    private Text statusText;
    private InteractionCondition selectedCondition = InteractionCondition.RaycastBaseline;
    private DisplayLayoutPreset selectedLayout = DisplayLayoutPreset.StrongOcclusion;
    private PrototypeTask selectedTask = PrototypeTask.T1FocusPointing;
    private bool randomizeT1Trials;

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
            case MenuAction.SelectT2AReferenceList:
                selectedTask = PrototypeTask.T2AReferenceList;
                break;
            case MenuAction.SelectT1FixedOrder:
                randomizeT1Trials = false;
                ApplySelectionsToManagers();
                break;
            case MenuAction.SelectT1RandomOrder:
                randomizeT1Trials = true;
                ApplySelectionsToManagers();
                break;
            case MenuAction.SelectRaycastBaseline:
                selectedCondition = InteractionCondition.RaycastBaseline;
                ApplySelectionsToManagers();
                break;
            case MenuAction.SelectExplicitDisplayFocus:
                selectedCondition = InteractionCondition.ExplicitDisplayFocus;
                ApplySelectionsToManagers();
                break;
            case MenuAction.SelectNoOcclusion:
                selectedLayout = DisplayLayoutPreset.NoOcclusion;
                ApplySelectionsToManagers();
                break;
            case MenuAction.SelectPartialOcclusion:
                selectedLayout = DisplayLayoutPreset.PartialOcclusion;
                ApplySelectionsToManagers();
                break;
            case MenuAction.SelectStrongOcclusion:
                selectedLayout = DisplayLayoutPreset.StrongOcclusion;
                ApplySelectionsToManagers();
                break;
            case MenuAction.BeginTraining:
                ApplySelectionsToManagers();
                if (selectedTask == PrototypeTask.T2AReferenceList)
                {
                    referenceListTaskManager?.BeginTrainingTask();
                }
                else
                {
                    focusPointingTaskManager?.BeginTrainingTask();
                }
                break;
            case MenuAction.BeginMain:
                ApplySelectionsToManagers();
                if (selectedTask == PrototypeTask.T2AReferenceList)
                {
                    referenceListTaskManager?.BeginMainTask();
                }
                else
                {
                    focusPointingTaskManager?.BeginMainTask();
                }
                break;
            case MenuAction.EndTraining:
                ReturnActiveTaskToSelection();
                break;
            case MenuAction.AbortMainTask:
                ReturnActiveTaskToSelection();
                break;
        }

        UpdateVisualState();
        Debug.Log($"[VRTaskMenu] action={action}, task={selectedTask}, condition={selectedCondition}, layout={selectedLayout}");
    }

    private void ApplySelectionsToManagers()
    {
        if (focusPointingTaskManager != null)
        {
            focusPointingTaskManager.SetSelectedCondition(selectedCondition);
            focusPointingTaskManager.SetSelectedLayout(selectedLayout);
            focusPointingTaskManager.SetRandomizeTrialsWithinCondition(randomizeT1Trials);
        }

        if (referenceListTaskManager != null)
        {
            referenceListTaskManager.SetSelectedCondition(selectedCondition);
            referenceListTaskManager.SetSelectedLayout(selectedLayout);
        }

        if (inputManager != null && !inputManager.IsConditionLocked)
        {
            inputManager.SetCondition(selectedCondition);
        }

        if (applySelectedLayout && experimentManager != null && (inputManager == null || !inputManager.IsConditionLocked))
        {
            experimentManager.ApplyLayout(selectedLayout);
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
            selectedLayout = experimentManager.CurrentLayout;
        }

        if (focusPointingTaskManager != null)
        {
            randomizeT1Trials = focusPointingTaskManager.RandomizeTrialsWithinCondition;
        }
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

            referenceListTaskManager?.SetSelectedCondition(selectedCondition);
        }
    }

    private void EnsureMenu()
    {
        if (menuRoot != null)
        {
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

        RectTransform canvasRect = display.WorldSpaceCanvas.GetComponent<RectTransform>();
        Vector2 canvasSize = canvasRect != null ? canvasRect.sizeDelta : display.CanvasPixelSize;

        GameObject rootObject = new GameObject("VR_ConditionTaskMenu", typeof(RectTransform));
        rootObject.transform.SetParent(display.WorldSpaceCanvas.transform, false);
        menuRoot = rootObject.GetComponent<RectTransform>();
        menuRoot.SetAsLastSibling();
        menuRoot.anchorMin = new Vector2(0.5f, 0.5f);
        menuRoot.anchorMax = new Vector2(0.5f, 0.5f);
        menuRoot.pivot = new Vector2(0.5f, 0.5f);
        menuRoot.anchoredPosition = Vector2.zero;
        menuRoot.sizeDelta = canvasSize * 0.86f;

        menuPanel = rootObject.AddComponent<Image>();
        menuPanel.color = panelColor;

        statusText = CreateText(menuRoot, "Status", "Select condition and start task", new Rect(0.04f, 0.84f, 0.92f, 0.15f), 20, TextAnchor.MiddleLeft);

        CreateButton(MenuAction.SelectT1FocusPointing, "T1 Pointing", new Rect(0.08f, 0.72f, 0.40f, 0.10f));
        CreateButton(MenuAction.SelectT2AReferenceList, "T2-A Reference List", new Rect(0.52f, 0.72f, 0.40f, 0.10f));
        CreateButton(MenuAction.SelectRaycastBaseline, "RaycastBaseline", new Rect(0.08f, 0.55f, 0.40f, 0.11f));
        CreateButton(MenuAction.SelectExplicitDisplayFocus, "ExplicitDisplayFocus", new Rect(0.52f, 0.55f, 0.40f, 0.11f));
        CreateButton(MenuAction.SelectT1FixedOrder, "Fixed Order", new Rect(0.08f, 0.38f, 0.40f, 0.10f));
        CreateButton(MenuAction.SelectT1RandomOrder, "Random Order", new Rect(0.52f, 0.38f, 0.40f, 0.10f));
        if (showLayoutButtons)
        {
            CreateButton(MenuAction.SelectNoOcclusion, "NoOcclusion", new Rect(0.08f, 0.31f, 0.26f, 0.07f));
            CreateButton(MenuAction.SelectPartialOcclusion, "Partial", new Rect(0.37f, 0.31f, 0.26f, 0.07f));
            CreateButton(MenuAction.SelectStrongOcclusion, "Strong", new Rect(0.66f, 0.31f, 0.26f, 0.07f));
        }

        CreateButton(MenuAction.BeginTraining, "Start Training", new Rect(0.08f, 0.16f, 0.40f, 0.14f));
        CreateButton(MenuAction.BeginMain, "Start Main Task", new Rect(0.52f, 0.16f, 0.40f, 0.14f));
        CreateButton(MenuAction.EndTraining, "End Training", new Rect(0.68f, 1.34f, 0.28f, 0.09f));
        CreateButton(MenuAction.AbortMainTask, "Abort Main", new Rect(0.72f, 1.34f, 0.24f, 0.08f));

        UpdateVisibility();
        UpdateVisualState();
    }

    private void RestoreButtonBindings()
    {
        AddExistingButton(MenuAction.SelectT1FocusPointing, "T1 Pointing", new Rect(0.08f, 0.72f, 0.40f, 0.10f));
        AddExistingButton(MenuAction.SelectT2AReferenceList, "T2-A Reference List", new Rect(0.52f, 0.72f, 0.40f, 0.10f));
        AddExistingButton(MenuAction.SelectRaycastBaseline, "RaycastBaseline", new Rect(0.08f, 0.55f, 0.40f, 0.11f));
        AddExistingButton(MenuAction.SelectExplicitDisplayFocus, "ExplicitDisplayFocus", new Rect(0.52f, 0.55f, 0.40f, 0.11f));
        AddExistingButton(MenuAction.SelectT1FixedOrder, "Fixed Order", new Rect(0.08f, 0.38f, 0.40f, 0.10f));
        AddExistingButton(MenuAction.SelectT1RandomOrder, "Random Order", new Rect(0.52f, 0.38f, 0.40f, 0.10f));

        if (showLayoutButtons)
        {
            AddExistingButton(MenuAction.SelectNoOcclusion, "NoOcclusion", new Rect(0.08f, 0.31f, 0.26f, 0.07f));
            AddExistingButton(MenuAction.SelectPartialOcclusion, "Partial", new Rect(0.37f, 0.31f, 0.26f, 0.07f));
            AddExistingButton(MenuAction.SelectStrongOcclusion, "Strong", new Rect(0.66f, 0.31f, 0.26f, 0.07f));
        }

        AddExistingButton(MenuAction.BeginTraining, "Start Training", new Rect(0.08f, 0.16f, 0.40f, 0.14f));
        AddExistingButton(MenuAction.BeginMain, "Start Main Task", new Rect(0.52f, 0.16f, 0.40f, 0.14f));
        AddExistingButton(MenuAction.EndTraining, "End Training", new Rect(0.68f, 1.34f, 0.28f, 0.09f));
        AddExistingButton(MenuAction.AbortMainTask, "Abort Main", new Rect(0.72f, 1.34f, 0.24f, 0.08f));
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

        Text text = CreateText(rectTransform, "Label", label, new Rect(0f, 0f, 1f, 1f), 24, TextAnchor.MiddleCenter);
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

    private Text CreateText(RectTransform parent, string name, string text, Rect normalizedRect, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        ApplyNormalizedRect(rectTransform, normalizedRect);

        Text uiText = textObject.AddComponent<Text>();
        uiText.text = text;
        uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        uiText.fontSize = fontSize;
        uiText.alignment = alignment;
        uiText.color = textColor;
        uiText.horizontalOverflow = HorizontalWrapMode.Wrap;
        uiText.verticalOverflow = VerticalWrapMode.Truncate;
        return uiText;
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

        Vector2 canvasPoint = new Vector2(
            (Mathf.Clamp01(displayNormalizedPosition.x) - 0.5f) * canvasRect.rect.width,
            (Mathf.Clamp01(displayNormalizedPosition.y) - 0.5f) * canvasRect.rect.height);
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
        return showMenuInConditionSelection && (IsConditionSelectionVisible() || IsTrainingVisible() || IsMainTaskVisible());
    }

    private bool IsTaskRunning()
    {
        return (focusPointingTaskManager != null
                && focusPointingTaskManager.CurrentPhase != FocusPointingTaskPhase.ConditionSelection)
            || (referenceListTaskManager != null
                && referenceListTaskManager.CurrentPhase != ReferenceListTaskPhase.ConditionSelection);
    }

    private bool IsConditionSelectionVisible()
    {
        bool t1Idle = focusPointingTaskManager == null
            || focusPointingTaskManager.CurrentPhase == FocusPointingTaskPhase.ConditionSelection;
        bool t2Idle = referenceListTaskManager == null
            || referenceListTaskManager.CurrentPhase == ReferenceListTaskPhase.ConditionSelection;
        return t1Idle && t2Idle;
    }

    private bool IsTrainingVisible()
    {
        return (focusPointingTaskManager != null
                && focusPointingTaskManager.CurrentPhase == FocusPointingTaskPhase.Training)
            || (referenceListTaskManager != null
                && referenceListTaskManager.CurrentPhase == ReferenceListTaskPhase.Training);
    }

    private bool IsMainTaskVisible()
    {
        return (focusPointingTaskManager != null
                && focusPointingTaskManager.CurrentPhase == FocusPointingTaskPhase.MainTask)
            || (referenceListTaskManager != null
                && referenceListTaskManager.CurrentPhase == ReferenceListTaskPhase.MainTask);
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
            bool shouldShow = ShouldShowButton(button.Action);
            if (button.Root != null)
            {
                button.Root.SetActive(shouldShow);
            }

            bool selected = IsSelected(button.Action);
            bool start = button.Action == MenuAction.BeginTraining || button.Action == MenuAction.BeginMain;
            if (button.Image != null)
            {
                bool hovered = IsWorldButtonHovered(button);
                button.Image.color = hovered
                    ? hoveredButtonColor
                    : selected
                        ? selectedButtonColor
                        : start || button.Action == MenuAction.EndTraining || button.Action == MenuAction.AbortMainTask
                            ? startButtonColor
                            : buttonColor;
            }
        }

        if (statusText != null)
        {
            statusText.gameObject.SetActive(IsConditionSelectionVisible());
            string trialOrder = randomizeT1Trials
                ? $"Random (seed {focusPointingTaskManager?.RandomSeed ?? 0})"
                : "Fixed";
            statusText.text =
                $"Task: {selectedTask}\nCondition: {selectedCondition}\nT1 Order: {trialOrder}";
        }

        if (menuPanel != null)
        {
            menuPanel.enabled = IsConditionSelectionVisible();
        }
    }

    private bool ShouldShowButton(MenuAction action)
    {
        if (IsTrainingVisible())
        {
            return action == MenuAction.EndTraining;
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

            if ((action == MenuAction.SelectT1FixedOrder || action == MenuAction.SelectT1RandomOrder)
                && selectedTask != PrototypeTask.T1FocusPointing)
            {
                return false;
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
            || (action == MenuAction.SelectT2AReferenceList && selectedTask == PrototypeTask.T2AReferenceList)
            || (action == MenuAction.SelectT1FixedOrder && !randomizeT1Trials)
            || (action == MenuAction.SelectT1RandomOrder && randomizeT1Trials)
            || (action == MenuAction.SelectRaycastBaseline && selectedCondition == InteractionCondition.RaycastBaseline)
            || (action == MenuAction.SelectExplicitDisplayFocus && selectedCondition == InteractionCondition.ExplicitDisplayFocus)
            || (action == MenuAction.SelectNoOcclusion && selectedLayout == DisplayLayoutPreset.NoOcclusion)
            || (action == MenuAction.SelectPartialOcclusion && selectedLayout == DisplayLayoutPreset.PartialOcclusion)
            || (action == MenuAction.SelectStrongOcclusion && selectedLayout == DisplayLayoutPreset.StrongOcclusion);
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

        if (referenceListTaskManager == null)
        {
            referenceListTaskManager = FindObjectOfType<ReferenceListTaskManager>();
        }

        if (raycastPointer == null)
        {
            raycastPointer = FindObjectOfType<RaycastPointer>();
        }

        if (gazeProvider == null)
        {
            gazeProvider = FindObjectOfType<GazeProvider>();
        }
    }

    private void ReturnActiveTaskToSelection()
    {
        if (focusPointingTaskManager != null
            && focusPointingTaskManager.CurrentPhase != FocusPointingTaskPhase.ConditionSelection)
        {
            focusPointingTaskManager.ReturnToConditionSelection();
        }

        if (referenceListTaskManager != null
            && referenceListTaskManager.CurrentPhase != ReferenceListTaskPhase.ConditionSelection)
        {
            referenceListTaskManager.ReturnToConditionSelection();
        }
    }
}
