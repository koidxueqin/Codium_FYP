// ShrineController2D.cs
using UnityEngine;
using TMPro;
using System.Linq;
using System.Text;

public class ShrineController2D : MonoBehaviour
{
    [Header("Data")]
    public ShrineQuestionSet set;

    [Header("World")]
    public CheckpointSlot2D[] slots; // size 3 in inspector; we enable only needed
    public Transform blocksParent;

    [Header("Player/Enemy/Anim")]
    public PlayerMovement playerMovement;
    public EnemyAnimator enemyAnimator;

    [Header("UI: IDE & Bubble")]
    public TMP_Text instructionText;
    public CodeRunnerUI2D runner;
    public TMP_Text promptText;            // multi-line template
    public GameObject speechbubble;
    public TMP_Text bubbleTitle, bubbleBody;
    public GameObject nextButton;          // shown only when correct

    [Header("UI: Hearts & Panels")]
    public HeartUI playerHearts;
    public HeartUI enemyHearts;
    public GameObject questClearedPanel;
    public GameObject questFailedPanel;

    [Header("Rewards/Meta (reuse yours)")]
    public string shrineId = "ShrineOne";
    public int rewardXP = 50;
    public UnityEngine.UI.Image[] starFilled;
    public UnityEngine.UI.Image[] starEmpty;
    public TMP_Text scoreNum, coinNum, xpNum;

    // runtime
    int qIndex, playerHp, enemyHp;
    bool ended;

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
        ended = false;
        if (questClearedPanel) questClearedPanel.SetActive(false);
        if (questFailedPanel) questFailedPanel.SetActive(false);
        if (playerMovement) playerMovement.isDead(false);

        int hearts = set.questions.Count;
        playerHp = hearts; enemyHp = hearts;
        playerHearts?.SetLives(playerHp); playerHearts.maxLives = hearts;
        enemyHearts?.SetLives(enemyHp); enemyHearts.maxLives = hearts;

        qIndex = 0;
        LoadQuestion(qIndex);
        ClearAllLooseBlocks();

