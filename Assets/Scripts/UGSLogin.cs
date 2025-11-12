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

    // === Awaitable sign-in gate for other systems (Leaderboards, Cloud Save, etc.) ===
    public static Task WhenSignedIn => _signedInTcs.Task;
    private static TaskCompletionSource<bool> _signedInTcs =
    new(TaskCreationOptions.RunContinuationsAsynchronously);

    private async void Awake()
    {
        await EnsureInitializedAsync();

        // Subscribe to Player Accounts sign-in event
        PlayerAccountService.Instance.SignedIn += OnPlayerAccountsSignedIn;

        // If already signed in with Player Accounts (returning session), continue to Unity Auth
        if (PlayerAccountService.Instance.IsSignedIn)
        {
            _ = SignInWithUnityAuth(); // fire & forget
        }
    }

    private void OnDestroy()
    {
        PlayerAccountService.Instance.SignedIn -= OnPlayerAccountsSignedIn;
    }

    private static async Task EnsureInitializedAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();
    }

    // ================= Player Accounts flow =================

    private async void OnPlayerAccountsSignedIn()
    {
        await SignInWithUnityAuth();
    }


    public async void OnSignInButton()
    {
        try
        {
            if (PlayerAccountService.Instance.IsSignedIn)
                await SignInWithUnityAuth();
            else
                await PlayerAccountService.Instance.StartSignInAsync();
        }
        catch (System.Exception e) { Debug.LogException(e); }
    }

    private async Task SignInWithUnityAuth()
    {
        try
        {
            var token = PlayerAccountService.Instance.AccessToken;

            // If a Unity Auth session exists (e.g., from a previous run), attempt link first.
            // This is safe even without anonymous sign-in; it handles “account already linked” gracefully.
            if (AuthenticationService.Instance.IsSignedIn &&
                AuthenticationService.Instance.SessionTokenExists)
            {
                try
                {
                    await AuthenticationService.Instance.LinkWithUnityAsync(token);
                    CompleteGateAndNavigate();
                    return;
                }
                catch (AuthenticationException ex) when (ex.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
                {
                    // Fall through to normal sign-in
                }
            }

            await AuthenticationService.Instance.SignInWithUnityAsync(token);
            CompleteGateAndNavigate();
        }
        catch (RequestFailedException ex) { Debug.LogException(ex); }
    }

    // ================= Navigation / Sign-out =================

    private void CompleteGateAndNavigate()
    {
        if (!_signedInTcs.Task.IsCompleted)
            _signedInTcs.TrySetResult(true);

        OnSignedInNavigate();
    }

    private void OnSignedInNavigate()
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

    /// <summary>Hook this to your Logout button.</summary>
    public void OnSignOutButton()
    {
        try { AuthenticationService.Instance.SignOut(true); }
        catch { try { AuthenticationService.Instance.SignOut(); } catch { } }

        try { PlayerAccountService.Instance.SignOut(); } catch { }

        navigated = false;

        // Reset the awaitable so future systems will wait for a fresh sign-in
        _signedInTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);


        int count = SceneManager.sceneCountInBuildSettings;
        if (startupSceneIndex < 0 || startupSceneIndex >= count)
        {
            Debug.LogWarning($"startupSceneIndex {startupSceneIndex} is out of range (0..{count - 1}).");
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(startupSceneIndex);
    }
}
