using UnityEngine;
using UnityEngine.UI;

public class StarsUI : MonoBehaviour
{
    [Tooltip("Full star Images (rendered on top).")]
    [SerializeField] private Image[] fullStars = new Image[3];

    [Tooltip("Empty star Images behind the full ones (optional).")]
    [SerializeField] private Image[] emptyStars = new Image[3];

    [Tooltip("Max stars this widget shows.")]
    [SerializeField] private int maxStars = 3;

    public int MaxStars => maxStars;

    /// <summary>
    /// Set how many stars to display as filled (0..maxStars).
    /// </summary>
    public void SetStars(int stars)
    {
        stars = Mathf.Clamp(stars, 0, maxStars);

        for (int i = 0; i < maxStars; i++)
        {
            bool showFull = i < stars;

            if (fullStars != null && i < fullStars.Length && fullStars[i])
            {
                fullStars[i].enabled = showFull;
                fullStars[i].raycastTarget = false;
            }

            if (emptyStars != null && i < emptyStars.Length && emptyStars[i])
            {
                // If you want empties always visible, set to true.
                // If you want empties hidden when full overlays exist, use !showFull.
                emptyStars[i].enabled = !showFull;
                emptyStars[i].raycastTarget = false;
            }
        }
    }

    /// <summary>
    /// Optional helper if you need to toggle everything at once.
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (fullStars != null)
            foreach (var img in fullStars) if (img) img.enabled = visible;

        if (emptyStars != null)
            foreach (var img in emptyStars) if (img) img.enabled = visible;
    }
}
