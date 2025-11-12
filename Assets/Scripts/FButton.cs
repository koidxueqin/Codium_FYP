// File: FButton2D.cs
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public class FButton2D : MonoBehaviour
{
    [Header("Destination")]
    [SerializeField] private string sceneName = "Shrine1";

    [Header("Filtering (optional)")]
    [Tooltip("Only react to colliders with this tag. Leave empty to allow any.")]
    [SerializeField] private string requiredTag = "Player";

    [Header("Input")]
    [SerializeField] private KeyCode interactKey = KeyCode.F;

    private bool _inRange;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true; // ensure trigger for OnTrigger events
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (PassesFilter(other)) _inRange = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (PassesFilter(other)) _inRange = false;
    }

    private void Update()
    {
        if (_inRange && Input.GetKeyDown(interactKey))
        {
            if (!string.IsNullOrEmpty(sceneName))
                SceneManager.LoadScene(sceneName);
            else
                Debug.LogWarning("[FButton2D] Scene name is empty. Set it in the Inspector.");
        }
    }

    private bool PassesFilter(Collider2D other)
    {
        return string.IsNullOrEmpty(requiredTag) || other.CompareTag(requiredTag);
    }
}