        // Intro message
        ShowBubble(
            "How to play",
            "Type a value (\"text\" or numbers) and press Run to spawn a block.\nStand on the slot and press F to drop it.\nFill all blanks correctly to damage the enemy!",
            false
        );


    }

    public void NotifySpawned(CodeBlock2D cb)
    {
        // Hide intro if it’s still up
        ShowBubble("", "", false);
    }


    public void NotifySnapped(CodeBlock2D cb)
    {
        if (runner != null) runner.NotifySnapped(cb);
    }



    void LoadQuestion(int i)
    {
        var q = set.questions[i];
        instructionText.text = q.instruction ?? "";

        // enable only needed slots
        for (int s = 0; s < slots.Length; s++)
        {
            bool active = s < q.blanksCount;
            if (slots[s])
            {
                slots[s].gameObject.SetActive(active);
                if (active) slots[s].ClearIfOccupied();
            }
        }

        // show code template lines
        promptText.text = string.Join("\n", q.codeTemplateLines);

        // clear spawned loose block at spawn if any (not penalizing)
        runner?.ShowError(""); // hide old error label
    }

    public void OnAnySlotUpdated()
    {
        // If all active slots filled -> validate
        var q = set.questions[qIndex];
        var act = slots.Take(q.blanksCount).ToArray();
        if (act.Any(s => s.occupied == null)) return;
        Validate(q, act);
    }

    void Validate(QuestionDef q, CheckpointSlot2D[] act)
    {
        bool ok = q.mode == ValidationMode.PerBlankExact ? ValidatePerBlank(q, act)
                                                        : ValidateEvaluate(q, act);

        if (ok)
        {
            enemyHp = Mathf.Max(0, enemyHp - 1);
            enemyHearts?.SetLives(enemyHp);

            if (enemyAnimator)
            {
                enemyAnimator.Hurt(true);
                Invoke(nameof(EnemyStopHurt), 1.0f);
            }

            ShowBubble("Correct!", string.IsNullOrWhiteSpace(q.whyCorrect) ? "Nice!" : q.whyCorrect, true);

            if (enemyHp <= 0)
            {
                ended = true;
                Invoke(nameof(EndQuestCleared), 0.5f);
            }
        }
        else
        {
            // wrong: player loses 1 heart, clear only wrong blanks & show hints
            playerHp = Mathf.Max(0, playerHp - 1);
            playerHearts?.SetLives(playerHp);

            if (playerMovement)
            {
                playerMovement.isHurt(true);
                Invoke(nameof(StopHurt), 1.0f);
            }

            if (playerHp <= 0)
            {
                ended = true;
                Invoke(nameof(EndGameOver), 0.5f);
                return;
            }

            ShowWrongHintsAndClear(q, act);
        }
    }

    bool ValidatePerBlank(QuestionDef q, CheckpointSlot2D[] act)
    {
        for (int i = 0; i < q.blanksCount; i++)
        {
            var rule = q.blanks[i];
            var cb = act[i].occupied;
            if (!cb) return false;

            // Map CodeBlock2D kind -> AnswerKind
            bool kindOk =
                (rule.kind == AnswerKind.StringLiteral && cb.kind == TokenKind.StringLiteral) ||
                (rule.kind == AnswerKind.Number && cb.kind == TokenKind.Number) ||
                (rule.kind == AnswerKind.Identifier && cb.kind == TokenKind.Identifier);

            if (!kindOk) return false;

            // If acceptedValues provided, require exact text match (for strings, include quotes in data)
            if (rule.acceptedValues != null && rule.acceptedValues.Count > 0)
            {
                string candidate = cb.kind == TokenKind.StringLiteral ? $"\"{cb.valueText}\"" : cb.valueText;
                if (!rule.acceptedValues.Contains(candidate)) return false;
            }
        }
        return true;
    }

    bool ValidateEvaluate(QuestionDef q, CheckpointSlot2D[] act)
    {
        // For evaluate mode we only care about total result (Q3).
        // Expect Number tokens in all blanks. If any not number -> wrong.
        foreach (var s in act) if (s.occupied.kind != TokenKind.Number) return false;

        int a = int.Parse(act[0].occupied.valueText);
        int b = int.Parse(act[1].occupied.valueText);
        int c = int.Parse(act[2].occupied.valueText);
        int result = a + b * c;
        return result == q.targetValue;
    }

    void ShowWrongHintsAndClear(QuestionDef q, CheckpointSlot2D[] act)
    {
        var sb = new StringBuilder();

        if (q.mode == ValidationMode.PerBlankExact)
        {
            for (int i = 0; i < q.blanksCount; i++)
            {
                var rule = q.blanks[i];
                var s = act[i];
                var cb = s.occupied;
                bool kindOk =
                    (rule.kind == AnswerKind.StringLiteral && cb.kind == TokenKind.StringLiteral) ||
                    (rule.kind == AnswerKind.Number && cb.kind == TokenKind.Number) ||
                    (rule.kind == AnswerKind.Identifier && cb.kind == TokenKind.Identifier);

                bool valueOk = true;
                if (kindOk && rule.acceptedValues != null && rule.acceptedValues.Count > 0)
                {
                    string candidate = cb.kind == TokenKind.StringLiteral ? $"\"{cb.valueText}\"" : cb.valueText;
                    valueOk = rule.acceptedValues.Contains(candidate);
                }

                if (!kindOk || !valueOk)
                {
                    sb.AppendLine($"Blank {i + 1}: {rule.hintForWrong}");
                    s.ClearIfOccupied();
                }
            }
        }
        else
        {
            // Evaluate mode wrong: clear ALL and show general hint
            foreach (var s in act) s.ClearIfOccupied();
            sb.AppendLine(string.IsNullOrWhiteSpace(q.whyWrongGeneral)
                ? "Adjust your numbers so a + b * c equals the target."
                : q.whyWrongGeneral);
        }

        ShowBubble("Try again", sb.ToString(), false);
    }

    void EnemyStopHurt() { if (enemyAnimator) enemyAnimator.Hurt(false); }
    void StopHurt() { if (playerMovement) playerMovement.isHurt(false); }

    void ShowBubble(string title, string body, bool showNext)
    {
        if (bubbleTitle) bubbleTitle.text = title;
        if (bubbleBody) bubbleBody.text = body;
        if (nextButton) nextButton.SetActive(showNext);
        if (speechbubble) speechbubble.SetActive(!string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(body) || showNext);
    }

    public void OnClickNext()
    {
        if (ended) return;
        int next = qIndex + 1;
        if (next >= set.questions.Count) return;
        qIndex = next;
        LoadQuestion(qIndex);
        ShowBubble("", "", false);
        ClearAllLooseBlocks();
    }

    public void PenalizeInvalidInput(string msg)
    {
        // Called from runner when token is invalid (e.g., missing quotes for strings)
        playerHp = Mathf.Max(0, playerHp - 1);
        playerHearts?.SetLives(playerHp);
        if (playerMovement)
        {
            playerMovement.isHurt(true);
            Invoke(nameof(StopHurt), 1.0f);
        }
        if (playerHp <= 0) { ended = true; Invoke(nameof(EndGameOver), 0.4f); }
        // Also surface the message in the bubble (optional)
        ShowBubble("Invalid input", msg, false);
    }

    void EndQuestCleared()
    {
        // Rewards flow (reuse your UI + RewardsHelper)
        int stars = Mathf.Clamp(playerHp, 1, 3);
        UpdateStarsUI(stars);
        var (score, coins) = ComputeRewards(stars);
        if (scoreNum) scoreNum.text = score.ToString();
        if (coinNum) coinNum.text = coins.ToString();
        if (xpNum) xpNum.text = rewardXP.ToString();

        // Save (same pattern as your previous controller)
        _ = SaveAllAsync();

        if (questClearedPanel) questClearedPanel.SetActive(true);

        async System.Threading.Tasks.Task SaveAllAsync()
        {
            try
            {
                await RewardsHelper.SaveRewardsAndXpAsync(shrineId, stars, score, coins, rewardXP);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"SaveAll failed: {ex.Message}");
            }
        }
    }

    void EndGameOver()
    {
        if (playerMovement) playerMovement.isDead(true);
        if (questFailedPanel) questFailedPanel.SetActive(true);
    }

    void ClearAllLooseBlocks()
    {
        if (!blocksParent) return;
        var blocks = blocksParent.GetComponentsInChildren<CodeBlock2D>(true);
        foreach (var b in blocks) if (!b.isSnappedToSlot) Destroy(b.gameObject);
    }

    // --- Helpers (stars/rewards; mirrors your old controller) ---
    (int score, int coins) ComputeRewards(int stars)
    {
        int score = stars * 500;
        int coins = stars switch { 3 => 100, 2 => 60, _ => 30 };
        return (score, coins);
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
}
