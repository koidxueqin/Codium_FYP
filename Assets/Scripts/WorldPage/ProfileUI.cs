using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using UnityEngine.SceneManagement;

public class ProfileUI : MonoBehaviour
{
    [Header("Username")]
    [SerializeField] TMP_InputField usernameInput;
    [SerializeField] Button saveUsernameButton;
    [SerializeField] TMP_Text usernameStatusText;

    [Header("Bio")]
    [SerializeField] TMP_InputField bioInput;
    [SerializeField] Button saveBioButton;
    [SerializeField] TMP_Text bioStatusText;

    [Header("Totals")]
    [SerializeField] TMP_Text totalScoreText;

    [Header("Shrines")]
    [Tooltip("Add as many shrines as you want; each row controls one shrine’s UI.")]
    [SerializeField] List<ShrineWidget> shrines = new();

    [Header("Logout")]
    [SerializeField] Button logoutButton;
    [Tooltip("Build index for your LoginPage/startup scene.")]
    [SerializeField] int loginSceneBuildIndex = 0;

    // ---- Keys ----
    const string UsernameKey = "username";
    const string BioKey = "bio";
    const string TotalScoreKey = "total_score";

    [System.Serializable]
    public class ShrineWidget
    {
        [Tooltip("Unique Shrine ID used in saves, e.g., ShrineOne, Shrine2, Shrine3")]
        public string shrineId = "ShrineOne";

        [Tooltip("Panel for this shrine (set active only if cleared). Optional.")]
        public GameObject shrinePanel;

        [Tooltip("Optional image to show the shrine art.")]
        public Image shrineImage;

        [Tooltip("Reusable StarsUI widget for this shrine.")]
        public StarsUI starsUI;

        [Tooltip("Displays the best (highest) score achieved for this shrine.")]
        public TMP_Text bestScoreText;

        // Derived Cloud Save keys
        public string BestStarsKey => $"best_stars_{shrineId}";
        public string BestScoreKey => $"best_score_{shrineId}";
        public string ClearedKey => $"cleared_{shrineId}";
    }

    async void Awake()
    {
        if (!usernameInput || !saveUsernameButton) Debug.LogWarning("[ProfileUI] Wire Username input & save button.");
        if (!bioInput || !saveBioButton) Debug.LogWarning("[ProfileUI] Wire Bio input & save button.");

        saveUsernameButton?.onClick.AddListener(OnClickSaveUsername);
        saveBioButton?.onClick.AddListener(OnClickSaveBio);
        logoutButton?.onClick.AddListener(OnClickLogout);

        await EnsureUgsAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            Debug.LogWarning("[ProfileUI] Not signed in; UI will be blank.");
            return;
        }

