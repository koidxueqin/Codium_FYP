using UnityEngine;
using TMPro;
using UnityEngine.Events;

public class TimerUI : MonoBehaviour
{
    [SerializeField] TMP_Text timerText;
    [SerializeField] bool autoHideWhenStopped = true;

    public UnityEvent OnTimeUp;
    public float TimeLeft { get; private set; }
    public bool IsRunning { get; private set; }

    public void StartCountdown(float seconds)
    {
        TimeLeft = Mathf.Max(0, seconds);
        IsRunning = true;
        if (timerText) timerText.gameObject.SetActive(true);
        UpdateText();
    }

    public void Stop()
    {
        IsRunning = false;
        if (autoHideWhenStopped && timerText) timerText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!IsRunning) return;
        TimeLeft -= Time.deltaTime;
        if (TimeLeft <= 0f)
        {
            TimeLeft = 0f;
            IsRunning = false;
            UpdateText();
            OnTimeUp?.Invoke();
            return;
        }
        UpdateText();
    }

    void UpdateText()
    {
        if (!timerText) return;
        int s = Mathf.CeilToInt(TimeLeft);
        timerText.text = s.ToString();
    }
}
