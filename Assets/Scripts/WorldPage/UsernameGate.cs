using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;

public class UsernameGate : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] GameObject usernamePanel;
    [SerializeField] TMP_InputField input;
    [SerializeField] TMP_Text errorText;
    [SerializeField] Button saveButton;
    [SerializeField] Button cancelButton; // optional

    const string UsernameKey = "username";

    async void Awake()
    {
        // Basic wiring guard
        if (!usernamePanel || !input || !saveButton)
            Debug.LogError("[UsernameGate] Wire panel, input, saveButton in Inspector.");

        await EnsureUgsAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            Debug.LogWarning("[UsernameGate] Not signed in; skipping username check.");
            return;
        }

        // Load saved username
        string savedName = await LoadSavedUsernameAsync();

        if (!string.IsNullOrWhiteSpace(savedName))
        {
            // Keep UGS Player Name in sync so leaderboards show it
            try { await AuthenticationService.Instance.UpdatePlayerNameAsync(savedName); }
            catch (System.Exception e) { Debug.LogWarning($"[UsernameGate] Set PlayerName failed: {e.Message}"); }

            HidePanel();
        }
        else
        {
            ShowPanel();
        }
    }

    void OnEnable()
    {
        if (saveButton) saveButton.onClick.AddListener(OnClickSave);
        if (cancelButton) cancelButton.onClick.AddListener(OnClickCancel);
    }

    void OnDisable()
    {
        if (saveButton) saveButton.onClick.RemoveListener(OnClickSave);
        if (cancelButton) cancelButton.onClick.RemoveListener(OnClickCancel);
    }

    void ShowPanel()
    {
        if (errorText) errorText.text = "";
        if (usernamePanel) usernamePanel.SetActive(true);
        if (input) { input.text = ""; input.Select(); input.ActivateInputField(); }
        Time.timeScale = 0f; // optional: pause while modal is open
    }

    void HidePanel()
    {
        if (usernamePanel) usernamePanel.SetActive(false);
        Time.timeScale = 1f;
    }

    async void OnClickSave()
    {
        string desired = input ? input.text.Trim() : string.Empty;

        // Local validation
        if (string.IsNullOrEmpty(desired))
        {
            SetError("Enter a username.");
            return;
        }
        if (!System.Text.RegularExpressions.Regex.IsMatch(desired, "^[A-Za-z0-9_]{3,16}$"))
        {
            SetError("Use 3–16 letters/numbers/_ only.");
            return;
        }

        ToggleInteractable(false);

        try
        {
            await EnsureUgsAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                SetError("Not signed in. Please try again.");
                ToggleInteractable(true);
                return;
            }

            // Save to this player's Cloud Save (client-side; not globally unique)
            var toSave = new Dictionary<string, object> { { UsernameKey, desired } };
            await CloudSaveService.Instance.Data.Player.SaveAsync(toSave);

            // IMPORTANT: also set UGS Player Name so leaderboards display it
            await AuthenticationService.Instance.UpdatePlayerNameAsync(desired);

            HidePanel();
            Debug.Log($"[UsernameGate] Username set to '{desired}'.");
        }
        catch (System.Exception e)
        {
            SetError("Could not save. Check connection and try again.");
            Debug.LogWarning($"[UsernameGate] Save failed: {e.Message}");
            ToggleInteractable(true);
        }
    }

    void OnClickCancel()
    {
        // Optional: sign out & go back to login
        try { AuthenticationService.Instance.SignOut(true); } catch { try { AuthenticationService.Instance.SignOut(); } catch { } }
        HidePanel();
        // Optionally load your startup scene here.
    }

    // ---- helpers ----

    async Task<string> LoadSavedUsernameAsync()
    {
        try
        {
            var data = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { UsernameKey });
            if (data.TryGetValue(UsernameKey, out var v))
                return v.Value.GetAs<string>();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[UsernameGate] Load username failed: {e.Message}");
        }
        return null;
    }

    void SetError(string msg) { if (errorText) errorText.text = msg; }

    void ToggleInteractable(bool enable)
    {
        if (saveButton) saveButton.interactable = enable;
        if (input) input.interactable = enable;
    }

    static async Task EnsureUgsAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        // Wait for your proper sign-in flow (Unity Player Accounts via UGSLogin)
        if (!AuthenticationService.Instance.IsSignedIn)
            await UGSLogin.WhenSignedIn;
    }
}
