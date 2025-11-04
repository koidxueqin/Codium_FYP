using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;     // ADD
using UnityEngine.UI;            // ADD (if you wire star/score UI)


public enum S3State { Intro, AwaitInput, BlockSpawned, Carrying, InSubmitSnap, Resolving, NextQuestion, Win, Lose }

public class Shrine3Controller : MonoBehaviour
{
    [Header("Data")]
    public Shrine3Set set;

    [Header("World")]
    public Transform blocksParent;
    public Transform spawnPoint;
    public GameObject codeBlockPrefab;

    [Header("Slots")]
    public CheckpointSlot2D submitSlot;
    public CheckpointSlot2D trashSlot;

    [Header("Player/Carry")]
    public PlayerCarry3 playerCarry;     // new script below
    public Transform carryAnchor;

    [Header("UI")]
    public TMP_Text bubbleTitle;
    public TMP_Text bubbleBody;
    public GameObject speechbubble;
    public TMP_Text timerText;
    public CodeRunnerUI3 runner;         // new script below

    [Header("Hearts & Panels")]
    public HeartUI playerHearts;
    public HeartUI enemyHearts;
    public GameObject questClearedPanel;
    public GameObject questFailedPanel;

    [Header("Config")]
    public int playerHeartsStart = 6;
    public int enemyHeartsStart = 6;

    [Header("Runtime (read-only)")]
    public S3State state;
    public int qIndex;
    public float timeLeft;
    public int score;

    [Header("Rewards UI")]           
    public Image[] starFilled;       
    public Image[] starEmpty;      
    public TMP_Text scoreNum;       
    public TMP_Text coinNum;
    [SerializeField] int rewardXP;
    [SerializeField] TMP_Text xpNum; 

    [Header("IDs / Save")]           
    public string shrineId = "ShrineThree"; 


    CodeBlock2D activeBlock; // one active block policy
    public CostHeartsUI costHeartsUI;


    void Awake()
    {
        state = S3State.Intro;
        if (playerHearts) playerHearts.SetHearts(playerHeartsStart);
        if (enemyHearts) enemyHearts.SetHearts(enemyHeartsStart);
        if (playerCarry) playerCarry.Init(this, carryAnchor, submitSlot, trashSlot);
        if (runner) runner.Init(this, spawnPoint, blocksParent, codeBlockPrefab);
    }

    void Start()
    {
        if (set == null || set.questions == null || set.questions.Length == 0)
        {
            Debug.LogError("[Shrine3] No questions set.");
            return;
        }
        ShowQuestion(0);
    }

    void Update()
    {
        if (state == S3State.AwaitInput || state == S3State.BlockSpawned || state == S3State.Carrying)
        {
            timeLeft -= Time.deltaTime;
            UpdateTimerUI();
            if (timeLeft <= 0f) OnTimerExpired();
        }
    }

    void ShowQuestion(int i)
    {
        qIndex = i;
        var q = set.questions[qIndex];
        timeLeft = Mathf.Max(1f, q.timeLimitSeconds);
        speechbubble.SetActive(true);
        if (bubbleTitle) bubbleTitle.text = $"Question {qIndex + 1}";
        if (bubbleBody) bubbleBody.text = q.prompt;
        if (costHeartsUI) costHeartsUI.SetCost(q.costHearts);
        UpdateTimerUI();
        UnlockRunner();
        state = S3State.AwaitInput;
        ClearSlots();
        DestroyActiveBlockIfAny();
    }

    void UpdateTimerUI()
    {
        if (!timerText) return;
        timerText.text = Mathf.CeilToInt(Mathf.Max(0f, timeLeft)).ToString();
    }

    void OnTimerExpired()
    {
        timeLeft = 0f;
        PlayerLoseHearts(1);
        DestroyActiveBlockIfAny();
        UnlockRunner();
        if (CheckLose()) return;
        AdvanceOrWin();
    }

