using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine.UI;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Leaderboards;

[System.Serializable]
public class Question
{
    [TextArea] public string prompt;      
    [TextArea] public string contextLine; 
    public string prefix;                 
    public string suffix;                 
    public string correct;                
    [TextArea] public string hint;
    [TextArea] public string explainCorrect;
    public string[] options = new string[3]; 

}



public class SimpleGameController : MonoBehaviour
{
    public static SimpleGameController Instance { get; private set; }

    [Header("Characters")]
    public PlayerMovement playerMovement;
    public EnemyAnimator enemyAnimator;

    [Header("Scene References")]
    public DropSlot slot1;
    public Transform tokensRow;
    public DragToken tokenPrefab;

    [Header("UI Text")]
    public TMP_Text promptText;       
    public TMP_Text contextLineText;  
    public TMP_Text prefixText;     
    public TMP_Text suffixText;       
    public TMP_Text feedbackText;     
    public CanvasGroup feedbackToast; 

    [Header("Hearts")]
    public HeartUI playerHearts;   
    public HeartUI enemyHearts;   

    [Header("Lives")]
    public int playerMaxLives = 3;

    [Header("Questions")]
    public Question[] questions;   // set in Inspector or use defaults

    [Header("End Panels")]
    public GameObject questClearedPanel; 
    public GameObject gameOverPanel;     
    public string worldSceneName = "WorldPage";

    [Header("Rewards UI (Quest Cleared Panel)")]
    public Image[] starFilled;   
    public Image[] starEmpty;    
    public TMP_Text scoreNum;    
    public TMP_Text coinNum;

    [Header("XP Rewards")]
    [SerializeField] int rewardXP = 50;
    [SerializeField] TMP_Text xpNum; // optional on panel


    [Header("IDs / Save")]
    public string shrineId = "ShrineOne"; // used for per-shrine best tracking 

    // Runtime cache to prevent double-grant
    bool rewardsGranted = false;


    // Runtime
    int playerLives;
    int enemyLives;
    int qIndex;
    bool ended;

    Question Q => questions[qIndex];

    void Awake() => Instance = this;

    void Start()
    {
        if (questions == null || questions.Length == 0)
        {
            Debug.LogWarning("No questions set in the Inspector for SimpleGameController.");
            return;
        }
        InitLevel();

    }

    void InitLevel()
    {
        ended = false;
        playerMovement.isDead(false);

        playerLives = playerMaxLives;
        enemyLives = Mathf.Max(1, questions.Length);

        if (playerHearts) { playerHearts.maxLives = playerMaxLives; playerHearts.SetLives(playerLives); }
        if (enemyHearts) { enemyHearts.maxLives = enemyLives; enemyHearts.SetLives(enemyLives); }

        if (questClearedPanel) questClearedPanel.SetActive(false);
        if (gameOverPanel) gameOverPanel.SetActive(false);

        qIndex = 0;
        LoadQuestion();
        HideToast();
    }

    void LoadQuestion()
    { 
        qIndex = Mathf.Clamp(qIndex, 0, questions.Length - 1);

        if (slot1) slot1.ClearSlot();
        DestroyTokens();

        if (promptText) promptText.text = Q.prompt;
        if (contextLineText) contextLineText.text = string.IsNullOrEmpty(Q.contextLine) ? "" : Q.contextLine;
        if (prefixText) prefixText.text = Q.prefix;
        if (suffixText) suffixText.text = Q.suffix;

        if (slot1)
        {
            slot1.ClearSlot();
            
        }

        

        foreach (var t in Q.options)
        {
            var token = Instantiate(tokenPrefab, tokensRow);
            token.SetLabel(t);
        }
    }


    public void NotifySlotFilled(string slotId, string tokenText, DragToken token)
    {
        if (ended) return;

        // identify the slot that actually received the drop
        DropSlot droppedInto = null;
        if (token && token.transform.parent)
            droppedInto = token.transform.parent.GetComponent<DropSlot>();

        if (tokenText == Q.correct)
        {
            OnCorrect();
        }
        else
        {
            // snap wrong token back immediately and free that slot
            if (droppedInto) droppedInto.ClearSlot();
            OnWrong();
        }
    }



    void OnCorrect()
    {
        if (slot1) slot1.HidePlacedTokenVisual();

        enemyLives = Mathf.Max(0, enemyLives - 1);
        if (enemyHearts) enemyHearts.SetLives(enemyLives);

        if (enemyAnimator)
        {
            enemyAnimator.Hurt(true);           // turn ON
            Invoke(nameof(EnemyStopHurt), 1.15f); // use your clip length here
        }

        string message = string.IsNullOrWhiteSpace(Q.explainCorrect) ? "Nice!" : Q.explainCorrect;
        ShowToastTimed(message, 3f);

        if (enemyLives <= 0) { ended = true; Invoke(nameof(EndQuestCleared), 3f); return; }
        qIndex++;
        Invoke(nameof(LoadQuestion), 3f);
    }

