using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Leaderboards;
using Unity.Services.Authentication;

public static class CodiumLeaderboards
{
    // Use this ID in the UGS dashboard
    public const string DefaultId = "codium_global";

    public struct Entry { public int rank; public string name; public int score; }

    public static async Task SubmitAsync(int score, string leaderboardId = DefaultId, string playerDisplayName = null)
    {
        // Wait until your UGSLogin finished the proper Unity sign-in
        await UGSLogin.WhenSignedIn;

        if (!string.IsNullOrWhiteSpace(playerDisplayName))
            await AuthenticationService.Instance.UpdatePlayerNameAsync(playerDisplayName);

        await LeaderboardsService.Instance.AddPlayerScoreAsync(leaderboardId, score);
    }

    public static async Task<List<Entry>> GetTopAsync(string leaderboardId = DefaultId, int limit = 50)
    {
        await UGSLogin.WhenSignedIn;

        var page = await LeaderboardsService.Instance
            .GetScoresAsync(leaderboardId, new GetScoresOptions { Limit = limit });

        return page.Results.Select(r => new Entry
        {
            rank = r.Rank,
            score = (int)r.Score,
            name = r.PlayerName ?? r.PlayerId
        }).ToList();
    }
}
