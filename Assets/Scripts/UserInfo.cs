using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;

public class UserInfo : MonoBehaviour
{
    [SerializeField] TMP_Text levelAndXpText; // e.g., "0/50 XP  Level 1"
    [SerializeField] TMP_Text totalCoinsText;
    [SerializeField] TMP_Text totalScoreText;
    [SerializeField] GameObject levelUpPanel;  // assign LevelUpPanel
    [SerializeField] TMP_Text levelUpMsg;      // assign LevelUpMsg
    [SerializeField] TMP_Text levelUpLevelText;
    [SerializeField] TMP_Text levelNum;

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
            "total_score","total_coins","level","total_xp","next_xp",
            "level_up_pending","level_up_bonus"
        };
            var data = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

            int level = data.TryGetValue("level", out var lv) ? lv.Value.GetAs<int>() : 1;
            int totalXp = data.TryGetValue("total_xp", out var tx) ? tx.Value.GetAs<int>() : 0;
            int nextXp = data.TryGetValue("next_xp", out var nx) ? nx.Value.GetAs<int>() : 50;
            int coins = data.TryGetValue("total_coins", out var c) ? c.Value.GetAs<int>() : 0;
            int score = data.TryGetValue("total_score", out var s) ? s.Value.GetAs<int>() : 0;

            bool pending = data.TryGetValue("level_up_pending", out var p) && p.Value.GetAs<bool>();
            int bonus = data.TryGetValue("level_up_bonus", out var b) ? b.Value.GetAs<int>() : 0;

            // Update standard labels
            if (levelAndXpText) levelAndXpText.text = $"{totalXp}/{nextXp} XP";
            if (levelNum) levelNum.text = $"{level}";
            if (totalCoinsText) totalCoinsText.text = coins.ToString();
            if (totalScoreText) totalScoreText.text = score.ToString();

            // Handle pending level-up prompt here
            if (pending && bonus > 0)
            {
                // Show panel
                if (levelUpMsg) levelUpMsg.text = $"Level Up!\n{bonus}";
                if (levelUpLevelText) levelUpLevelText.text = $"{level}";
                if (levelUpPanel) levelUpPanel.SetActive(true);

                // Immediately grant coins and clear flags
                coins += bonus;
                var toSave = new Dictionary<string, object>{
                { "total_coins", coins },
                { "level_up_pending", false },
                { "level_up_bonus", 0 }
            };
                await CloudSaveService.Instance.Data.Player.SaveAsync(toSave);

                // Refresh coins label
                if (totalCoinsText) totalCoinsText.text = coins.ToString();
            }
            else
            {
                // Ensure panel is hidden if nothing pending
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
