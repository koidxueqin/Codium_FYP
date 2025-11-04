using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class LeaderboardUI : MonoBehaviour
{
    [Header("Which leaderboard to show")]
    [SerializeField] string leaderboardId = CodiumLeaderboards.DefaultId; 

    [Header("Template & Container")]
    [SerializeField] RectTransform entryContainer;     // the Content object
    [SerializeField] RectTransform entryTemplate;      // the inactive prefab instance  

    [Header("Options")]
    [SerializeField] float rowHeight = 32f;
    [SerializeField] int topN = 50;
    [SerializeField] float pollSeconds = 10f;          // set 0 to disable polling

    readonly List<Transform> _rows = new();

    async void OnEnable()
    {
        if (entryTemplate != null)
            entryTemplate.gameObject.SetActive(false);

        await RefreshOnce(); // immediate first load

        if (pollSeconds > 0f)
            InvokeRepeating(nameof(RefreshTick), pollSeconds, pollSeconds);
    }

    void OnDisable()
    {
        CancelInvoke(nameof(RefreshTick));
    }

    void RefreshTick()
    {
        _ = RefreshOnce(); // fire-and-forget
    }

    public async System.Threading.Tasks.Task RefreshOnce()
    {
        var entries = await CodiumLeaderboards.GetTopAsync(leaderboardId, topN);

        // Clear old rows except the template
        for (int i = entryContainer.childCount - 1; i >= 0; i--)
        {
            var child = entryContainer.GetChild(i);
            if (child != entryTemplate) Destroy(child.gameObject);
        }
        _rows.Clear();

        // Build rows with 1-based display rank
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            CreateRow(i + 1, e.name, e.score);   // <-- 1,2,3,...
        }
    }


    void CreateRow(int rank, string name, int score)
    {
        var t = Instantiate(entryTemplate, entryContainer);
        var rt = t.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(0, -rowHeight * _rows.Count);
        t.gameObject.SetActive(true);

        // Strip trailing #digits discriminator for display only
        name = StripDiscriminator(name);

        t.Find("posText").GetComponent<TMPro.TextMeshProUGUI>().text = RankStr(rank);
        t.Find("scoreText").GetComponent<TMPro.TextMeshProUGUI>().text = score.ToString();
        t.Find("nameText").GetComponent<TMPro.TextMeshProUGUI>().text = name;

        var bg = t.Find("background");
        if (bg) bg.gameObject.SetActive(rank % 2 == 1);

        _rows.Add(t);
    }

    // Removes a trailing "#1234" style suffix if present
    static string StripDiscriminator(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        int hash = s.LastIndexOf('#');
        if (hash <= 0) return s;

        // ensure everything after # is digits and short (3–6) to avoid nuking legit names
        int digits = s.Length - hash - 1;
        if (digits < 3 || digits > 6) return s;
        for (int i = hash + 1; i < s.Length; i++)
            if (!char.IsDigit(s[i])) return s;

        return s.Substring(0, hash);
    }


    string RankStr(int r) => r == 1 ? "1ST" : r == 2 ? "2ND" : r == 3 ? "3RD" : r + "TH";


}
