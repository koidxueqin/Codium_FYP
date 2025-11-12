using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Leaderboards;
using Unity.Services.Authentication;

public static class CodiumLeaderboards
{
    public const string DefaultId = "codium_global";

    public struct Entry
    {
        public int rank;
        public string name;
        public int score;
        public string playerId;  
    }

    public static async Task SubmitAsync(int score, string leaderboardId = DefaultId, string playerDisplayName = null)
    {
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
            name = r.PlayerName ?? r.PlayerId,
            playerId = r.PlayerId
        }).ToList();
    }


    public static async Task<Entry?> GetSelfAsync(string leaderboardId = DefaultId)
    {
        await UGSLogin.WhenSignedIn;

        var self = await LeaderboardsService.Instance.GetPlayerScoreAsync(leaderboardId);
        if (self == null) return null;

        return new Entry
        {
            rank = self.Rank,
            score = (int)self.Score,
            name = self.PlayerName ?? self.PlayerId,
            playerId = self.PlayerId
        };
    }
}
