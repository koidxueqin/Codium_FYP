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

    // === Awaitable sign-in gate for other systems (Leaderboards will await this) ===
    public static Task WhenSignedIn => _signedInTcs.Task;
    static TaskCompletionSource<bool> _signedInTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    async void Awake()
    {
        await UnityServices.InitializeAsync();

        // Subscribe first (so we don't miss a fast sign-in)
        PlayerAccountService.Instance.SignedIn += OnPlayerAccountsSignedIn;

        // If Player Accounts is already signed in (e.g., returning to app), continue to UGS Auth
        if (PlayerAccountService.Instance.IsSignedIn)
        {
            // Fire and forget; any failures are logged inside
            _ = SignInWithUnityAuth();
        }
    }

    void OnDestroy()
    {
        // avoid multiple subscriptions if the object gets recreated
        PlayerAccountService.Instance.SignedIn -= OnPlayerAccountsSignedIn;
    }

    private async void OnPlayerAccountsSignedIn()
    {
        await SignInWithUnityAuth();
    }

    // Hook this to your "Sign In" button (or auto-call StartSignInAsync elsewhere)
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

    async Task SignInWithUnityAuth()
    {
        try
        {
            var token = PlayerAccountService.Instance.AccessToken;

            // If the game already has an auth session, try linking Player Accounts first (guest->account upgrade)
            if (AuthenticationService.Instance.IsSignedIn &&
                AuthenticationService.Instance.SessionTokenExists)
            {
                try
                {
                    await AuthenticationService.Instance.LinkWithUnityAsync(token);

                    // Mark the awaitable as ready
                    if (!_signedInTcs.Task.IsCompleted) _signedInTcs.TrySetResult(true);

                    OnSignedInNavigate();
                    return;
                }
                catch (AuthenticationException ex) when (ex.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
                {
                    // fall through to normal sign-in
                }
            }

            // Normal sign-in with Unity Player Accounts
            await AuthenticationService.Instance.SignInWithUnityAsync(token);

            // Mark the awaitable as ready BEFORE navigating so other systems can continue
            if (!_signedInTcs.Task.IsCompleted) _signedInTcs.TrySetResult(true);

            OnSignedInNavigate();
        }
        catch (RequestFailedException ex) { Debug.LogException(ex); }
    }

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

        // Make sure timeScale is normal in case you paused anywhere
        Time.timeScale = 1f;

        SceneManager.LoadScene(startupSceneIndex);
    }
}
