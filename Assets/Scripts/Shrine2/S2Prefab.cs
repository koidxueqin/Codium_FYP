using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public class S2Prefab : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Assign your GameController, or leave empty to use Shrine2Controller.Instance.")]
    public Shrine2Controller controller;

    [Header("Trigger")]
    [Tooltip("Only react when a collider with this tag enters.")]
    public string triggerTag = "Catcher"; // default, set your catcher GameObject to this tag

    [Header("Events")]
    public UnityEvent onMatch;   // invoked when answer is consumed
    public UnityEvent onNoMatch; // invoked when it doesn't match / already used

    private TextMeshProUGUI tmpUI;
    private TextMeshPro tmp3D;
    private bool _armed;

    void Awake()
    {
        if (controller == null)
            controller = Shrine2Controller.Instance;

        tmpUI = GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmpUI == null)
            tmp3D = GetComponentInChildren<TextMeshPro>(true);

        // Ensure collider is a trigger to receive OnTriggerEnter2D
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void Start()
    {
        _armed = false;
        Invoke(nameof(Arm), 0.20f);     // small grace to avoid spawn-overlap penalties
        Invoke(nameof(DestroySelf), 3f);
    }

    private void Arm() => _armed = true;

    private void DestroySelf() => Destroy(gameObject);

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_armed) return;
        if (!string.IsNullOrEmpty(triggerTag) && !other.CompareTag(triggerTag)) return;

        var ctrl = controller != null ? controller : Shrine2Controller.Instance;
        if (ctrl == null)
        {
            Debug.LogWarning("[S2Prefab] No Shrine2Controller found/assigned.");
            Destroy(gameObject);
            return;
        }

        string text = GetBlockText();
        if (string.IsNullOrEmpty(text))
        {
            Debug.LogWarning("[S2Prefab] No TextMeshPro component or empty text.");
            onNoMatch?.Invoke();
            Destroy(gameObject);
            return;
        }

        bool consumed = ctrl.TryConsumeAnswer(text);
        if (consumed) onMatch?.Invoke();
        else onNoMatch?.Invoke();

        Destroy(gameObject);
    }

    private string GetBlockText()
    {
        if (tmpUI != null) return tmpUI.text;
        if (tmp3D != null) return tmp3D.text;
        return null;
    }
}
