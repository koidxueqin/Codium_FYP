// Fbutton.cs
using UnityEngine;
using UnityEngine.SceneManagement;

public class Fbutton : MonoBehaviour
{
    [Header("Behavior")]
    [Tooltip("Name of the scene to load (must be in Build Settings).")]
    public string sceneToLoad = "Shrine1";

    [Tooltip("Only allow interaction if the other collider has this tag. Leave empty to allow any.")]
    public string requiredTag = "Player";

    [Header("Optional UI Prompt")]
    [Tooltip("Optional: a prompt GameObject like 'Press F to enter'. It will show/hide automatically.")]
    public GameObject promptUI;

    bool _playerInRange;

    void Awake()
    {
        if (promptUI != null) promptUI.SetActive(false);
    }

    // --- For 2D triggers ---
    void OnTriggerEnter2D(Collider2D other)
    {
        if (IsAllowed(other.gameObject))
        {
            _playerInRange = true;
            if (promptUI != null) promptUI.SetActive(true);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (IsAllowed(other.gameObject))
        {
            _playerInRange = false;
            if (promptUI != null) promptUI.SetActive(false);
        }
    }

  

    void Update()
    {
        if (_playerInRange && Input.GetKeyDown(KeyCode.F))
        {

            SceneManager.LoadScene(sceneToLoad);
        }
    }

    bool IsAllowed(GameObject other)
    {
        return string.IsNullOrEmpty(requiredTag) || other.CompareTag(requiredTag);
    }
}
