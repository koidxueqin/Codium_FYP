using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;

public static class RewardsHelper
{
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

    public static async Task SaveRewardsAsync(string shrineId, int stars, int score, int coins)
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            var keys = new HashSet<string> { "total_score", "total_coins", $"best_stars_{shrineId}" };
            var loaded = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

            int totalScore = loaded.TryGetValue("total_score", out var s) ? s.Value.GetAs<int>() : 0;
            int totalCoins = loaded.TryGetValue("total_coins", out var c) ? c.Value.GetAs<int>() : 0;
            int bestStars = loaded.TryGetValue($"best_stars_{shrineId}", out var b) ? b.Value.GetAs<int>() : 0;

            totalScore += score;
            totalCoins += coins;
            if (stars > bestStars) bestStars = stars;

            var data = new Dictionary<string, object> {
                { "total_score", totalScore },
                { "total_coins", totalCoins },
                { $"best_stars_{shrineId}", bestStars }
            };
            await CloudSaveService.Instance.Data.Player.SaveAsync(data);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"SaveRewards (shrine {shrineId}) failed: {ex.Message}");
        }
    }
}
