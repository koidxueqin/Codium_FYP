using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts;

public class UGSLogin : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField] private int menuSceneIndex = 1;     // set in Build Settings order
    [SerializeField] private int startupSceneIndex = 0;  // where to go after logout
    private bool navigated;

    async void Awake()
    {
        await UnityServices.InitializeAsync();
        PlayerAccountService.Instance.SignedIn += OnPlayerAccountsSignedIn;
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

            if (AuthenticationService.Instance.IsSignedIn &&
                AuthenticationService.Instance.SessionTokenExists)
            {
                try
                {
                    await AuthenticationService.Instance.LinkWithUnityAsync(token);
                    OnSignedInNavigate();
                    return;
                }
                catch (AuthenticationException ex) when (ex.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
                {
                    // fall through to normal sign-in
                }
            }

            await AuthenticationService.Instance.SignInWithUnityAsync(token);
            OnSignedInNavigate();
        }
        catch (RequestFailedException ex) { Debug.LogException(ex); }
    }

    void OnSignedInNavigate()
    {
        if (navigated) return;
        navigated = true;

        int count = SceneManager.sceneCountInBuildSettings;
        if (menuSceneIndex < 0 || menuSceneIndex >= count)
        {
            Debug.LogWarning($"menuSceneIndex {menuSceneIndex} is out of range (0..{count - 1}). " +
                             "Add scenes to File > Build Settings and set the correct index.");
            return;
        }

        SceneManager.LoadScene(menuSceneIndex);
    }

    // Hook this to your Logout button
    public void OnSignOutButton()
    {
        // Optional: force clearing the session token so next launch requires login again.
        // If your SDK version supports the bool overload, use true:
        try { AuthenticationService.Instance.SignOut(true); } catch { /* fallback if overload not available */ AuthenticationService.Instance.SignOut(); }
        try { PlayerAccountService.Instance.SignOut(); } catch { }

        navigated = false;

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
