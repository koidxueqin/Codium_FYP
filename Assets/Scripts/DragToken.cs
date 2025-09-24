using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;

public class DragToken : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [HideInInspector] public string tokenText;
    [HideInInspector] public Transform originalParent;

    private RectTransform rect;
    private Canvas canvas;
    private CanvasGroup cg;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
    }

    public void SetLabel(string text)
    {
        tokenText = text;
        GetComponentInChildren<TextMeshProUGUI>().text = text;
    }

    public void OnBeginDrag(PointerEventData e)
    {
        originalParent = transform.parent;
        transform.SetParent(canvas.transform, true);
        cg.blocksRaycasts = false;
        cg.alpha = 0.9f;
    }

    public void OnDrag(PointerEventData e)
    {
        rect.anchoredPosition += e.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData e)
    {
        cg.blocksRaycasts = true;
        cg.alpha = 1f;
        if (transform.parent == canvas.transform) SnapBack(); // not dropped on a slot
    }

    public void SnapBack()
    {
        transform.SetParent(originalParent, true);
        rect.anchoredPosition = Vector2.zero;
    }

    public void SetVisible(bool visible)
    {
        var cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        cg.alpha = visible ? 1f : 0f;
        cg.blocksRaycasts = visible;

        // Also stop the image from catching clicks when hidden
        var img = GetComponent<Image>();
        if (img) img.raycastTarget = visible;
    }
}
