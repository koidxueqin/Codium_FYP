using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;   // ? add

public class CodeRunnerUI3 : MonoBehaviour
{
    [Header("UI")]
    public TMP_InputField input;
    public TMP_Text errorHint;
    public Camera cam;

    [Header("Spawn Wiring")]
    public Shrine3Controller shrine;
    public Transform spawnPoint;
    public Transform blocksParent;
    public GameObject codeBlockPrefab;

    [Header("Player Control Lock")]
    [Tooltip("All movement/input scripts to disable while typing (e.g., PlayerMovement, PlayerInput).")]
    public Behaviour[] disableWhileTyping;     // ? add

    bool locked;

    void OnEnable()
    {
        // lock player when input gets focus (clicked or tabbed)
        if (input != null)
        {
            input.onSelect.AddListener(OnInputSelected);
            input.onDeselect.AddListener(OnInputDeselected);
        }
    }

    void OnDisable()
    {
        if (input != null)
        {
            input.onSelect.RemoveListener(OnInputSelected);
            input.onDeselect.RemoveListener(OnInputDeselected);
        }
    }

    void OnInputSelected(string _)
    {
        SetPlayerControlEnabled(false);
    }

    void OnInputDeselected(string _)
    {
        SetPlayerControlEnabled(true);
    }

    void SetPlayerControlEnabled(bool enabled)
    {
        if (disableWhileTyping == null) return;
        foreach (var b in disableWhileTyping)
        {
            if (b) b.enabled = enabled;
        }
    }

    public void Init(Shrine3Controller ctrl, Transform sp, Transform parent, GameObject prefab)
    {
        shrine = ctrl; spawnPoint = sp; blocksParent = parent; codeBlockPrefab = prefab;
        SetLocked(false);
    }

    public void OnRun()
    {
        if (locked) { ShowError("Destroy or submit the existing block first."); return; }
        if (!input || !cam || shrine == null || spawnPoint == null || blocksParent == null || codeBlockPrefab == null)
        {
            ShowError("Runner not wired."); return;
        }

        var text = input.text ?? string.Empty;

        // 1) spawn
        shrine.SpawnBlockWithText(text, cam);

        // 2) immediately deselect and lock the field
        input.text = string.Empty;                // clear all input
        input.DeactivateInputField();             // kill caret/focus
        EventSystem.current?.SetSelectedGameObject(null);
        SetLocked(true);                          // disables typing until block is discarded/submitted

        // 3) re-enable player movement (since field is no longer focused)
        SetPlayerControlEnabled(true);

        HideError();
    }

    public void SetLocked(bool v)
    {
        locked = v;
        if (input) input.interactable = !v;
        // If we lock while it’s focused, also deselect to avoid swallowing WASD
        if (v)
        {
            input.DeactivateInputField();
            EventSystem.current?.SetSelectedGameObject(null);
        }
    }

    void ShowError(string msg)
    {
        if (!errorHint) return;
        errorHint.text = msg;
        errorHint.gameObject.SetActive(true);
    }

    void HideError()
    {
        if (!errorHint) return;
        errorHint.gameObject.SetActive(false);
    }
}
