// Shrine1Controller.cs
using UnityEngine;
using TMPro;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;   


public class Shrine1Controller : MonoBehaviour
{
    [Header("Data")]
    public ShrineQuestionSet set;

    [Header("World")]
    public CheckpointSlot2D[] slots; // only index 0 is used
    public Transform blocksParent;
    public Transform[] spawnPoints;  // size 4
    public GameObject codeBlockPrefab;

    [Header("Player/Enemy/Anim")]
    public PlayerMovement playerMovement;
    public EnemyAnimator enemyAnimator;

    [Header("UI")]
    public TMP_Text instructionText;
    public GameObject speechbubble;
    public TMP_Text bubbleTitle, bubbleBody;
    public GameObject nextButton;
    public TMP_Text prefixText;
    public TMP_Text suffixText;
    public RectTransform blankAnchor; // optional, not used by controller

    [Header("Hearts & Panels")]
    public HeartUI playerHearts;
    public HeartUI enemyHearts;
    public GameObject questClearedPanel;
    public GameObject questFailedPanel;

    [Header("Rewards/Meta")]
    public string shrineId = "ShrineOne";
    public int rewardXP = 50;
    public UnityEngine.UI.Image[] starFilled;
    public UnityEngine.UI.Image[] starEmpty;
    public TMP_Text scoreNum, coinNum, xpNum;

    [Header("Exit / Replay")]
    [Tooltip("If >= 0, loads this build index on Exit. Overrides Exit Scene Name.")]
    public int exitBuildIndex = -1;
    [Tooltip("If not empty and exitBuildIndex < 0, loads this scene name on Exit.")]
    public string exitSceneName = "";

    int qIndex, playerHp, enemyHp;
    bool ended;
    readonly List<CodeBlock2D> spawned = new();
    int wrongAttemptsThisQuestion = 0; // NEW: per-question wrong counter


    void Start()
    {
        if (set == null || set.questions == null || set.questions.Count == 0)
        {
            Debug.LogWarning("ShrineQuestionSet is empty.");
            return;
        }
        InitLevel();
    }

    void InitLevel()
    {
        CancelInvoke(); // stop pending hurt/clear invokes from prior run
        ended = false;

        questClearedPanel?.SetActive(false);
        questFailedPanel?.SetActive(false);
        nextButton?.SetActive(false);
        ShowBubble("", "", false);

        playerMovement?.isDead(false);
        if (enemyAnimator) enemyAnimator.isHurt(false);
        if (playerMovement) playerMovement.isHurt(false);

        int hearts = Mathf.Max(1, set.questions.Count);
        playerHp = hearts; enemyHp = hearts;
        if (playerHearts) { playerHearts.maxLives = hearts; playerHearts.SetLives(playerHp); }
        if (enemyHearts) { enemyHearts.maxLives = hearts; enemyHearts.SetLives(enemyHp); }

        // clear any occupied/snapped block from previous run
        if (slots != null)
        {
            foreach (var sl in slots)
                if (sl) sl.ClearIfOccupied();
        }
        ClearAllLooseBlocks();

        qIndex = 0;
        LoadQuestion(qIndex);

        ShowBubble("How to play",
            "Press F to pick up and drop the correct block!",
            false);
    }

    void LoadQuestion(int i)
    {
        ClearAllLooseBlocks();

        wrongAttemptsThisQuestion = 0;

        var q = set.questions[i];
        if (instructionText) instructionText.text = q.instruction ?? "";

        // Single active slot
        for (int s = 0; s < (slots?.Length ?? 0); s++)
        {
            bool active = (s == 0);
            if (slots[s])
            {
                slots[s].gameObject.SetActive(active);
                if (active) slots[s].ClearIfOccupied();
            }
        }

        if (prefixText) prefixText.text = q.prefix ?? "";
        if (suffixText) suffixText.text = q.suffix ?? "";

        SpawnAnswerSet(q);
        ShowBubble("", "", false);
    }