        _ = RefreshAllAsync(); // fire-and-forget load
    }

    void OnDestroy()
    {
        saveUsernameButton?.onClick.RemoveListener(OnClickSaveUsername);
        saveBioButton?.onClick.RemoveListener(OnClickSaveBio);
        logoutButton?.onClick.RemoveListener(OnClickLogout);
    }

    async Task RefreshAllAsync()
    {
        try
        {
            // Build a single key set for batch load
            var keys = new HashSet<string> { UsernameKey, BioKey, TotalScoreKey };

            if (shrines != null)
            {
                foreach (var sw in shrines)
                {
                    if (string.IsNullOrWhiteSpace(sw?.shrineId)) continue;
                    keys.Add(sw.BestStarsKey);
                    keys.Add(sw.BestScoreKey);
                    keys.Add(sw.ClearedKey);
                }
            }

            var data = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

            // Username
            if (data.TryGetValue(UsernameKey, out var u))
                if (usernameInput) usernameInput.text = u.Value.GetAs<string>();

            // Bio
            if (data.TryGetValue(BioKey, out var b))
                if (bioInput) bioInput.text = b.Value.GetAs<string>();

            // Total score
            int totalScore = data.TryGetValue(TotalScoreKey, out var ts) ? ts.Value.GetAs<int>() : 0;
            if (totalScoreText) totalScoreText.text = totalScore.ToString();

            // Per shrine
            if (shrines != null)
            {
                foreach (var sw in shrines)
                {
                    if (sw == null || string.IsNullOrWhiteSpace(sw.shrineId)) continue;

                    bool cleared = data.TryGetValue(sw.ClearedKey, out var cl) && cl.Value.GetAs<bool>();
                    if (sw.shrinePanel) sw.shrinePanel.SetActive(cleared);

                    int bestStars = data.TryGetValue(sw.BestStarsKey, out var bs) ? bs.Value.GetAs<int>() : 0;
                    if (sw.starsUI) sw.starsUI.SetStars(bestStars);

                    int bestScore = data.TryGetValue(sw.BestScoreKey, out var bsc) ? bsc.Value.GetAs<int>() : 0;
                    if (sw.bestScoreText) sw.bestScoreText.text = bestScore.ToString();
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ProfileUI] Refresh failed: {e.Message}");
        }
    }

    async void OnClickSaveUsername()
    {
        if (!usernameInput) return;

        string desired = usernameInput.text.Trim();

        // Simple local validation: 2–12 A-Z a-z 0-9 _
        if (string.IsNullOrEmpty(desired) ||
            !System.Text.RegularExpressions.Regex.IsMatch(desired, "^[A-Za-z0-9_]{2,12}$"))
        {
            SetStatus(usernameStatusText, "Use 2–12 letters/numbers/_ only.");
            return;
        }

        SetInteractable(usernameInput, saveUsernameButton, false);
        SetStatus(usernameStatusText, "Saving...");

        try
        {
            await EnsureUgsAsync();
            if (!AuthenticationService.Instance.IsSignedIn) throw new System.Exception("Not signed in");

            var toSave = new Dictionary<string, object> { { UsernameKey, desired } };
            await CloudSaveService.Instance.Data.Player.SaveAsync(toSave);

            // Keep UGS player name in sync (leaderboards)
            await AuthenticationService.Instance.UpdatePlayerNameAsync(desired);

            SetStatus(usernameStatusText, "Saved!");

            // Instant UX: update on-page UserInfo if present
            var ui = FindObjectOfType<UserInfo>();
            if (ui != null)
            {
                // Optional: add a SetUsernameImmediate(string) in UserInfo as shown earlier,
                // or just refresh:
                _ = ui.RefreshAsync();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ProfileUI] Save username failed: {e.Message}");
            SetStatus(usernameStatusText, "Could not save.");
        }
        finally
        {
            SetInteractable(usernameInput, saveUsernameButton, true);
        }
    }

    async void OnClickSaveBio()
    {
        if (!bioInput) return;

        string bio = bioInput.text?.Trim() ?? string.Empty;

        SetInteractable(bioInput, saveBioButton, false);
        SetStatus(bioStatusText, "Saving...");

        try
        {
            await EnsureUgsAsync();
            if (!AuthenticationService.Instance.IsSignedIn) throw new System.Exception("Not signed in");

            var toSave = new Dictionary<string, object> { { BioKey, bio } };
            await CloudSaveService.Instance.Data.Player.SaveAsync(toSave);

            SetStatus(bioStatusText, "Saved!");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ProfileUI] Save bio failed: {e.Message}");
            SetStatus(bioStatusText, "Could not save.");
        }
        finally
        {
            SetInteractable(bioInput, saveBioButton, true);
        }
    }

    void OnClickLogout()
    {
        var login = FindObjectOfType<UGSLogin>();
        if (login != null)
        {
            login.OnSignOutButton();
            return;
        }

        try { AuthenticationService.Instance.SignOut(true); } catch { try { AuthenticationService.Instance.SignOut(); } catch { } }
        SceneManager.LoadScene(loginSceneBuildIndex);
    }

    // ---- helpers ----

    void SetStatus(TMP_Text label, string msg) { if (label) label.text = msg; }

    void SetInteractable(TMP_InputField input, Button btn, bool enable)
    {
        if (input) input.interactable = enable;
        if (btn) btn.interactable = enable;
    }

    static async Task EnsureUgsAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await UGSLogin.WhenSignedIn;
    }
}
