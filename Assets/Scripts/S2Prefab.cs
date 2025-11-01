using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class S2Prefab : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Assign your GameController, or leave empty to use GameController.Instance.")]
    public Shrine2Controller controller;

    [Tooltip("Optional: only react when a collider with this tag enters. Leave empty to accept any.")]
    public string triggerTag = ""; // e.g., "Slot" or "Goal"

    [Header("Events")]
    public UnityEvent onMatch;     // invoked when answer is consumed
    public UnityEvent onNoMatch;   // invoked when it doesn't match / already used

    private TextMeshProUGUI tmpUI;
    private TextMeshPro tmp3D;

    void Awake()
    {
        controller = FindAnyObjectByType<Shrine2Controller>();
        tmpUI = GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmpUI == null)
            tmp3D = GetComponentInChildren<TextMeshPro>(true);
    }

    private void Start()
    {
        Invoke("DestroySelf", 3);
    }

    void DestroySelf()
    {
        Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!string.IsNullOrEmpty(triggerTag) && !other.CompareTag(triggerTag))
            return;

        // Find controller if not assigned
        var ctrl = controller != null ? controller : Shrine2Controller.Instance;
        if (ctrl == null)
        {
            Debug.LogWarning("[S2Prefab] No Shrine2Controller found/assigned.");
            Destroy(gameObject); // still destroy to avoid lingering blocks
            return;
        }

        // Read the text on this prefab
        string text = GetBlockText();
        if (string.IsNullOrEmpty(text))
        {
            Debug.LogWarning("[S2Prefab] No TextMeshPro component or empty text.");
            onNoMatch?.Invoke();
            Destroy(gameObject); // destroy even if invalid
            return;
        }

        // Ask the controller to consume it (one-time use)
        bool consumed = ctrl.TryConsumeAnswer(text);
        if (consumed)
        {
            onMatch?.Invoke();
        }
        else
        {
            onNoMatch?.Invoke();
        }

        // Always destroy on trigger enter (correct or wrong)
        Destroy(gameObject);
    }

    private string GetBlockText()
    {
        if (tmpUI != null) return tmpUI.text;
        if (tmp3D != null) return tmp3D.text;
        return null;
    }
}