    void SpawnAnswerSet(QuestionDef q)
    {
        if (spawnPoints == null || spawnPoints.Length < 4 || codeBlockPrefab == null || blocksParent == null)
        {
            Debug.LogWarning("Assign 4 spawnPoints, a codeBlockPrefab, and blocksParent.");
            return;
        }

        var expectedKind = MapAnswerKind(q.expectedKind);

        var pool = new List<(string text, TokenKind k)>();
        pool.Add((q.correctAnswer, expectedKind));

        var uniqueDistractors = (q.distractors ?? new string[0])
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct()
            .Where(d => d != q.correctAnswer)
            .ToList();

        foreach (var d in uniqueDistractors.Take(3))
            pool.Add((d, expectedKind));

        while (pool.Count < 4 && uniqueDistractors.Count > 0)
            pool.Add((uniqueDistractors[Random.Range(0, uniqueDistractors.Count)], expectedKind));
        while (pool.Count < 4)
            pool.Add((q.correctAnswer, expectedKind));

        // shuffle
        for (int i = 0; i < pool.Count; i++)
        {
            int j = Random.Range(i, pool.Count);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        spawned.Clear();
        for (int i = 0; i < 4; i++)
        {
            var go = Instantiate(codeBlockPrefab, spawnPoints[i].position, spawnPoints[i].rotation, blocksParent);
            var cb = go.GetComponent<CodeBlock2D>();
            var tmp = go.GetComponentInChildren<TMP_Text>(true);

            if (!cb) { Debug.LogWarning("codeBlockPrefab missing CodeBlock2D."); continue; }

            cb.kind = pool[i].k;
            cb.valueText = pool[i].text;
            cb.isSnappedToSlot = false;

            if (tmp)
            {
                tmp.text = cb.valueText;
            }

            spawned.Add(cb);
        }
    }

    // Called from PlayerCarry2D after Snap()
    public void NotifySnapped(CodeBlock2D cb)
    {
        
        var slot = (slots != null && slots.Length > 0) ? slots[0] : null;
        if (!slot || slot.occupied == null) return;
        ValidateCurrent(slot.occupied);
    }

    void ValidateCurrent(CodeBlock2D filled)
    {
        // --- local helpers (scoped to this method) ---
        bool IsQuoted(string s) =>
            !string.IsNullOrEmpty(s) && s.Length >= 2 && s[0] == '"' && s[^1] == '"';

        string StripOuterDoubleQuotes(string s) =>
            IsQuoted(s) ? s.Substring(1, s.Length - 2) : (s ?? string.Empty);
        // ---------------------------------------------

        var q = set.questions[qIndex];
        var expectedKind = MapAnswerKind(q.expectedKind);

        bool kindOk = filled.kind == expectedKind;

        bool valueOk;
        if (expectedKind == TokenKind.StringLiteral)
        {
            // For strings: quotes must match AND inner text must match (case-sensitive as before)
            string a = filled.valueText ?? string.Empty;
            string b = q.correctAnswer ?? string.Empty;

            bool aQuoted = IsQuoted(a);
            bool bQuoted = IsQuoted(b);

            valueOk = (aQuoted == bQuoted) &&
                      string.Equals(
                          StripOuterDoubleQuotes(a),
                          StripOuterDoubleQuotes(b),
                          System.StringComparison.Ordinal
                      );
        }
        else
        {
            // Original behavior for non-strings
            valueOk = string.Equals(
                NormalizeForKind(filled.valueText, filled.kind),
                NormalizeForKind(q.correctAnswer, expectedKind),
                System.StringComparison.Ordinal
            );
        }

        bool ok = kindOk && valueOk;

        if (ok)
        {
            enemyHp = Mathf.Max(0, enemyHp - 1);
            enemyHearts?.SetLives(enemyHp);

            if (enemyAnimator)
            {
                enemyAnimator.isHurt(true);
                Invoke(nameof(EnemyStopHurt), 1.0f);
            }

            string correctMsg = string.IsNullOrWhiteSpace(q.whyCorrect) ? "Nice!" : q.whyCorrect;
            ShowBubble("Correct!", correctMsg, true);

            DestroyAllLooseBlocksExcept(filled);

            if (enemyHp <= 0)
            {
                ended = true;
                Invoke(nameof(EndQuestCleared), 0.5f);
            }
        }
        else
        {
            wrongAttemptsThisQuestion++;

            playerHp = Mathf.Max(0, playerHp - 1);
            playerHearts?.SetLives(playerHp);

            playerMovement?.HurtOverlay(0.8f);


            var slot = slots[0];
            if (slot) slot.ClearIfOccupied();

            string hintText = "That block doesn’t fit the blank. Pick another one.";
            if (q.wrongHints != null && q.wrongHints.Length > 0)
            {
                int idx = (wrongAttemptsThisQuestion - 1) % q.wrongHints.Length;
                if (!string.IsNullOrWhiteSpace(q.wrongHints[idx]))
                    hintText = q.wrongHints[idx];
            }
            ShowBubble("Hint", hintText, false);

            if (playerHp <= 0)
            {
                ended = true;
                Invoke(nameof(EndGameOver), 0.5f);
            }
        }
    }



    void EnemyStopHurt() { if (enemyAnimator) enemyAnimator.isHurt(false); }
    void StopHurt() { if (playerMovement) playerMovement.isHurt(false); }

    void ShowBubble(string title, string body, bool showNext)
    {
        if (bubbleTitle) bubbleTitle.text = title;
        if (bubbleBody) bubbleBody.text = body;
        if (nextButton) nextButton.SetActive(showNext);
        if (speechbubble) speechbubble.SetActive(!string.IsNullOrEmpty(title) || !string.IsNullOrWhiteSpace(body) || showNext);
    }

    public void OnClickNext()
    {
        if (ended) return;
        int next = qIndex + 1;
        if (next >= set.questions.Count) return;
        wrongAttemptsThisQuestion = 0;
        qIndex = next;
        LoadQuestion(qIndex);
    }

    // === NEW ===
    public void OnClickRestart()
    {
        InitLevel();
    }

    // === NEW ===
    public void OnClickExit()
    {
        // Optional: stop any leftover invocations and clear state
        CancelInvoke();
        ClearAllLooseBlocks();
        if (slots != null && slots.Length > 0) slots[0]?.ClearIfOccupied();

        if (exitBuildIndex >= 0)
        {
            SceneManager.LoadScene(exitBuildIndex);
            return;
        }

        if (!string.IsNullOrEmpty(exitSceneName))
        {
            SceneManager.LoadScene(exitSceneName);
            return;
        }

        // Fallbacks
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void EndQuestCleared()
    {
        enemyAnimator.isDead(true);

        // 1) Stars from hearts (clamped 1..3)
        int stars = RewardsHelper.ComputeStarsFromHearts(playerHp);
        UpdateStarsUI(stars);

        // 2) Score/coins from stars (via RewardsHelper)
        var (score, coins) = RewardsHelper.ComputeRewards(stars);

        // 3) Update the panel numbers (your existing UI)
        if (scoreNum) scoreNum.text = score.ToString();
        if (coinNum) coinNum.text = coins.ToString();
        if (xpNum) xpNum.text = rewardXP.ToString();

        _ = SaveAllAsync(); // fire-and-forget to avoid blocking UI
        questClearedPanel?.SetActive(true);

        async Task SaveAllAsync()
        {
            try
            {
                await RewardsHelper.SaveRewardsXpAndSubmitAsync(
                    shrineId: shrineId,         
                    stars: stars,
                    score: score,
                    coins: coins,
                    rewardXp: rewardXP,
                    leaderboardId: CodiumLeaderboards.DefaultId
                );
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Shrine1] SaveAll failed: {ex.Message}");
            }

           
        } 

        _ = SaveShrine1MetaAsync();

        async System.Threading.Tasks.Task SaveShrine1MetaAsync()
            {
                try
                {
                    var keys = new HashSet<string> { $"best_score_{shrineId}" };
                    var loaded = await Unity.Services.CloudSave.CloudSaveService.Instance.Data.Player.LoadAsync(keys);
                    int currentBest = loaded.TryGetValue($"best_score_{shrineId}", out var v) ? v.Value.GetAs<int>() : 0;
                    int newBest = Mathf.Max(currentBest, score);

                    var toSave = new Dictionary<string, object> {
            { $"cleared_{shrineId}", true },
            { $"best_score_{shrineId}", newBest }
        };
                    await Unity.Services.CloudSave.CloudSaveService.Instance.Data.Player.SaveAsync(toSave);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[Shrine1] Save meta failed: {ex.Message}");
                }
            }

    }


    void EndGameOver()
    {
        if (playerMovement) playerMovement.isDead(true);
        questFailedPanel?.SetActive(true);
        ShowBubble("Game Over", "You ran out of hearts.", false);   
    }


    void ClearAllLooseBlocks()
    {
        if (!blocksParent) return;
        var blocks = blocksParent.GetComponentsInChildren<CodeBlock2D>(true);
        foreach (var b in blocks) if (!b.isSnappedToSlot) Destroy(b.gameObject);
        spawned.Clear();
    }

    void DestroyAllLooseBlocksExcept(CodeBlock2D keep)
    {
        if (!blocksParent) return;

        var blocks = blocksParent.GetComponentsInChildren<CodeBlock2D>(true);
        foreach (var b in blocks)
        {
            if (!b) continue;
            if (b == keep) continue;
            Destroy(b.gameObject);
        }

        spawned.Clear();
        if (keep) spawned.Add(keep);
    }


    void UpdateStarsUI(int stars)
    {
        for (int i = 0; i < 3; i++)
        {
            bool filled = i < stars;
            if (starFilled != null && i < starFilled.Length && starFilled[i])
            {
                starFilled[i].enabled = filled;
                starFilled[i].raycastTarget = false;
            }
            if (starEmpty != null && i < starEmpty.Length && starEmpty[i])
            {
                starEmpty[i].enabled = !filled;
                starEmpty[i].raycastTarget = false;
            }
        }
    }

    static TokenKind MapAnswerKind(AnswerKind k) =>
        k switch
        {
            AnswerKind.StringLiteral => TokenKind.StringLiteral,
            AnswerKind.Number => TokenKind.Number,
            _ => TokenKind.Identifier
        };

    static string NormalizeForKind(string raw, TokenKind k)
    {
        if (k == TokenKind.StringLiteral && !string.IsNullOrEmpty(raw) && raw.Length >= 2 &&
            raw[0] == '"' && raw[raw.Length - 1] == '"')
            return raw.Substring(1, raw.Length - 2);
        return raw;
    }
}
