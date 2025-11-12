// File: ShrinePortal2D.cs
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;          

[RequireComponent(typeof(Collider2D))]
public class ShrinePortal2D : MonoBehaviour
{
    [Header("Destination")]
    [Tooltip("Scene to load when player presses F (must be in Build Settings).")]
    public string sceneToLoad = "Shrine1";

    [Header("Gate / Progress")]
    [Tooltip("Cloud Save key required to unlock this shrine (e.g., cleared_ShrineOne). Leave empty to always unlocked.")]
    public string requiredClearedKey = "";
    [Tooltip("Start locked until progress is evaluated/applied.")]
    public bool startLocked = false;

    [Header("Trigger Filter")]
    [Tooltip("Only reacts to a collider with this tag. Leave empty to accept any.")]
    public string requiredTag = "Player";

    [Header("Visuals Shown While In Range")]
    [Tooltip("Objects (sprites, glows, outlines, etc.) to enable when the player is in the trigger.")]
    public GameObject[] highlightObjects;
    [Tooltip("Root GameObject of your prompt UI (Canvas/Panel).")]
    public GameObject promptRoot;
    [Tooltip("TMP text component on the prompt to show 'Press F to enter' or 'Locked'.")]
    public TMP_Text promptLabel;

    [Header("Prompt Text")]
    public string unlockedText = "Press F to enter";
    public string lockedText = "Locked";

    bool _playerInRange;
    bool _unlocked;

    void Awake()
    {
        // Initialize lock state
        ApplyLocked(startLocked);

        // Hide visuals until the player is in range
        SetHighlights(false);
        SetPromptVisible(false);
    }

    // ---- Public APIs to drive lock state from your world loader ----

    /// <summary>Force locked/unlocked state (true = locked).</summary>
    public void ApplyLocked(bool locked)
    {
        _unlocked = !locked;
        RefreshPromptText();
    }

    /// <summary>Set unlocked directly (opposite of ApplyLocked).</summary>
    public void SetUnlocked(bool unlocked) => ApplyLocked(!unlocked);

    /// <summary>
    /// Evaluate this gate using a progress lookup, e.g. loader.EvaluateWith(key => flags[key]).
    /// Unlocks when requiredClearedKey is true or when no key is set.
    /// </summary>
    public void EvaluateWith(Func<string, bool> isCleared)
    {
        if (string.IsNullOrEmpty(requiredClearedKey))
            ApplyLocked(false);                 // no requirement → unlocked
        else
            ApplyLocked(!(isCleared?.Invoke(requiredClearedKey) ?? false));
    }

    // ---------------- Trigger Handling ----------------

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!PassesFilter(other.gameObject)) return;
        _playerInRange = true;
        SetHighlights(true);
        SetPromptVisible(true);
        RefreshPromptText();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!PassesFilter(other.gameObject)) return;
        _playerInRange = false;
        SetHighlights(false);
        SetPromptVisible(false);
    }

    void Update()
    {
        if (_playerInRange && _unlocked && Input.GetKeyDown(KeyCode.F))
        {
            if (!string.IsNullOrEmpty(sceneToLoad))
                SceneManager.LoadScene(sceneToLoad);
            else
                Debug.LogWarning("[ShrinePortal2D] sceneToLoad is empty.");
        }
    }

    // ---------------- Helpers ----------------

    bool PassesFilter(GameObject other) =>
        string.IsNullOrEmpty(requiredTag) || other.CompareTag(requiredTag);

    void SetHighlights(bool on)
    {
        if (highlightObjects == null) return;
        for (int i = 0; i < highlightObjects.Length; i++)
        {
            if (highlightObjects[i]) highlightObjects[i].SetActive(on);
        }
    }

    void SetPromptVisible(bool on)
    {
        if (promptRoot) promptRoot.SetActive(on);
    }

    void RefreshPromptText()
    {
        if (!promptLabel) return;
        promptLabel.text = _unlocked ? unlockedText : lockedText;
    }
}
