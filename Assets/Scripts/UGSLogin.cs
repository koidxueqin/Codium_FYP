using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts;

public class UGSLogin : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField] private int menuSceneIndex = 1; // set in Build Settings order
    private bool navigated;

    async void Awake()
    {
        await UnityServices.InitializeAsync();
        PlayerAccountService.Instance.SignedIn += async () => { await SignInWithUnityAuth(); };
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
                catch (AuthenticationException ex) when (ex.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked) { /* fall through */ }
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

    public void OnSignOutButton()
    {
        AuthenticationService.Instance.SignOut();
        PlayerAccountService.Instance.SignOut();
        navigated = false;
    }
}
