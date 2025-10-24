using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using UnityEngine.SceneManagement;

[System.Serializable]
public class OrderQuestion
{
    [TextArea] public string prompt;
    [TextArea] public string contextLine;
    [TextArea] public string[] correctOrder;
    [TextArea] public string[] distractors;
    [TextArea] public string[] stepExplanations; 
    [TextArea] public string[] stepHints;
}

public class Shrine2OrderingController : MonoBehaviour
{

    [Header("Characters")]
    public PlayerMovement playerMovement;
    public EnemyAnimator enemyAnimator;


    [Header("UI - Header")]
    public TMP_Text promptText;
    public TMP_Text contextLineText;

    [Header("UI - Progress / Feedback")]
    public Transform forgedListContainer;
    public TMP_Text forgedLinePrefab;
    public TMP_Text feedbackText;
    public CanvasGroup feedbackToast;

    [Header("UI - Choices")]
    public Button[] choiceButtons;   
    public TMP_Text[] choiceLabels;
    public float stepTimeLimit = 0f;
    public Image stepTimerFill;      
    [Header("Hearts")]
    public HeartUI playerHearts;
    public HeartUI enemyHearts;

    [Header("End Panels")]
    public GameObject questClearedPanel;
    public GameObject gameOverPanel;
    public string worldSceneName = "WorldPage";

    [Header("Rewards UI")]
    public Image[] starFilled;
    public Image[] starEmpty;
    public TMP_Text scoreNum;
    public TMP_Text coinNum;

    [Header("XP Rewards")]
    [SerializeField] int rewardXP = 50;
    [SerializeField] TMP_Text xpNum;


    [Header("Content")]
    public OrderQuestion[] questions;

    [Header("IDs / Save")]
    public string shrineId = "ShrineTwo";

    [Header("FX")]
    public Color wrongFlashColor = new Color(1f, 0.5f, 0.5f);
    public float wrongFlashDuration = 0.1f;
    public float choiceFlashDuration = 0.07f;

    [Header("Tuning")]
    [Range(3, 4)] public int choicesPerStep = 3;
    public bool shuffleChoicePositionsEachStep = true;

    int playerLives, enemyLives, qIndex, stepIndex, currentCorrectIndex = -1, combo;
    bool ended, rewardsGranted, stepActive;
    float stepTimeLeft;

