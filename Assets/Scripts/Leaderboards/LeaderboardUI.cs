using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Services.Authentication;

public class LeaderboardUI : MonoBehaviour
{
    [Header("Which leaderboard to show")]
    [SerializeField] string leaderboardId = CodiumLeaderboards.DefaultId;

    [Header("Template & Container")]
    [SerializeField] RectTransform entryContainer;   // the Content object
    [SerializeField] RectTransform entryTemplate;    // the inactive prefab instance

    [Header("Options")]
    [SerializeField] float rowHeight = 32f;
    [SerializeField] int topN = 50;
    [SerializeField] float pollSeconds = 10f;        // set 0 to disable polling

    [Header("Self Highlight")]
    [SerializeField] Color selfNameColor = Color.green;  

    [Header("Tier Settings (mirror UGS thresholds)")]
    [Tooltip("Minimum score for Silver tier (>=). Bronze is below this.")]
    [SerializeField] int silverMin = 500;
    [Tooltip("Minimum score for Gold tier (>=). Silver is [silverMin, goldMin).")]
    [SerializeField] int goldMin = 1000;

    [Header("Tier Badge Sprites (optional)")]
    [SerializeField] Sprite bronzeSprite;
    [SerializeField] Sprite silverSprite;
    [SerializeField] Sprite goldSprite;

    readonly List<Transform> _rows = new();

    async void OnEnable()
    {
        if (entryTemplate != null)
            entryTemplate.gameObject.SetActive(false);

        await RefreshOnce();

        if (pollSeconds > 0f)
            InvokeRepeating(nameof(RefreshTick), pollSeconds, pollSeconds);
    }

    void OnDisable()
    {
        CancelInvoke(nameof(RefreshTick));
    }

    void RefreshTick()
    {
        _ = RefreshOnce();
    }

    public async System.Threading.Tasks.Task RefreshOnce()
    {
        var entries = await CodiumLeaderboards.GetTopAsync(leaderboardId, topN);
        var selfId = AuthenticationService.Instance.PlayerId;

        // Try to get the self entry (even if not in Top N)
        var maybeSelf = await CodiumLeaderboards.GetSelfAsync(leaderboardId);

        // Clear old rows except the template
        for (int i = entryContainer.childCount - 1; i >= 0; i--)
        {
            var child = entryContainer.GetChild(i);
            if (child != entryTemplate) Destroy(child.gameObject);
        }
        _rows.Clear();

        // Build Top N rows
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            int displayRank = e.rank + 1;
            bool isSelf = e.playerId == selfId;
            CreateRow(displayRank, e.name, e.score, e.playerId, isSelf);
        }

        // If self not in Top N, pin a personal row at the bottom
        if (maybeSelf.HasValue && entries.TrueForAll(e => e.playerId != selfId))
        {
            // Optional: add a faint separator row
            CreateSeparatorRow();

            var me = maybeSelf.Value;
            int displayRank = me.rank + 1;
            CreateRow(displayRank, me.name, me.score, me.playerId, true);
        }
    }

    void CreateRow(int rank, string name, int score, string playerId, bool isSelf)
    {
        var t = Instantiate(entryTemplate, entryContainer);
        var rt = t.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(0, -rowHeight * _rows.Count);
        t.gameObject.SetActive(true);

        // Cleanup name for display
        name = StripDiscriminator(name);

        // If this is the signed-in user, show "You" and color it
        var nameText = t.Find("nameText").GetComponent<TextMeshProUGUI>();
        nameText.text = isSelf ? "You" : name;
        if (isSelf) nameText.color = selfNameColor;   // green highlight

        t.Find("posText").GetComponent<TextMeshProUGUI>().text = RankStr(rank);
        t.Find("scoreText").GetComponent<TextMeshProUGUI>().text = score.ToString();

        // Row zebra background (optional)
        var bg = t.Find("background");
        if (bg) bg.gameObject.SetActive(_rows.Count % 2 == 1);


        _rows.Add(t);
    }

    void CreateSeparatorRow()
    {
        var t = Instantiate(entryTemplate, entryContainer);
        var rt = t.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(0, -rowHeight * _rows.Count);
        t.gameObject.SetActive(true);

        // Make it minimal/faint
        var nameText = t.Find("nameText").GetComponent<TextMeshProUGUI>();
        var posText = t.Find("posText").GetComponent<TextMeshProUGUI>();
        var scoreTxt = t.Find("scoreText").GetComponent<TextMeshProUGUI>();

        nameText.text = "—"; posText.text = ""; scoreTxt.text = "";
        nameText.alpha = 0.25f; posText.alpha = 0f; scoreTxt.alpha = 0f;

        var bg = t.Find("background");
        if (bg) bg.gameObject.SetActive(false);

        var badge = t.Find("badge");
        if (badge) badge.gameObject.SetActive(false);

        _rows.Add(t);
    }

    // Removes a trailing "#1234" style suffix if present
    static string StripDiscriminator(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        int hash = s.LastIndexOf('#');
        if (hash <= 0) return s;

        int digits = s.Length - hash - 1;
        if (digits < 3 || digits > 6) return s;
        for (int i = hash + 1; i < s.Length; i++)
            if (!char.IsDigit(s[i])) return s;

        return s.Substring(0, hash);
    }

    string RankStr(int r)
    {
        int mod100 = r % 100;
        if (mod100 == 11 || mod100 == 12 || mod100 == 13) return r + "TH";

        switch (r % 10)
        {
            case 1: return r + "ST";
            case 2: return r + "ND";
            case 3: return r + "RD";
            default: return r + "TH";
        }
    }


   
}