    void ClearSlots()
    {
        if (submitSlot) submitSlot.ClearIfOccupied();
        if (trashSlot) trashSlot.ClearIfOccupied();
    }

    void DestroyActiveBlockIfAny()
    {
        if (activeBlock)
        {
            Destroy(activeBlock.gameObject);
            activeBlock = null;
        }
    }

    public bool CanSpawnBlock() => state == S3State.AwaitInput && activeBlock == null;

    public void SpawnBlockWithText(string text, Camera camForCanvas)
    {
        if (!CanSpawnBlock()) { Debug.Log("[Shrine3] Spawn blocked."); return; }
        var go = Instantiate(codeBlockPrefab, spawnPoint.position, Quaternion.identity, blocksParent);
        var cb = go.GetComponent<CodeBlock2D>();
        if (!cb) { Debug.LogError("[Shrine3] CodeBlock2D missing on prefab."); Destroy(go); return; }

        cb.Init(TokenKind.Invalid, text); // we ignore token kind; just print text
        if (cb.canvas) cb.canvas.worldCamera = camForCanvas;

        activeBlock = cb;
        LockRunner();
        state = S3State.BlockSpawned;
    }

    public void NotifyPickedUp(CodeBlock2D cb)
    {
        if (cb == activeBlock) state = S3State.Carrying;
    }

    public void NotifyDroppedFree(CodeBlock2D cb)
    {
        if (cb == activeBlock) state = S3State.BlockSpawned;
    }

    public void NotifyDroppedToTrash(CheckpointSlot2D slot, CodeBlock2D cb)
    {
        if (cb != activeBlock) return;
        slot.Snap(cb); // snap visually, then destroy
        Destroy(cb.gameObject);
        activeBlock = null;
        UnlockRunner();
        state = S3State.AwaitInput;
    }

    public void NotifyDroppedToSubmit(CheckpointSlot2D slot, CodeBlock2D cb)
    {
        if (cb != activeBlock) return;
        slot.Snap(cb);
        state = S3State.Resolving;
        ValidateSubmission(cb.valueText);
        Destroy(cb.gameObject);
        activeBlock = null;
        UnlockRunner();
    }

    void ValidateSubmission(string payload)
    {
        var q = set.questions[qIndex];
        bool pass = CheckAccepted(q, payload);

        if (pass)
        {
            EnemyLoseHearts(q.costHearts);
            score += q.costHearts;
            if (enemyHearts && enemyHearts.CurrentHearts <= 0) { OnWin(); return; }
            AdvanceOrWin();
        }
        else
        {
            PlayerLoseHearts(q.costHearts);
            ShowFailHint(q);
            if (CheckLose()) return;
            state = S3State.AwaitInput; // retry permitted
        }
    }

    static bool CheckAccepted(Shrine3Question q, string payload)
    {
        string p = (payload ?? "").Trim();
        if (q.acceptedAnswers != null && q.acceptedAnswers.Length > 0)
        {
            if (q.acceptedAnswers.Any(a => string.Equals(a?.Trim(), p, System.StringComparison.OrdinalIgnoreCase)))
                return true;
        }
        if (q.acceptedRegex != null && q.acceptedRegex.Length > 0)
        {
            foreach (var pat in q.acceptedRegex)
            {
                if (string.IsNullOrEmpty(pat)) continue;
                if (Regex.IsMatch(p, pat, RegexOptions.IgnoreCase)) return true;
            }
        }
        return false;
    }

    void ShowFailHint(Shrine3Question q)
    {
        if (bubbleBody && q.failHints != null && q.failHints.Length > 0)
            bubbleBody.text = q.failHints[Random.Range(0, q.failHints.Length)];
    }

    void AdvanceOrWin()
    {
        if (qIndex + 1 >= set.questions.Length) { OnWin(); return; }
        ShowQuestion(qIndex + 1);
    }

