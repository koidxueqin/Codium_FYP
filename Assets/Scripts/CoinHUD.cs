using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;

public class CoinHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text coinSum; // assign your CoinSum TMP here in Inspector

    async void Start()
    {
        if (coinSum == null) { Debug.LogWarning("CoinHUD: coinSum not assigned."); return; }
        coinSum.text = "…";                      // placeholder while loading
        await EnsureUGS();
        await Refresh();
    }

    public async Task Refresh()
    {
        try
        {
            var keys = new HashSet<string> { "total_coins" };
            var data = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

            int total = 0;
            if (data != null && data.TryGetValue("total_coins", out var item))
                total = item.Value.GetAs<int>();

            coinSum.text = total.ToString();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"CoinHUD Refresh failed: {e.Message}");
            coinSum.text = "0"; // fallback
        }
    }

    static async Task EnsureUGS()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }
}