    void Awake()
    {
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            int k = i;
            choiceButtons[i].onClick.RemoveAllListeners();
            choiceButtons[i].onClick.AddListener(() => OnChoose(k));
        }
    }

    void Start()
    {
        if (questions == null || questions.Length == 0)
        {
            Debug.LogWarning("Shrine2 has no questions.");
            return;
        }
        InitLevel();
    }

    void Update()
    {
        if (stepActive && stepTimeLimit > 0f)
        {
            stepTimeLeft -= Time.deltaTime;
            if (stepTimerFill) stepTimerFill.fillAmount = Mathf.Clamp01(stepTimeLeft / stepTimeLimit);
            if (stepTimeLeft <= 0f) { stepActive = false; HandleWrong(true); }
        }
        if (stepActive)
        { // keyboard 1..4
            if (Input.GetKeyDown(KeyCode.Alpha1)) TryPress(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) TryPress(1);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) TryPress(2);
            else if (Input.GetKeyDown(KeyCode.Alpha4)) TryPress(3);
        }
    }

    void InitLevel()
    {
        ended = false; rewardsGranted = false; combo = 0;
        if (playerMovement) playerMovement.isDead(false);

        int lives = Mathf.Max(1, questions.Length);
        playerLives = lives; enemyLives = lives;
        if (playerHearts) { playerHearts.maxLives = lives; playerHearts.SetLives(playerLives); }
        if (enemyHearts) { enemyHearts.maxLives = lives; enemyHearts.SetLives(enemyLives); }

        if (questClearedPanel) questClearedPanel.SetActive(false);
        if (gameOverPanel) gameOverPanel.SetActive(false);

        qIndex = 0;
        LoadQuestion();
        HideToast();
    }

    void LoadQuestion()
    {
        ClearForgedList();
        stepIndex = 0; combo = 0;
        var Q = questions[qIndex];
        if (promptText) promptText.text = Q.prompt;
        if (contextLineText) contextLineText.text = string.IsNullOrEmpty(Q.contextLine) ? "" : Q.contextLine;

        if (stepTimeLimit > 0f)
        {
            stepTimeLeft = stepTimeLimit;
            if (stepTimerFill) stepTimerFill.fillAmount = 1f;
        }
        NewStep();
    }

    void NewStep()
    {
        var Q = questions[qIndex];
        if (stepIndex >= Q.correctOrder.Length) { OnSequenceComplete(); return; }

        string correct = Q.correctOrder[stepIndex];
        var pool = new List<string>();
        if (Q.distractors != null) pool.AddRange(Q.distractors);

        var already = Q.correctOrder.Take(stepIndex).ToHashSet();
        pool = pool.Where(x => !already.Contains(x) && x != correct).Distinct().ToList();

        var distracts = pool.OrderBy(_ => Random.value).Take(Mathf.Max(0, choicesPerStep - 1)).ToList();

        var candidates = new List<string> { correct };
        candidates.AddRange(distracts);
        if (shuffleChoicePositionsEachStep) candidates = candidates.OrderBy(_ => Random.value).ToList();

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            bool active = i < candidates.Count;
            choiceButtons[i].gameObject.SetActive(active);
            if (active && i < choiceLabels.Length && choiceLabels[i]) choiceLabels[i].text = candidates[i];
        }

        currentCorrectIndex = System.Array.IndexOf(choiceLabels.Select(t => t ? t.text : null).ToArray(), correct);
        stepActive = true;
        
    }

    void TryPress(int idx)
    {
        if (!stepActive) return;
        if (idx >= choiceButtons.Length || !choiceButtons[idx].gameObject.activeInHierarchy) return;
        OnChoose(idx);
    }

    void OnChoose(int idx)
    {
        if (!stepActive) return;
        stepActive = false;

        bool ok = (idx == currentCorrectIndex);
        var img = choiceButtons[idx].GetComponent<Image>();
        if (img) StartCoroutine(FlashImage(img, ok ? Color.white : wrongFlashColor, choiceFlashDuration));

        if (ok) HandleCorrect();
        else HandleWrong(false);
    }

    void HandleCorrect()
    {
        combo++;
        var Q = questions[qIndex];
        var locked = Instantiate(forgedLinePrefab, forgedListContainer);
        locked.text = Q.correctOrder[stepIndex];

        string explain = null;
        if (Q.stepExplanations != null && stepIndex < Q.stepExplanations.Length)
            explain = Q.stepExplanations[stepIndex];

        
        if (string.IsNullOrWhiteSpace(explain))
            explain = (combo % 3 == 0 ? "Combo!" : "Nice!");

        ShowToastTimed(explain, 3.5f);

        stepIndex++;
        Invoke(nameof(NewStep), 0.05f);



        if (enemyAnimator)
        {
            enemyAnimator.Hurt(true);           // turn ON
            Invoke(nameof(EnemyStopHurt), 1.15f); // use your clip length here
        }
    }

    void EnemyStopHurt()
    {
        if (enemyAnimator) enemyAnimator.Hurt(false); // turn OFF
    }

    void HandleWrong(bool timeout)
    {
        combo = 0;
        var Q = questions[qIndex];
        string stepHint = null;
        if (Q.stepHints != null && stepIndex < Q.stepHints.Length)
            stepHint = Q.stepHints[stepIndex];

        string msg = string.IsNullOrWhiteSpace(stepHint)
            ? (timeout ? "Time’s up!" : "Try again.")
            : stepHint;

        ShowToast(msg);




        playerLives = Mathf.Max(0, playerLives - 1);
        if (playerHearts) playerHearts.SetLives(playerLives);
        if (playerMovement) { playerMovement.isHurt(true); Invoke(nameof(StopHurt), 0.8f); }

        if (playerLives <= 0) { EndGameOver(); return; }

        if (timeout)
        {
            qIndex = Mathf.Clamp(qIndex + 1, 0, questions.Length - 1);
            Invoke(nameof(LoadQuestion), 0.3f); // this is where the timer resets (per-question)
        }
        else
        {
            Invoke(nameof(NewStep), 0.05f);     // same question, same timer continues
        }



    }

    void StopHurt() { if (playerMovement) playerMovement.isHurt(false); }

    void OnSequenceComplete()
    {
        enemyLives = Mathf.Max(0, enemyLives - 1);
        if (enemyHearts) enemyHearts.SetLives(enemyLives);

        var Q = questions[qIndex];
       

        if (enemyLives <= 0) { EndQuestCleared(); return; }
        qIndex = Mathf.Clamp(qIndex + 1, 0, questions.Length - 1);
        Invoke(nameof(LoadQuestion), 0.3f);
    }

    void EndQuestCleared()
    {
        ended = true;
        int stars = Mathf.Clamp(playerLives, 1, 3);
        UpdateStarsUI(stars);
        var (score, coins) = ComputeRewards(stars);
        if (scoreNum) scoreNum.text = score.ToString();
        if (coinNum) coinNum.text = coins.ToString();
        if (xpNum) xpNum.text = rewardXP.ToString();

        if (!rewardsGranted)
        {
            rewardsGranted = true;
            _ = SaveAllAsync();
        }

        async Task SaveAllAsync()
        {
            try
            {
                var res = await RewardsHelper.SaveRewardsAndXpAsync(shrineId, stars, score, coins, rewardXP);
                // No level-up UI here.
                // Do not overwrite the quest panel’s coinNum with total coins.
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Shrine2] SaveAll failed: {ex.Message}");
            }
        }


        if (questClearedPanel) questClearedPanel.SetActive(true);
    }

    void EndGameOver()
    {
        ended = true;
        ShowToast("Game Over!");
        if (playerMovement) playerMovement.isDead(true);
        if (gameOverPanel) gameOverPanel.SetActive(true);
    }

    void ClearForgedList()
    {
        for (int i = forgedListContainer.childCount - 1; i >= 0; i--)
            Destroy(forgedListContainer.GetChild(i).gameObject);
    }

    System.Collections.IEnumerator FlashImage(Image img, Color flash, float dur)
    {
        if (!img) yield break;
        var o = img.color; img.color = flash;
        yield return new WaitForSecondsRealtime(dur);
        img.color = o;
    }

    void ShowToast(string msg)
    {
        if (feedbackText) feedbackText.text = msg;
        if (feedbackToast) { feedbackToast.alpha = 1f; feedbackToast.blocksRaycasts = false; feedbackToast.interactable = false; }
    }

    void ShowToastTimed(string msg, float s)
    {
        ShowToast(msg); CancelInvoke(nameof(HideToast)); Invoke(nameof(HideToast), s);
    }
    void HideToast() { if (feedbackToast) feedbackToast.alpha = 0f; }

    void UpdateStarsUI(int stars)
    {
        for (int i = 0; i < 3; i++)
        {
            bool filled = i < stars;
            if (starFilled != null && i < starFilled.Length && starFilled[i]) { starFilled[i].enabled = filled; starFilled[i].raycastTarget = false; }
            if (starEmpty != null && i < starEmpty.Length && starEmpty[i]) { starEmpty[i].enabled = !filled; starEmpty[i].raycastTarget = false; }
        }
    }

    (int score, int coins) ComputeRewards(int stars)
    {
        int score = stars * 500;
        int coins = stars switch { 3 => 100, 2 => 60, _ => 30 };
        return (score, coins);
    }

   

    async Task EnsureUgsReadyAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    // Buttons on end panels
    public void BtnTryAgain() { if (ended) InitLevel(); }
    public void BtnExitShrine()
    {
        if (!string.IsNullOrEmpty(worldSceneName)) SceneManager.LoadScene(worldSceneName);
        else Debug.LogWarning("worldSceneName is empty.");
    }
}
