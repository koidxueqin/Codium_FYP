using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[System.Serializable]
public class OrderQuestion
{
    [TextArea] public string prompt;
    [TextArea] public string contextLine;

    // Labelled in the Inspector as "Correct Answers"
    [TextArea] public string[] correctOrder;

    [TextArea] public string[] distractors;

    // If provided, shown when the whole question is completed
    [TextArea] public string[] stepExplanations;

    // If provided, shown on wrong catches
    [TextArea] public string[] stepHints;
}

public class Shrine2Controller : MonoBehaviour
{
    // ------------------ Spawner ------------------
    [Header("Spawner / Prefab")]
    [Tooltip("Prefab that contains a TextMeshPro(TMP) somewhere inside.")]
    public GameObject codeBlockPrefab;

    [Tooltip("Assign any number of empty Transforms in your scene as spawn positions.")]
    public Transform[] spawnPoints;

    [Tooltip("Seconds between spawns when repeating.")]
    [Min(0.01f)] public float spawnInterval = 1.5f;

    [Tooltip("0 = unlimited. Otherwise stops after this many total spawns.")]
    public int maxSpawns = 0;

    [Tooltip("Spawn one immediately on Start().")]
    public bool spawnOnStart = true;

    [Tooltip("If true, keeps spawning on a timer.")]
    public bool repeatSpawning = true;

    private int _spawnedCount = 0;
    private Coroutine _loop;

    // Current spawn text pool (rebuilt per-question)
    [HideInInspector] public string[] blockTexts;

    // ------------------ Matching ------------------
    [Header("Matching Settings")]
    [Tooltip("Ignore case when matching.")]
    public bool ignoreCase = true;

    // Remaining answers to collect this question (normalized)
    private HashSet<string> remainingAnswers = new HashSet<string>();
    // All correct answers for quick membership checks (normalized)
    private HashSet<string> allCorrectThisQ = new HashSet<string>();

    // ------------------ Characters ------------------
    [Header("Characters")]
    public PlayerMovement playerMovement;
    public EnemyAnimator enemyAnimator;

    // ------------------ UI - Header / Forged ------------------
    [Header("UI - Header")]
    public TMP_Text promptText;
    public TMP_Text contextLineText;

    [Header("UI - Progress")]
    public Transform forgedListContainer;
    public TMP_Text forgedLinePrefab;

    // ------------------ UI - Speech Bubble ------------------
    [Header("UI - Speech Bubble")]
    public GameObject speechbubble;
    public TMP_Text bubbleTitle;
    public TMP_Text bubbleBody;
    public GameObject nextButton;

    // ------------------ Hearts ------------------
    [Header("Hearts")]
    public HeartUI playerHearts;
    public HeartUI enemyHearts;

    // ------------------ End Panels / Rewards ------------------
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

    // ------------------ Content / Timing ------------------
    [Header("Content")]
    public OrderQuestion[] questions;

    [Header("IDs / Save")]
    public string shrineId = "ShrineTwo";

    [Header("FX / Tuning")]
    public Color wrongFlashColor = new Color(1f, 0.5f, 0.5f);
    public float wrongFlashDuration = 0.1f;

    [Tooltip("Per-question time limit. 0 = no limit.")]
    public float stepTimeLimit = 0f;
    public Image stepTimerFill;

    // ------------------ State ------------------
    public static Shrine2Controller Instance { get; private set; }

    int playerLives, enemyLives;
    int qIndex;
    bool ended, rewardsGranted, stepActive;   // stepActive==true means accepts catches
    float stepTimeLeft;
    int nextRequiredIndex;                 // which index in correctOrder is next
    string[] currentCorrectSeqNorm;        // normalized correctOrder for this question


    // Bubble "Next" action
    private System.Action _onBubbleNext;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    void Start()
    {
        InitLevel();

        if (spawnOnStart) SpawnOnce();
        StartSpawnLoopIfNeeded();
    }

