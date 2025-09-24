using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using TMPro;

public class AuthManager : MonoBehaviour
{
    [Header("UI References (assign in Inspector)")]
    public TMP_InputField usernameInput;
    public TMP_InputField emailInput;      // optional for Sign In
    public TMP_InputField passwordInput;
    public TMP_InputField confirmInput;    // optional for Sign In
    public TMP_Text logTxt;                // optional

    [Header("Navigation")]
    [Tooltip("Scene index to load after a successful Sign In. Set to -1 to do nothing.")]
    public int nextSceneOnLogin = -1;

    async void Awake()
    {
        await UnityServices.InitializeAsync();
        Log("");
    }

    // ---------- PUBLIC BUTTON HOOKS ----------
    public async void SignUp() => await SafeRun(SignUpFlow);
    public async void SignIn() => await SafeRun(SignInFlow);
    public async void LinkFromAnonymous() => await SafeRun(LinkAnonymousToUsernamePassword);

    // Change Scene
    public void ChangeScene(int sceneNum)
    {
        Debug.Log("Load Scene " + sceneNum);
        SceneManager.LoadScene(sceneNum);
    }

    // ---------- FLOWS ----------
    async Task SignUpFlow()
    {
        // For SignUp we require: username, email, password, confirm
        var (ok, user, email, pass) = ValidateInputs(requireConfirm: true, requireEmail: true);
        if (!ok) return;

        await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(user, pass);
        Log("Signing Up...");

        // Save email (optional)
        await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> {
            { "contact_email", email }
        });
        Log("");

        // Set display name
        await AuthenticationService.Instance.UpdatePlayerNameAsync(user);
        Log("Sign up Successful!");

        // <<< IMPORTANT: end the flow signed-out so the next scene requires login >>>
        AuthenticationService.Instance.SignOut();
        Log("Account created. Please sign in.");
    }

    async Task SignInFlow()
    {
        //require username and sign in for sign in
        var (ok, user, _, pass) = ValidateInputs(requireConfirm: false, requireEmail: false);
        if (!ok) return;

        await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(user, pass);
        Log("SignIn successful. PlayerId: " + AuthenticationService.Instance.PlayerId);

        //load character state
        try { await PlayerCharacterStore.LoadAsync(); }
        catch (System.Exception ex) { Debug.LogException(ex); Log("Loaded with defaults (offline?)"); }

        if (nextSceneOnLogin >= 0)
            ChangeScene(nextSceneOnLogin);
    }

    async Task LinkAnonymousToUsernamePassword()
    {
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        // Linking behaves like sign-up (needs username+password; email optional)
        var (ok, user, _, pass) = ValidateInputs(requireConfirm: true, requireEmail: false);
        if (!ok) return;

        await AuthenticationService.Instance.AddUsernamePasswordAsync(user, pass);
        Log("");

        await AuthenticationService.Instance.UpdatePlayerNameAsync(user);
        Log("Player name set to: " + user);
        // For linking, we usually KEEP the user signed in.
    }

    // ---------- VALIDATION ----------
    (bool ok, string user, string email, string pass) ValidateInputs(bool requireConfirm, bool requireEmail)
    {
        string user = usernameInput?.text?.Trim() ?? "";
        string email = emailInput?.text?.Trim() ?? "";
        string pass = passwordInput?.text ?? "";
        string confirm = confirmInput?.text ?? "";

        // Required-field check
        bool missingRequired =
            string.IsNullOrEmpty(user) ||
            string.IsNullOrEmpty(pass) ||
            (requireConfirm && string.IsNullOrEmpty(confirm)) ||
            (requireEmail && string.IsNullOrEmpty(email));

        if (missingRequired)
            return Fail("Please input all fields");

        // Username: 3–20 chars; letters, digits, . - @ _
        var userOk = Regex.IsMatch(user, @"^[A-Za-z0-9._@\-]{3,20}$");
        if (!userOk) return Fail("Username must be 3–20 chars: letters, numbers, . - @ _ only.");

        // Password rule (simple length; tighten if you want)
        if (pass.Length < 8) return Fail("Password must be at least 8 characters.");
        if (requireConfirm && pass != confirm) return Fail("Passwords do not match.");

        // Email format (only validate if provided/required)
        if (!string.IsNullOrEmpty(email))
        {
            var emailOk = Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
            if (!emailOk) return Fail("Email looks invalid.");
        }

        return (true, user.ToLowerInvariant(), email, pass);

        (bool, string, string, string) Fail(string msg)
        {
            Log(msg);
            return (false, "", "", "");
        }
    }

    // ---------- UTILITY ----------
    async Task SafeRun(Func<Task> flow)
    {
        try { await flow(); }
        catch (AuthenticationException ex)
        {
            Log("Auth error: " + ex.Message);
            Debug.LogException(ex);
        }
        catch (RequestFailedException ex)
        {
            Log("Request failed: " + ex.Message);
            Debug.LogException(ex);
        }
        catch (Exception ex)
        {
            Log("Unexpected: " + ex.Message);
            Debug.LogException(ex);
        }
    }

    void Log(string s)
    {
        Debug.Log(s);
        if (logTxt) logTxt.text = s;
    }
}
