using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class TargetSelectable : MonoBehaviour, IPointerClickHandler, ISelectHandler
{
    [SerializeField] private FocusPointingTaskManager manager;
    [SerializeField] private string displayName;
    [SerializeField] private string targetPositionId;
    [SerializeField] private bool notifyOnPointerClick = true;
    [SerializeField] private bool notifyOnSelect;

    public string DisplayName => displayName;
    public string TargetPositionId => targetPositionId;

    public void Initialize(FocusPointingTaskManager taskManager, string displayId, string positionId)
    {
        manager = taskManager;
        displayName = displayId;
        targetPositionId = positionId;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (notifyOnPointerClick)
        {
            NotifyManager();
        }
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (notifyOnSelect)
        {
            NotifyManager();
        }
    }

    public void Select()
    {
        NotifyManager();
    }

    private void NotifyManager()
    {
        if (manager == null)
        {
            manager = FindObjectOfType<FocusPointingTaskManager>();
        }

        if (manager != null)
        {
            manager.OnTargetSelected(this);
        }
    }
}
