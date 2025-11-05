using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.U2D.Animation;
using Unity.Services.Core;
using Unity.Services.CloudSave;

public class OutfitInventoryUGS : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private OutfitApplier applier;
    [SerializeField] private Outfit[] allOutfits;

    [Header("Defaults")]
    [SerializeField] private string defaultOutfitId = "default";

    private const string KOwned = "owned_outfits";
    private const string KSelected = "selected_outfit";
    private const string KCoins = "total_coins";

    private HashSet<string> owned = new();
    private string selectedId;
    private bool ready;

    // >>> NEW: await this in UI so it renders after data has loaded
    private readonly TaskCompletionSource<bool> _readyTcs = new();
    public Task WhenReady => _readyTcs.Task;

    public string SelectedId => selectedId;
    public event Action<string> SelectedChanged;
    public bool IsSelected(string outfitId) => selectedId == outfitId;
    public bool IsOwned(string outfitId) => owned.Contains(outfitId);
    public Outfit GetOutfit(string id) => allOutfits?.FirstOrDefault(o => o && o.outfitId == id);

    private async void Awake()
    {
        await EnsureUgsAsync();
        await LoadAsync();
        ApplySelectedLocally();

        ready = true;
        _readyTcs.TrySetResult(true);          // >>> signal ready
        SelectedChanged?.Invoke(selectedId);    // inform any already-subscribed UI
    }

    public async Task<(bool ok, int coinsAfter)> PurchaseAsync(Outfit outfit)
    {
        if (!ready || outfit == null) return (false, 0);
        if (IsOwned(outfit.outfitId)) return (true, await GetCoinsAsync());

        int coins = await GetCoinsAsync();
        if (coins < outfit.price) return (false, coins);

        coins -= outfit.price;
        owned.Add(outfit.outfitId);

        var payload = new Dictionary<string, object> {
            { KOwned, JsonUtility.ToJson(new StringArray{ items = owned.ToArray() }) },
            { KCoins, coins }
        };

        try
        {
            await CloudSaveService.Instance.Data.Player.SaveAsync(payload);
            return (true, coins);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Outfits] Purchase save failed: {e.Message}");
            owned.Remove(outfit.outfitId);
            return (false, coins + outfit.price);
        }
    }

    public async Task<bool> EquipAsync(Outfit outfit)
    {
        if (!ready || outfit == null) return false;
        if (!IsOwned(outfit.outfitId)) return false;

        selectedId = outfit.outfitId;
        ApplySelectedLocally();

        try
        {
            await CloudSaveService.Instance.Data.Player.SaveAsync(
                new Dictionary<string, object> { { KSelected, selectedId } });
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Outfits] Save selected failed: {e.Message}");
        }

        SelectedChanged?.Invoke(selectedId);
        return true;
    }

    private async Task LoadAsync()
    {
        owned = new HashSet<string> { defaultOutfitId };
        selectedId = defaultOutfitId;

        try
        {
            var keys = new HashSet<string> { KOwned, KSelected };
            var data = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

            if (data.TryGetValue(KOwned, out var ownedItem))
            {
                var arr = JsonUtility.FromJson<StringArray>(ownedItem.Value.GetAsString());
                if (arr?.items != null && arr.items.Length > 0)
                    owned = new HashSet<string>(arr.items);
            }

            if (data.TryGetValue(KSelected, out var selItem))
            {
                var s = selItem.Value.GetAsString();
                if (!string.IsNullOrEmpty(s)) selectedId = s;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Outfits] Load failed, using defaults: {e.Message}");
        }

        owned.Add(defaultOutfitId);
    }

    private void ApplySelectedLocally()
    {
        var outfit = GetOutfit(selectedId) ?? GetOutfit(defaultOutfitId);
        if (outfit != null && applier != null)
            applier.Apply(outfit);
    }

    public async Task<int> GetCoinsAsync()
    {
        try
        {
            var data = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { KCoins });
            return data.TryGetValue(KCoins, out var c) ? c.Value.GetAs<int>() : 0;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Outfits] GetCoins failed: {e.Message}");
            return 0;
        }
    }

    private static async Task EnsureUgsAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        await UGSLogin.WhenSignedIn;
    }

    [Serializable] private class StringArray { public string[] items; }
}
