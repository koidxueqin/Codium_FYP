// PanelGate.cs
using UnityEngine;

public class PanelGate : MonoBehaviour
{
    [Header("Disable these while THIS panel is active")]
    public GameObject[] deactivateObjects;
    public Behaviour[] disableBehaviours;

    [Tooltip("If this panel has a CanvasGroup, block clicks behind it while shown.")]
    public bool blockRaycasts = true;

    CanvasGroup _cg;

    void Awake() => _cg = GetComponent<CanvasGroup>();

    void OnEnable()
    {
        SetTargets(false);
        if (blockRaycasts && _cg)
        {
            _cg.blocksRaycasts = true;
            _cg.interactable = true;
        }
    }

    void OnDisable()
    {
        SetTargets(true);
        if (blockRaycasts && _cg)
        {
            _cg.blocksRaycasts = false;
            _cg.interactable = false;
        }
    }

    void SetTargets(bool active)
    {
        if (deactivateObjects != null)
            foreach (var go in deactivateObjects) if (go) go.SetActive(active);

        if (disableBehaviours != null)
            foreach (var b in disableBehaviours) if (b) b.enabled = active;
    }
}
