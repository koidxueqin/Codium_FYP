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
        // basic wiring guard
        if (!usernamePanel || !input || !saveButton)
            Debug.LogError("[UsernameGate] Wire panel, input, saveButton in Inspector.");

        await EnsureUgsAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            Debug.LogWarning("[UsernameGate] Not signed in; skipping username check.");
            return;
        }

        bool has = await HasUsernameAsync();
        if (!has) ShowPanel();
        else HidePanel();
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
        Time.timeScale = 0f; // optional pause while modal is open
    }

    void HidePanel()
    {
        if (usernamePanel) usernamePanel.SetActive(false);
        Time.timeScale = 1f;
    }

    async Task<bool> HasUsernameAsync()
    {
        try
        {
            var data = await CloudSaveService.Instance.Data.Player.LoadAsync(
                new HashSet<string> { UsernameKey }
            );
            return data.TryGetValue(UsernameKey, out var v) &&
                   !string.IsNullOrWhiteSpace(v.Value.GetAs<string>());
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[UsernameGate] Load username failed: {e.Message}");
            return false;
        }
    }

    async void OnClickSave()
    {
        string desired = input ? input.text.Trim() : string.Empty;

        // Local validation (adjust to taste)
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
            // CLIENT-ONLY: just save to this player's data.
            // NOTE: This does NOT enforce global uniqueness.
            var toSave = new Dictionary<string, object> { { UsernameKey, desired } };
            await CloudSaveService.Instance.Data.Player.SaveAsync(toSave);

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
        try { AuthenticationService.Instance.SignOut(true); } catch { AuthenticationService.Instance.SignOut(); }
        HidePanel();
        // You can load your startup scene here if you want to bounce the player.
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
        // Do NOT auto sign-in anonymously here; your UGSLogin handles sign-in.
    }
}
