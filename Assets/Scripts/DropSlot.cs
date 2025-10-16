using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public interface ITokenDropNotifier
{
    void NotifySlotFilled(string slotId, string tokenText, DragToken token);
}

public class DropSlot : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    [Header("Identity")]
    public string slotId = "slot1";

    [Header("Notifier (optional)")]
    [SerializeField] private MonoBehaviour notifierBehaviour; // drag your controller if you want
    private ITokenDropNotifier notifier;

    // Runtime
    public DragToken Current { get; private set; }   // the token sitting in this slot (if any)

    void Awake()
    {
        notifier = notifierBehaviour as ITokenDropNotifier; // ok if null
        Current = null;
    }

    /// Allow wiring the notifier from code
    public void SetNotifier(ITokenDropNotifier n) => notifier = n;

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData == null || eventData.pointerDrag == null) return;

        var token = eventData.pointerDrag.GetComponent<DragToken>();
        if (token == null) return;

        // If this slot is already occupied by a different token ? reject and snap the dragged token back
        if (Current != null && Current != token)
        {
            token.SnapBack();
            return;
        }

        // If we dropped the same token we're already holding, just re-center it
        if (Current == token)
        {
            CenterToken(token);
            NotifyPlaced(token);
            return;
        }

        // If token came from another slot, free that slot first
        var sourceSlot = token.sourceSlotBeforeDrag;
        if (sourceSlot != null && sourceSlot != this)
        {
            sourceSlot.ClearSlot();
        }

        // Accept: parent here and center
        Current = token;
        CenterToken(token);
        NotifyPlaced(token);
    }

    private void CenterToken(DragToken token)
    {
        token.transform.SetParent(transform, false);

        var r = token.GetComponent<RectTransform>();
        var my = GetComponent<RectTransform>();
        if (r != null && my != null)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = Vector2.zero;
            r.localRotation = Quaternion.identity;
            r.localScale = Vector3.one;
            r.sizeDelta = my.rect.size; // fit perfectly in slot
        }
    }

    private void NotifyPlaced(DragToken token)
    {
        // Only notify when a token actually sits in the slot
        if (notifier != null)
            notifier.NotifySlotFilled(slotId, token.tokenText, token);
        else if (SimpleGameController.Instance != null)
            SimpleGameController.Instance.NotifySlotFilled(slotId, token.tokenText, token);
    }

    public void ClearSlot()
    {
        // Eject the token back to its original row (if any)
        if (Current != null)
        {
            Current.gameObject.SetActive(true);
            Current.SnapBack();
            Current = null;
        }

        // Remove any leftover children (safety)
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child != null && child.parent == transform)
                Destroy(child.gameObject);
        }
    }

    /// Optional: right-click to eject the token back to the row
    public void OnPointerClick(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Right) return;
        if (Current == null) return;

        Current.SnapBack();
        Current = null;
    }

    /// Hide the token pill (used if you want to hide visuals on correct)
    public void HidePlacedTokenVisual()
    {
        if (Current) Current.gameObject.SetActive(false);
    }
}
