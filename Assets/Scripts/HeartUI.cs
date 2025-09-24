using UnityEngine;
using UnityEngine.UI;

public class HeartUI : MonoBehaviour
{
    [Header("Assign in Inspector (left→right)")]
    public Image[] fullHearts;   // size 3
    public Image[] emptyHearts;  // size 3

    [Header("Optional")]
    public int maxLives = 3;     // default 3
    public bool animateOnHit = false;
    public float hitScale = 1.1f;
    public float hitBounceTime = 0.08f;

    int _lives;

    void Awake()
    {
        _lives = maxLives;
        SetLives(_lives);
    }

    /// Show exactly 'lives' full hearts (0..maxLives)
    public void SetLives(int lives)
    {
        _lives = Mathf.Clamp(lives, 0, maxLives);

        for (int i = 0; i < maxLives; i++)
        {
            bool showFull = i < _lives;
            if (fullHearts != null && i < fullHearts.Length && fullHearts[i])
                fullHearts[i].enabled = showFull;

            if (emptyHearts != null && i < emptyHearts.Length && emptyHearts[i])
                emptyHearts[i].enabled = !showFull;
        }
    }

    /// Lose one life and update UI. Returns the new life count.
    public int Damage(int amount = 1)
    {
        _lives = Mathf.Max(0, _lives - Mathf.Max(1, amount));
        SetLives(_lives);
        if (animateOnHit) FlashHit();
        return _lives;
    }

    /// Restore to full lives.
    public void ResetHearts()
    {
        _lives = maxLives;
        SetLives(_lives);
    }

    /// Get current lives.
    public int GetLives() => _lives;

    // Small bounce effect on hit (no extra packages needed)
    void FlashHit()
    {
        // simple tween-ish bounce using a coroutine
        StopAllCoroutines();
        StartCoroutine(Bounce());
    }

    System.Collections.IEnumerator Bounce()
    {
        var t = 0f;
        var start = Vector3.one;
        var peak = Vector3.one * hitScale;

        // scale up
        while (t < hitBounceTime)
        {
            t += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(start, peak, t / hitBounceTime);
            yield return null;
        }

        // scale back
        t = 0f;
        while (t < hitBounceTime)
        {
            t += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(peak, start, t / hitBounceTime);
            yield return null;
        }
        transform.localScale = Vector3.one;
    }
}