    void EnemyStopHurt()
    {
        if (enemyAnimator) enemyAnimator.Hurt(false); // turn OFF
    }



    void ShowToastTimed(string msg, float seconds)
    {
        ShowToast(msg);
        CancelInvoke(nameof(HideToast));
        Invoke(nameof(HideToast), seconds);
    }

    void DestroyTokens()
    {
        
        var tokens = FindObjectsOfType<DragToken>(true);
        for (int i = 0; i < tokens.Length; i++)
            Destroy(tokens[i].gameObject);
    }



    void OnWrong()
    {
        ShowToast(string.IsNullOrWhiteSpace(Q.hint) ? "Try again!" : Q.hint);

        playerLives = Mathf.Max(0, playerLives - 1);
        playerMovement.isHurt(true);
        Invoke(nameof(stopHurt), 1);
        

        if (playerHearts) playerHearts.SetLives(playerLives);
        if (playerLives <= 0)
        {
            ended = true;                
            Invoke(nameof(EndGameOver), 3f);
            return;
        }

    }

    void stopHurt()
    {
        playerMovement.isHurt(false);
    }




    void EndQuestCleared()
    {
        ended = true;

        // Compute stars from remaining hearts
        int stars = ComputeStarsFromHearts();
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
                
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"SaveAll failed: {ex.Message}");
            }
        }



        if (questClearedPanel) questClearedPanel.SetActive(true);
    }


    void EndGameOver()
    {
        playerMovement.isDead(true);
        ended = true;
        if (gameOverPanel) gameOverPanel.SetActive(true);


    }

    // Buttons
    public void BtnTryAgain()
    {
        if (ended) InitLevel();
    }

    public void BtnExitShrine()
    {
        if (!string.IsNullOrEmpty(worldSceneName))
            SceneManager.LoadScene(worldSceneName);
        else
            Debug.LogWarning("worldSceneName is empty.");
    }

    void ShowToast(string msg)
    {
        if (feedbackText) feedbackText.text = msg;
        if (feedbackToast)
        {
            feedbackToast.alpha = 1f;           // stay visible
            feedbackToast.blocksRaycasts = false;
            feedbackToast.interactable = false;
            // no auto-hide here
        }
    }

    void HideToast()
    {
        if (feedbackToast) feedbackToast.alpha = 0f;
    }


 

    int ComputeStarsFromHearts()
    {
        
        return Mathf.Clamp(playerLives, 1, 3);
    }

    void UpdateStarsUI(int stars)
    {
        for (int i = 0; i < 3; i++)
        {
            bool filled = i < stars;

            if (starFilled != null && i < starFilled.Length && starFilled[i])
            {
                starFilled[i].enabled = filled;            // show/hide the sprite only
                starFilled[i].raycastTarget = false;
            }
            if (starEmpty != null && i < starEmpty.Length && starEmpty[i])
            {
                starEmpty[i].enabled = !filled;            // inverse for empty
                starEmpty[i].raycastTarget = false;
            }
        }
    }



    (int score, int coins) ComputeRewards(int stars)
    {
        int score = stars * 500;     
        int coins = stars switch     
        {
            3 => 100,
            2 => 60,
            _ => 30
        };
        return (score, coins);
    }



    // Save totals to Cloud Save (adds to existing totals), and store per-shrine best stars
    /*async Task SaveRewardsAsync(int stars, int score, int coins)
    {
        try
        {

            await EnsureUgsReadyAsync();
            // Load current totals (if keys don't exist, we'll treat as 0)
            var keys = new HashSet<string> { "total_score", "total_coins", $"best_stars_{shrineId}" };
            var loaded = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

            int totalScore = 0;
            int totalCoins = 0;
            int bestStars = 0;

            if (loaded.TryGetValue("total_score", out var sItem))
                totalScore = sItem.Value.GetAs<int>();
            if (loaded.TryGetValue("total_coins", out var cItem))
                totalCoins = cItem.Value.GetAs<int>();
            if (loaded.TryGetValue($"best_stars_{shrineId}", out var bItem))
                bestStars = bItem.Value.GetAs<int>();

            // Add rewards
            totalScore += score;
            totalCoins += coins;

            // Keep best stars per shrine
            if (stars > bestStars) bestStars = stars;

            var data = new Dictionary<string, object>
        {
            { "total_score", totalScore },
            { "total_coins", totalCoins },
            { $"best_stars_{shrineId}", bestStars }
        };

            await CloudSaveService.Instance.Data.Player.SaveAsync(data);
            Debug.Log($"Saved totals: score={totalScore}, coins={totalCoins}, bestStars({shrineId})={bestStars}");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"SaveRewards failed: {ex.Message}");
        }
    } */

    async Task EnsureUgsReadyAsync()    
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync(); 
    }


}