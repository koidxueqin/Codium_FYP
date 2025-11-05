using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;

public class UserInfo : MonoBehaviour
{
    [Header("Profile")]
    [SerializeField] TMP_Text usernameText;   // ← drag your UI text here

    [Header("Stats")]
    [SerializeField] TMP_Text levelAndXpText; // e.g., "0/50 XP"
    [SerializeField] TMP_Text totalCoinsText;
    [SerializeField] TMP_Text totalScoreText;
    [SerializeField] TMP_Text levelNum;

    [Header("Level Up UI")]
    [SerializeField] GameObject levelUpPanel;   // assign LevelUpPanel
    [SerializeField] TMP_Text levelUpMsg;       // assign LevelUpMsg
    [SerializeField] TMP_Text levelUpLevelText;

    private async void OnEnable()
    {
        await EnsureUgsAsync();
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        try
        {
            var keys = new HashSet<string>{
                "username",               // ← NEW
                "total_score","total_coins","level","total_xp","next_xp",
                "level_up_pending","level_up_bonus"
            };

            var data = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

            // ---- Username ----
            string username = null;
            if (data.TryGetValue("username", out var un))
                username = un.Value.GetAs<string>();
            if (string.IsNullOrWhiteSpace(username))
            {
                // fallback to UGS PlayerName (set by UsernameGate)
                try { username = AuthenticationService.Instance.PlayerName; }
                catch { /* ignore */ }
            }
            if (string.IsNullOrWhiteSpace(username))
                username = "Player";
            if (usernameText) usernameText.text = username;

            // ---- Stats ----
            int level = data.TryGetValue("level", out var lv) ? lv.Value.GetAs<int>() : 1;
            int totalXp = data.TryGetValue("total_xp", out var tx) ? tx.Value.GetAs<int>() : 0;
            int nextXp = data.TryGetValue("next_xp", out var nx) ? nx.Value.GetAs<int>() : 50;
            int coins = data.TryGetValue("total_coins", out var c) ? c.Value.GetAs<int>() : 0;
            int score = data.TryGetValue("total_score", out var s) ? s.Value.GetAs<int>() : 0;

            bool pending = data.TryGetValue("level_up_pending", out var p) && p.Value.GetAs<bool>();
            int bonus = data.TryGetValue("level_up_bonus", out var b) ? b.Value.GetAs<int>() : 0;

            if (levelAndXpText) levelAndXpText.text = $"{totalXp}/{nextXp} XP";
            if (levelNum) levelNum.text = $"{level}";
            if (totalCoinsText) totalCoinsText.text = coins.ToString();
            if (totalScoreText) totalScoreText.text = score.ToString();

            // ---- Level-up panel ----
            if (pending && bonus > 0)
            {
                if (levelUpMsg) levelUpMsg.text = $"Level Up!\n{bonus}";
                if (levelUpLevelText) levelUpLevelText.text = $"{level}";
                if (levelUpPanel) levelUpPanel.SetActive(true);

                coins += bonus;
                var toSave = new Dictionary<string, object>{
                    { "total_coins", coins },
                    { "level_up_pending", false },
                    { "level_up_bonus", 0 }
                };
                await CloudSaveService.Instance.Data.Player.SaveAsync(toSave);

                if (totalCoinsText) totalCoinsText.text = coins.ToString();
            }
            else
            {
                if (levelUpPanel) levelUpPanel.SetActive(false);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[UserInfo] Refresh failed: {e.Message}");
        }
    }

    public void BtnCloseLevelUp()
    {
        if (levelUpPanel) levelUpPanel.SetActive(false);
    }

    static async Task EnsureUgsAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();
    }
}
