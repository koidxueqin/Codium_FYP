using UnityEngine;
using TMPro;
using UnityEngine.Events;

public class ToastUI : MonoBehaviour
{
    [SerializeField] CanvasGroup cg;
    [SerializeField] TMP_Text messageText;
    System.Action onOk;

    void Awake()
    {
        if (cg) { cg.alpha = 0f; cg.blocksRaycasts = false; }
    }

    public void Show(string msg, System.Action okCallback = null)
    {
        onOk = okCallback;
        if (messageText) messageText.text = msg;
        if (cg) { cg.alpha = 1f; cg.blocksRaycasts = true; }
    }

    public void Hide()
    {
        if (cg) { cg.alpha = 0f; cg.blocksRaycasts = false; }
        onOk = null;
    }

    // Hook this to ToastOkButton.onClick
    public void BtnOk()
    {
        var cb = onOk;
        Hide();
        cb?.Invoke();
    }
}