    void Update()
    {
        if (stepActive && stepTimeLimit > 0f)
        {
            stepTimeLeft -= Time.deltaTime;
            if (stepTimerFill) stepTimerFill.fillAmount = Mathf.Clamp01(stepTimeLeft / stepTimeLimit);
            if (stepTimeLeft <= 0f)
            {
                stepActive = false;
                HandleWrong(timeout: true);
            }
        }
    }

    // ======================== Game Flow ========================

    void InitLevel()
    {
        ended = false; rewardsGranted = false;

        if (playerMovement) { playerMovement.isDead(false); playerMovement.enabled = true; }
        if (questClearedPanel) questClearedPanel.SetActive(false);
        if (gameOverPanel) gameOverPanel.SetActive(false);

        int lives = Mathf.Max(1, questions != null ? questions.Length : 1);
        playerLives = lives;
        enemyLives = lives;
        if (playerHearts) { playerHearts.maxLives = lives; playerHearts.SetLives(playerLives); }
        if (enemyHearts) { enemyHearts.maxLives = lives; enemyHearts.SetLives(enemyLives); }

        qIndex = 0;
        LoadQuestion();

        ShowBubble(
            "How to play",
            "Catch the lines in the required order. Finish the sequence to damage the enemy..",
            false
        );
    }

    void LoadQuestion()
    {
        ClearForgedList();

        if (questions == null || questions.Length == 0)
        {
            Debug.LogWarning("[Shrine2] No questions assigned.");
            EndQuestCleared();
            return;
        }

        var Q = questions[qIndex];

        if (promptText) promptText.text = Q.prompt;
        if (contextLineText) contextLineText.text = string.IsNullOrEmpty(Q.contextLine) ? "" : Q.contextLine;

        // Build spawn pool = all correct answers + distractors (unique, non-empty)
        var candidates = new List<string>();
        if (Q.correctOrder != null) candidates.AddRange(Q.correctOrder);
        if (Q.distractors != null) candidates.AddRange(Q.distractors);
        blockTexts = candidates.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToArray();

        // Prepare ordered sequence (normalized) and reset pointer
        var ordered = (Q.correctOrder ?? new string[0])
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();
        currentCorrectSeqNorm = ordered.Select(Norm).ToArray();
        nextRequiredIndex = 0;

        // Start accepting catches
        stepActive = true;

        // Reset timer per-question
        if (stepTimeLimit > 0f)
        {
            stepTimeLeft = stepTimeLimit;
            if (stepTimerFill) stepTimerFill.fillAmount = 1f;
        }

        // Optional per-question instruction
        if (!string.IsNullOrWhiteSpace(Q.contextLine))
            ShowBubble("Instruction", Q.contextLine, false);

        // Resume spawning for this question
        StartSpawnLoopIfNeeded();
    }


    void OnQuestionComplete()
    {
        // Stop accepting until player proceeds
        stepActive = false;

        // Stop spawning immediately on completion
        StopSpawnLoop();

        // Enemy loses 1 life
        enemyLives = Mathf.Max(0, enemyLives - 1);
        if (enemyHearts) enemyHearts.SetLives(enemyLives);

        if (enemyAnimator)
        {
            enemyAnimator.Hurt(true);
            Invoke(nameof(EnemyStopHurt), 1.15f);
        }

        // If that was the final hit, end the quest *right now* (no Next click needed)
        if (enemyLives <= 0)
        {
            EndQuestCleared();
            return;
        }

        // Otherwise, show the usual explanation bubble and let the player proceed to the next question
        var Q = questions[qIndex];
        string explain =
            (Q.stepExplanations != null && Q.stepExplanations.Length > 0)
                ? string.Join("\n", Q.stepExplanations.Where(s => !string.IsNullOrWhiteSpace(s)))
                : "Well done. You collected all required lines.";

        _onBubbleNext = () =>
        {
            qIndex = Mathf.Clamp(qIndex + 1, 0, questions.Length - 1);

            // Clear current bubble immediately before next content
            ShowBubble("", "", false);

            LoadQuestion();
        };

        ShowBubble("Correct Answers Complete", explain, true);
    }


