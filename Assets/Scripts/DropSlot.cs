using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class DropSlot : MonoBehaviour, IDropHandler
{
    public string slotId = "slot1";
    public TMP_Text placedText;

    public DragToken Current { get; private set; }

    public void OnDrop(PointerEventData eventData)
    {
        var token = eventData.pointerDrag ? eventData.pointerDrag.GetComponent<DragToken>() : null;
        if (token == null) return;

        // If something is here already, make it visible and send it back
        if (Current)
        {
            Current.gameObject.SetActive(true);
            Current.SnapBack();
        }

        // Accept this token (we still parent so you could support multi-slots later)
        Current = token;
        token.transform.SetParent(transform, false);
        token.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

        if (placedText) placedText.text = token.tokenText;

        // Let controller check correctness
        if (SimpleGameController.Instance != null)
            SimpleGameController.Instance.NotifySlotFilled(slotId, token.tokenText, token);
    }

    public void ClearSlot()
    {
        if (Current)
        {
            // ensure it’s visible again, then return to tokens row
            Current.gameObject.SetActive(true);
            Current.SnapBack();
            Current = null;
        }
        if (placedText) placedText.text = "";
    }

    /// Call this after a correct answer to hide the pill but keep text.
    public void HidePlacedTokenVisual()
    {
        if (Current) Current.gameObject.SetActive(false);
    }
}
