using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts;

public class UGSLogin : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField] private int WorldSceneIndex = 1;   // set in Build Settings order
    [SerializeField] private int startupSceneIndex = 0; // where to go after logout
    private bool navigated;

    [Header("Dev / Anonymous Sign-In")]
    [Tooltip("When true in Editor, sign in anonymously immediately (skip Player Accounts).")]
    public bool useAnonymousInEditor = true;

    [Tooltip("When true in builds, sign in anonymously (useful for internal test builds).")]
    public bool useAnonymousInBuild = false;

    [Tooltip("Optional profile name to simulate different users on same device (e.g., testA, testB).")]
    public string devProfile = "";

    [Tooltip("If true, logs whether this device/profile is a first-time sign-in (no cached session).")]
    public bool detectFirstTime = true;

    // === Awaitable sign-in gate for other systems (Leaderboards will await this) ===
    public static Task WhenSignedIn => _signedInTcs.Task;
    static TaskCompletionSource<bool> _signedInTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    bool ShouldUseAnonymous()
    {
#if UNITY_EDITOR
        return useAnonymousInEditor;
#else
        return useAnonymousInBuild;
#endif
    }

    async void Awake()
    {
        await EnsureInitializedAsync();

        if (ShouldUseAnonymous())
        {
            // Dev/testing path: anonymous sign-in
            _ = SignInAnonymouslyAsync();
            return;
        }

        // Player Accounts path (your original behavior)
        PlayerAccountService.Instance.SignedIn += OnPlayerAccountsSignedIn;

        if (PlayerAccountService.Instance.IsSignedIn)
        {
            // If already signed-in with Player Accounts (returning session), continue to Unity Auth
            _ = SignInWithUnityAuth();
        }
    }

    void OnDestroy()
    {
        // avoid multiple subscriptions if the object gets recreated
        if (!ShouldUseAnonymous())
            PlayerAccountService.Instance.SignedIn -= OnPlayerAccountsSignedIn;
    }

    async Task EnsureInitializedAsync()
    {
        if (UnityServices.State == ServicesInitializationState.Initialized) return;

        var options = new InitializationOptions();

        // Allow switching profiles to spawn fresh anonymous identities for testing
        if (!string.IsNullOrWhiteSpace(devProfile))
            options.SetProfile(devProfile.Trim());

        await UnityServices.InitializeAsync(options);
    }

    // ================= Player Accounts path (original) ================

    private async void OnPlayerAccountsSignedIn()
    {
        await SignInWithUnityAuth();
    }

    // Hook this to your "Sign In" button (still works the same)
    public async void OnSignInButton()
    {
        try
        {
            if (ShouldUseAnonymous())
            {
                await SignInAnonymouslyAsync();
                return;
            }

            if (PlayerAccountService.Instance.IsSignedIn)
                await SignInWithUnityAuth();
            else
                await PlayerAccountService.Instance.StartSignInAsync();
        }
        catch (System.Exception e) { Debug.LogException(e); }
    }

    async Task SignInWithUnityAuth()
    {
        try
        {
            var token = PlayerAccountService.Instance.AccessToken;

            // If game already has an auth session, try linking (guest -> account upgrade)
            if (AuthenticationService.Instance.IsSignedIn &&
                AuthenticationService.Instance.SessionTokenExists)
            {
                try
                {
                    await AuthenticationService.Instance.LinkWithUnityAsync(token);
                    if (!_signedInTcs.Task.IsCompleted) _signedInTcs.TrySetResult(true);
                    OnSignedInNavigate();
                    return;
                }
                catch (AuthenticationException ex) when (ex.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
                {
                    // fall through to normal sign-in
                }
            }

            await AuthenticationService.Instance.SignInWithUnityAsync(token);

            if (!_signedInTcs.Task.IsCompleted) _signedInTcs.TrySetResult(true);
            OnSignedInNavigate();
        }
        catch (RequestFailedException ex) { Debug.LogException(ex); }
    }

    // ================= Anonymous path (new) ================

    async Task SignInAnonymouslyAsync()
    {
        try
        {
            bool wasFirstTime = !AuthenticationService.Instance.SessionTokenExists;

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            if (detectFirstTime)
                Debug.Log($"[UGSLogin] Anonymous sign-in OK. PlayerId={AuthenticationService.Instance.PlayerId}, FirstTime={wasFirstTime}");

            if (!_signedInTcs.Task.IsCompleted) _signedInTcs.TrySetResult(true);
            OnSignedInNavigate();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[UGSLogin] Anonymous sign-in failed: {ex.Message}");
        }
    }

    // ================= Navigation / Sign-out (unchanged) ================

    void OnSignedInNavigate()
    {
        if (navigated) return;
        navigated = true;

        int count = SceneManager.sceneCountInBuildSettings;
        if (WorldSceneIndex < 0 || WorldSceneIndex >= count)
        {
            Debug.LogWarning($"WorldSceneIndex {WorldSceneIndex} is out of range (0..{count - 1}). " +
                             "Add scenes to File > Build Settings and set the correct index.");
            return;
        }

        SceneManager.LoadScene(WorldSceneIndex);
    }

    // Hook this to your Logout button
    public void OnSignOutButton()
    {
        // Optional: force clearing the session token so next launch requires login again.
        try { AuthenticationService.Instance.SignOut(true); }
        catch { try { AuthenticationService.Instance.SignOut(); } catch { } }

        if (!ShouldUseAnonymous())
        {
            try { PlayerAccountService.Instance.SignOut(); } catch { }
        }

        navigated = false;

        // Reset the awaitable so future systems will wait for a fresh sign-in
        _signedInTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        int count = SceneManager.sceneCountInBuildSettings;
        if (startupSceneIndex < 0 || startupSceneIndex >= count)
        {
            Debug.LogWarning($"startupSceneIndex {startupSceneIndex} is out of range (0..{count - 1}).");
            return;
        }

        // Make sure timeScale is normal in case you paused anywhere
        Time.timeScale = 1f;

        SceneManager.LoadScene(startupSceneIndex);
    }

#if UNITY_EDITOR
    // ---------- Dev helpers (right-click component) ----------
    [ContextMenu("DEV: Clear Session Token")]
    public void DevClearSessionToken()
    {
        AuthenticationService.Instance.SignOut();
        AuthenticationService.Instance.ClearSessionToken();
        Debug.Log("[UGSLogin] Cleared session token. Next run will be a fresh sign-in.");
    }

    [ContextMenu("DEV: Delete Current Account")]
    public async void DevDeleteAccountAsync()
    {
        try
        {
            await AuthenticationService.Instance.DeleteAccountAsync();
            Debug.Log("[UGSLogin] Account deleted on server. Next sign-in will create a new anonymous player.");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[UGSLogin] DeleteAccount failed: {ex.Message}");
        }
    }
#endif
}