    void HandleWrong(bool timeout)
    {
        // Penalize
        playerLives = Mathf.Max(0, playerLives - 1);
        if (playerHearts) playerHearts.SetLives(playerLives);
        if (playerMovement) { playerMovement.isHurt(true); Invoke(nameof(StopHurt), 0.8f); }

        if (playerLives <= 0)
        {
            EndGameOver();
            return;
        }

        var Q = questions[qIndex];
        string hint =
            (Q.stepHints != null && Q.stepHints.Length > 0)
                ? Q.stepHints[0]
                : (timeout ? "Time’s up." : "Not a required line.");
        ShowBubble(timeout ? "Timeout" : "Wrong", hint, false);

        // Resume accepting after a wrong
        if (!timeout)
            stepActive = true;
        else
        {
            // On timeout, advance question
            qIndex = Mathf.Clamp(qIndex + 1, 0, questions.Length - 1);
            Invoke(nameof(LoadQuestion), 0.3f);
        }
    }

    void EnemyStopHurt() { if (enemyAnimator) enemyAnimator.Hurt(false); }
    void StopHurt() { if (playerMovement) playerMovement.isHurt(false); }

    void EndQuestCleared()
    {
        ended = true;

        // Hard stop spawning and input
        StopSpawnLoop();
        stepActive = false;
        repeatSpawning = false;
        if (playerMovement) playerMovement.enabled = false;

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

        if (questClearedPanel) questClearedPanel.SetActive(true);

        async Task SaveAllAsync()
        {
            try
            {
                await RewardsHelper.SaveRewardsAndXpAsync(shrineId, stars, score, coins, rewardXP);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Shrine2] SaveAll failed: {ex.Message}");
            }
        }
    }

    void EndGameOver()
    {
        ended = true;
        StopSpawnLoop();
        stepActive = false;
        if (playerMovement) playerMovement.isDead(true);
        if (gameOverPanel) gameOverPanel.SetActive(true);
        ShowBubble("Game Over", "You ran out of hearts.", false);
    }

    // ======================== Spawner bits ========================

    [ContextMenu("Spawn Once")]
    public void SpawnOnce()
    {
        if (!ValidateSpawner()) return;
        if (ended || !stepActive) return; // stop spawning when question is complete or quest ended

        int i = Random.Range(0, spawnPoints.Length);
        Transform p = spawnPoints[i];

        GameObject go = Instantiate(codeBlockPrefab, p.position, p.rotation);

        string text = PickText();
        var tmpUI = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmpUI != null) tmpUI.text = text;
        else
        {
            var tmp = go.GetComponentInChildren<TextMeshPro>(true);
            if (tmp != null) tmp.text = text;
            else Debug.LogWarning("[Shrine2] Spawned prefab has no TMP in children.");
        }
    }

    private IEnumerator SpawnLoop()
    {
        while (maxSpawns == 0 || _spawnedCount < maxSpawns)
        {
            if (ended) yield break;

            // Pause spawning while a question is not active (e.g., all answers caught or in bubble state)
            if (stepActive)
            {
                SpawnOnce();
                _spawnedCount++;
                yield return new WaitForSeconds(spawnInterval);
            }
            else
            {
                yield return null; // wait until stepActive becomes true again
            }
        }
        _loop = null;
    }

    void StartSpawnLoopIfNeeded()
    {
        if (!repeatSpawning || ended) return;
        if (_loop == null) _loop = StartCoroutine(SpawnLoop());
    }

    void StopSpawnLoop()
    {
        if (_loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }
    }

    string PickText()
    {
        if (blockTexts != null && blockTexts.Length > 0)
        {
            int idx = Random.Range(0, blockTexts.Length);
            var chosen = blockTexts[idx];
            if (!string.IsNullOrEmpty(chosen)) return chosen;
        }
        var Q = (questions != null && questions.Length > 0) ? questions[qIndex] : null;
        return Q != null ? (Q.prompt ?? "hello") : "hello";
    }

    bool ValidateSpawner()
    {
        if (codeBlockPrefab == null)
        {
            Debug.LogWarning("[Shrine2] No prefab assigned.");
            return false;
        }
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[Shrine2] No spawn points assigned.");
            return false;
        }
        return true;
    }

    // ======================== Bridge for S2Prefab ========================

    /// <summary>
    /// Called by S2Prefab on trigger. Accepts a line if it's one of this question's correct answers and not already collected.
    /// Distractors or repeats count as wrong. When all required lines are collected, the question completes.
    /// </summary>
    public bool TryConsumeAnswer(string text)
    {
        if (!stepActive || string.IsNullOrWhiteSpace(text)) return false;

        // If this question has no steps, treat as complete (safety)
        if (currentCorrectSeqNorm == null || currentCorrectSeqNorm.Length == 0)
        {
            OnQuestionComplete();
            return true;
        }

        string normText = Norm(text);
        string expected = currentCorrectSeqNorm[Mathf.Clamp(nextRequiredIndex, 0, currentCorrectSeqNorm.Length - 1)];

        // Correct AND in the proper order
        if (normText == expected)
        {
            // Lock the caught line into the forged list (use original text as seen on block)
            if (forgedLinePrefab && forgedListContainer)
            {
                var locked = Instantiate(forgedLinePrefab, forgedListContainer);
                locked.text = text;
            }

            nextRequiredIndex++;

            // Completed the whole ordered sequence -> finish question
            if (nextRequiredIndex >= currentCorrectSeqNorm.Length)
            {
                OnQuestionComplete();
            }

            return true;
        }

        // Wrong: either a distractor OR a correct line but out of order (or a repeat of a previous step)
        HandleWrong(timeout: false);
        return false;
    }


    // ======================== Bubble helpers ========================

    void ShowBubble(string title, string body, bool showNext)
    {
        // Clear current immediately; ensures instruction disappears when next toast shows
        if (bubbleTitle) bubbleTitle.text = "";
        if (bubbleBody) bubbleBody.text = "";
        if (nextButton) nextButton.SetActive(false);
        if (speechbubble) speechbubble.SetActive(false);

        bool visible = (!string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(body) || showNext);
        if (!visible) return;

        if (bubbleTitle) bubbleTitle.text = title ?? "";
        if (bubbleBody) bubbleBody.text = body ?? "";
        if (nextButton) nextButton.SetActive(showNext);
        if (speechbubble) speechbubble.SetActive(true);
    }

    // Hook this to the bubble's Next button
    public void BtnBubbleNext()
    {
        if (ended) return;

        var action = _onBubbleNext;
        _onBubbleNext = null;

        action?.Invoke();
    }

    // ======================== UI helpers / rewards ========================

    void ClearForgedList()
    {
        if (!forgedListContainer) return;
        for (int i = forgedListContainer.childCount - 1; i >= 0; i--)
            Destroy(forgedListContainer.GetChild(i).gameObject);
    }

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

    string Norm(string s)
    {
        if (s == null) return "";
        s = s.Trim().Replace("\r\n", "\n");
        return ignoreCase ? s.ToLowerInvariant() : s;
    }

    // ------------- Buttons on end panels -------------
    public void BtnTryAgain() { if (ended) InitLevel(); }
    public void BtnExitShrine()
    {
        StopSpawnLoop();
        if (!string.IsNullOrEmpty(worldSceneName)) SceneManager.LoadScene(worldSceneName);
        else Debug.LogWarning("worldSceneName is empty.");
    }
}
