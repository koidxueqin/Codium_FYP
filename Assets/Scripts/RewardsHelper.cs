using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;

public static class RewardsHelper
{
    public struct XpResult
    {
        public int level, totalXp, nextXp, bonusCoins, levelsGained;
        public bool leveledUp;
    }

    public static (int score, int coins) ComputeRewards(int stars)
    {
        int score = stars * 500;
        int coins = stars switch { 3 => 100, 2 => 60, _ => 30 };
        return (score, coins);
    }

    public static int ComputeStarsFromHearts(int playerLives)
    {
        return Mathf.Clamp(playerLives, 1, 3);
    }

    public static int NextXpAfter(int currentNextXp) =>
        Mathf.CeilToInt(currentNextXp * 1.1f);

    public static async Task<(int totalScore, int totalCoins, XpResult xp)>
    SaveRewardsAndXpAsync(string shrineId, int stars, int score, int coins, int rewardXp)
    {
        await EnsureUgsAsync();

        var keys = new HashSet<string>{
            "total_score","total_coins",$"best_stars_{shrineId}",
            "level","total_xp","next_xp"
        };
        var loaded = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

        int totalScore = loaded.TryGetValue("total_score", out var s) ? s.Value.GetAs<int>() : 0;
        int totalCoins = loaded.TryGetValue("total_coins", out var c) ? c.Value.GetAs<int>() : 0;
        int bestStars = loaded.TryGetValue($"best_stars_{shrineId}", out var b) ? b.Value.GetAs<int>() : 0;
        int level = loaded.TryGetValue("level", out var lv) ? lv.Value.GetAs<int>() : 1;
        int totalXp = loaded.TryGetValue("total_xp", out var x) ? x.Value.GetAs<int>() : 0;
        int nextXp = loaded.TryGetValue("next_xp", out var nx) ? nx.Value.GetAs<int>() : 50;

        totalScore += score;
        totalCoins += coins;
        if (stars > bestStars) bestStars = stars;

        totalXp += Mathf.Max(0, rewardXp);

        int bonusCoins = 0, levelsGained = 0;
        bool leveledUp = false;
        while (totalXp >= nextXp)
        {
            totalXp -= nextXp;
            level += 1;
            levelsGained += 1;
            leveledUp = true;
            int grant = 50 * level;
            bonusCoins += grant;
            totalCoins += grant;
            nextXp = NextXpAfter(nextXp);
        }

        var data = new Dictionary<string, object>{
    { "total_score", totalScore },
    { "total_coins", totalCoins },
    { $"best_stars_{shrineId}", bestStars },
    { "level", level },
    { "total_xp", totalXp },
    { "next_xp", nextXp },
    { "level_up_pending", leveledUp },               // NEW
    { "level_up_bonus", leveledUp ? bonusCoins : 0 } // NEW
};

        await CloudSaveService.Instance.Data.Player.SaveAsync(data);

        return (totalScore, totalCoins, new XpResult
        {
            level = level,
            totalXp = totalXp,
            nextXp = nextXp,
            bonusCoins = bonusCoins,
            levelsGained = levelsGained,
            leveledUp = leveledUp
        });
    }

    static async Task EnsureUgsAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }
}