    void OnWin()
    {
        state = S3State.Win;
        SaveProgress();
        LockRunner();
        if (questClearedPanel) questClearedPanel.SetActive(true);

        int remaining = playerHearts ? playerHearts.CurrentHearts : 0;
        int stars = Mathf.Clamp(remaining, 1, 3);
        UpdateStarsUI(stars);

        var (scoreVal, coinsVal) = ComputeRewards(stars);
        if (scoreNum) scoreNum.text = scoreVal.ToString();
        if (coinNum) coinNum.text = coinsVal.ToString();
        if (xpNum) xpNum.text = rewardXP.ToString();

        _ = SaveAllAsync();   // fire-and-forget

        async System.Threading.Tasks.Task SaveAllAsync()
        {
            try
            {
                // Save to Cloud Save, then submit TOTAL score to leaderboard "codium_total"
                await RewardsHelper.SaveRewardsXpAndSubmitAsync(
                    shrineId, stars, scoreVal, coinsVal, rewardXP, CodiumLeaderboards.DefaultId
                );
                // (Optional) If this scene shows a board, you can force a refresh here.
                // FindObjectOfType<LeaderboardUI>()?.RefreshOnce();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Shrine3] SaveAll failed: {ex.Message}");
            }
        }
    }



    bool CheckLose()
    {
        if (playerHearts && playerHearts.CurrentHearts <= 0)
        {
            state = S3State.Lose;
            if (questFailedPanel) questFailedPanel.SetActive(true);
            LockRunner();
            return true;
        }
        return false;
    }

    void PlayerLoseHearts(int n)
    {
        if (playerHearts) playerHearts.Damage(n);
    }

    void EnemyLoseHearts(int n)
    {
        if (enemyHearts) enemyHearts.Damage(n);
    }

    void LockRunner() { if (runner) runner.SetLocked(true); }
    void UnlockRunner() { if (runner) runner.SetLocked(false); }

    void SaveProgress()
    {
        // Minimal placeholder persistence; replace with your existing save system.
        PlayerPrefs.SetInt("shrine3_cleared", 1);
        PlayerPrefs.SetInt("shrine3_score", score);
        PlayerPrefs.SetInt("shrine3_stars", ComputeStars());
        PlayerPrefs.Save();
    }

    int ComputeStars()
    {
        if (!set) return 0;
        if (score >= set.threeStarMinScore) return 3;
        if (score >= set.twoStarMinScore) return 2;
        if (score >= set.oneStarMinScore) return 1;
        return 0;
    }

    // -------- Replay / Exit handlers --------
    public void BtnPlayAgain()
    {
        // Ensure unpaused before reload
        if (Time.timeScale != 1f) Time.timeScale = 1f;

        // Reload current scene
        var idx = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(idx, LoadSceneMode.Single);
    }

    [Tooltip("Scene to load when exiting the shrine (e.g., hub/overworld). Leave empty to reload current.")]
    public string exitSceneName = "Hub";

    public void BtnExitShrine()
    {
        if (Time.timeScale != 1f) Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(exitSceneName))
            SceneManager.LoadScene(exitSceneName, LoadSceneMode.Single);
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex, LoadSceneMode.Single);
    }

    void UpdateStarsUI(int stars)    // ADD
    {
        for (int i = 0; i < 3; i++)
        {
            bool filled = i < stars;
            if (starFilled != null && i < starFilled.Length && starFilled[i]) { starFilled[i].enabled = filled; starFilled[i].raycastTarget = false; }
            if (starEmpty != null && i < starEmpty.Length && starEmpty[i]) { starEmpty[i].enabled = !filled; starEmpty[i].raycastTarget = false; }
        }
    }

    (int score, int coins) ComputeRewards(int stars) // ADD
    {
        int score = stars * 500;
        int coins = stars switch { 3 => 100, 2 => 60, _ => 30 };
        return (score, coins);
    }


}
