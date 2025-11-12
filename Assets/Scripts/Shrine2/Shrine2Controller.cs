using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Services.CloudSave; 


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

    // -------------- Spawn Weights / Anti-repetition --------------
    [Header("Spawn Weights")]
    [Range(0f, 1f)] public float correctPickChance = 0.45f; // ~45% chance
    [Tooltip("Guarantee the expected line at least this often (0 = no forcing).")]
    public int forceExpectedEveryN = 4;
    private int _sinceExpected = 0;

    [Tooltip("Don’t repeat the same distractor in this many recent spawns.")]
    public int noRepeatWindow = 2;
    private readonly Queue<string> _recent = new();

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

        // >>> ADD: reset enemy animator flags on level start
        if (enemyAnimator)
        {
            enemyAnimator.isHurt(false);
            enemyAnimator.isDead(false);
        }

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

        _sinceExpected = 0;
        _recent.Clear();
    }


    void OnQuestionComplete()
    {
        stepActive = false;
        StopSpawnLoop();

        // Enemy loses 1 life
        enemyLives = Mathf.Max(0, enemyLives - 1);
        if (enemyHearts) enemyHearts.SetLives(enemyLives);

        if (enemyLives <= 0)
        {
            // >>> Ensure hurt pulses will not flip any flags back
            CancelInvoke(nameof(EnemyStopHurt));

            if (enemyAnimator)
            {
                enemyAnimator.isHurt(false);
                enemyAnimator.isDead(true);  // <- will set bool if available, else crossfade to death state
            }

            EndQuestCleared();  // rewards + panels
            return;
        }

        // Not dead → play hurt
        if (enemyAnimator)
        {
            enemyAnimator.isHurt(true);
            Invoke(nameof(EnemyStopHurt), 1.15f);
        }


        // >>> NEW: only show the explanation for the LAST step we just completed
        var Q = questions[qIndex];
        int lastStep = Mathf.Max(0, (currentCorrectSeqNorm?.Length ?? 1) - 1);
        string explain = GetStepSafe(Q.stepExplanations, lastStep, "Well done.");

        _onBubbleNext = () =>
        {
            qIndex = Mathf.Clamp(qIndex + 1, 0, questions.Length - 1);
            // Clear current bubble immediately before next content
            ShowBubble("", "", false);
            LoadQuestion();
        };

        ShowBubble("Sequence Complete", explain, true);
    }



    void HandleWrong(bool timeout)
    {
        // Penalize
        playerLives = Mathf.Max(0, playerLives - 1);
        if (playerHearts) playerHearts.SetLives(playerLives);
        // Lock in hurt for N seconds and ignore inputs during that time
        if (playerMovement) playerMovement.HurtOverlay(1f); 


        if (playerLives <= 0)
        {
            EndGameOver();
            return;
        }

        var Q = questions[qIndex];


        string fallback = timeout ? "Time’s up." : "Not the required line.";
        string stepHint = GetStepSafe(Q.stepHints, nextRequiredIndex, fallback);

        ShowSticky(timeout ? "Timeout" : "Wrong", stepHint);


        // Resume/advance logic
        if (!timeout)
        {
            stepActive = true;
        }
        else
        {
            // On timeout, advance question
            qIndex = Mathf.Clamp(qIndex + 1, 0, questions.Length - 1);
            Invoke(nameof(LoadQuestion), 0.3f);
        }
    }


    void EnemyStopHurt() { if (enemyAnimator) enemyAnimator.isHurt(false); }
    void StopHurt() { if (playerMovement) playerMovement.isHurt(false); }

    void EndQuestCleared()
    {
        ended = true;
        StopSpawnLoop();
        stepActive = false;
        repeatSpawning = false;

        if (enemyAnimator) enemyAnimator.isDead(true);

        // Stars from hearts (clamped 1..3)
        int stars = RewardsHelper.ComputeStarsFromHearts(playerLives);
        UpdateStarsUI(stars);

        // Score/coins from stars
        var (score, coins) = RewardsHelper.ComputeRewards(stars);

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
                // 1) Save totals + best stars + XP, then submit TOTAL score to leaderboard
                await RewardsHelper.SaveRewardsXpAndSubmitAsync(
                    shrineId,
                    stars,
                    score,
                    coins,
                    rewardXP,
                    CodiumLeaderboards.DefaultId
                );

                // 2) Save shrine meta: cleared flag + best per-shrine score
                await SaveShrine2MetaAsync(score);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Shrine2] SaveAll failed: {ex.Message}");
            }
        }
    }

    async Task SaveShrine2MetaAsync(int runScore)
    {
        try
        {
            string bestScoreKey = $"best_score_{shrineId}";
            string clearedKey = $"cleared_{shrineId}";

            // Load current best score, if any
            var keys = new HashSet<string> { bestScoreKey };
            var loaded = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

            int currentBest = loaded.TryGetValue(bestScoreKey, out var v) ? v.Value.GetAs<int>() : 0;
            int newBest = Mathf.Max(currentBest, runScore);

            var toSave = new Dictionary<string, object> {
            { clearedKey, true },
            { bestScoreKey, newBest }
        };

            await CloudSaveService.Instance.Data.Player.SaveAsync(toSave);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Shrine2] Save meta failed: {ex.Message}");
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
        var Q = (questions != null && questions.Length > 0) ? questions[qIndex] : null;
        if (Q == null) return "hello";

        // Determine next expected line (original, not normalized)
        string expectedOriginal = null;
        if (Q.correctOrder != null && nextRequiredIndex >= 0 && nextRequiredIndex < Q.correctOrder.Length)
            expectedOriginal = Q.correctOrder[nextRequiredIndex];

        // Force the expected every N spawns
        if (forceExpectedEveryN > 0 && _sinceExpected >= forceExpectedEveryN && !string.IsNullOrEmpty(expectedOriginal))
        {
            _sinceExpected = 0;
            RememberRecent(expectedOriginal);
            return expectedOriginal;
        }

        // Probabilistic bias toward expected
        if (!string.IsNullOrEmpty(expectedOriginal) && Random.value < correctPickChance)
        {
            _sinceExpected = 0;
            RememberRecent(expectedOriginal);
            return expectedOriginal;
        }

        // Otherwise, pick a distractor or (other) text avoiding recent repeats
        // Build a candidate list that excludes the expected line to vary the stream
        var pool = new List<string>(blockTexts ?? System.Array.Empty<string>());
        if (!string.IsNullOrEmpty(expectedOriginal))
            pool.RemoveAll(s => Norm(s) == Norm(expectedOriginal));

        // Filter out recent repeats if requested
        if (noRepeatWindow > 0 && _recent.Count > 0)
            pool.RemoveAll(s => _recent.Contains(s));

        // Safety fallbacks
        if (pool.Count == 0)
        {
            // If everything got filtered, return expected to keep gameplay moving
            if (!string.IsNullOrEmpty(expectedOriginal))
            {
                _sinceExpected = 0;
                RememberRecent(expectedOriginal);
                return expectedOriginal;
            }
            // Or return something simple
            return Q.prompt ?? "hello";
        }

        var chosen = pool[Random.Range(0, pool.Count)];
        if (string.IsNullOrEmpty(chosen))
            chosen = Q.prompt ?? "hello";

        _sinceExpected++;
        RememberRecent(chosen);
        return chosen;
    }

    void RememberRecent(string s)
    {
        if (noRepeatWindow <= 0 || string.IsNullOrEmpty(s)) return;
        _recent.Enqueue(s);
        while (_recent.Count > noRepeatWindow) _recent.Dequeue();
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

        if (normText == expected)
        {
            // Lock the caught line into the forged list (use original text as seen on block)
            if (forgedLinePrefab && forgedListContainer)
            {
                var locked = Instantiate(forgedLinePrefab, forgedListContainer);
                locked.text = text;
            }

            // >>> NEW: show explanation for THIS step (before we increment)
            var Q = questions[qIndex];
            string stepExplain = GetStepSafe(Q.stepExplanations, nextRequiredIndex, "Correct.");
            ShowSticky("Correct", stepExplain);


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

    static string GetStepSafe(string[] arr, int i, string fallback = "")
    {
        if (arr == null || i < 0 || i >= arr.Length) return fallback;
        var s = arr[i];
        return string.IsNullOrWhiteSpace(s) ? fallback : s;
    }


    void ShowSticky(string title, string body)
    {
        // No timers, no clearing — just replace what’s displayed.
        ShowBubble(title, body, showNext: false);
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
