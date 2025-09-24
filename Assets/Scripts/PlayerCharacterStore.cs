using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.CloudSave;

public static class PlayerCharacterStore
{
    // Cloud Save keys
    private const string KEY_SELECTED = "selected_character_id";
    private const string KEY_OWNED = "owned_character_ids";

    // Local cache
    private static string _selected;
    private static HashSet<string> _owned = new HashSet<string>();

    // Change this to your real default
    public const string DEFAULT_CHARACTER_ID = "player_default";

    /// Load from Cloud Save (creates sane defaults if missing)
    public static async Task LoadAsync()
    {
        var keys = new HashSet<string> { KEY_SELECTED, KEY_OWNED };
        var result = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

        // Selected
        if (result != null && result.TryGetValue(KEY_SELECTED, out var sel))
        {
            try { _selected = sel.Value.GetAs<string>(); }
            catch { _selected = DEFAULT_CHARACTER_ID; }
        }
        else
        {
            _selected = DEFAULT_CHARACTER_ID;
        }

        // Owned
        if (result != null && result.TryGetValue(KEY_OWNED, out var own))
        {
            try
            {
                var list = own.Value.GetAs<List<string>>() ?? new List<string>();
                _owned = new HashSet<string>(list);
            }
            catch
            {
                _owned = new HashSet<string>();
            }
        }

        // Guarantee: own the selected + default at minimum
        if (string.IsNullOrEmpty(_selected)) _selected = DEFAULT_CHARACTER_ID;
        _owned.Add(DEFAULT_CHARACTER_ID);
        _owned.Add(_selected);

        // Ensure keys exist in the backend
        await SaveOwnedAndSelectedAsync();
    }

    public static string GetSelected() => _selected ?? DEFAULT_CHARACTER_ID;
    public static bool Owns(string characterId) => _owned.Contains(characterId);
    public static IReadOnlyCollection<string> GetOwned() => _owned;

    /// Grant a character (e.g., reward)
    public static async Task GrantAsync(string characterId)
    {
        if (_owned.Add(characterId))
            await SaveOwnedAsync();
    }

    /// Switch selected character (only if owned)
    public static async Task<bool> TrySelectAsync(string characterId)
    {
        if (!_owned.Contains(characterId))
        {
            Debug.LogWarning($"Cannot select {characterId} (not owned).");
            return false;
        }
        _selected = characterId;
        await SaveSelectedAsync();
        return true;
    }

    // ---------- Cloud Save helpers (uses Data.Player like your code) ----------

    private static async Task SaveSelectedAsync()
    {
        var data = new Dictionary<string, object> { { KEY_SELECTED, _selected } };
        await CloudSaveService.Instance.Data.Player.SaveAsync(data);
    }

    private static async Task SaveOwnedAsync()
    {
        var data = new Dictionary<string, object> { { KEY_OWNED, new List<string>(_owned) } };
        await CloudSaveService.Instance.Data.Player.SaveAsync(data);
    }

    private static async Task SaveOwnedAndSelectedAsync()
    {
        var data = new Dictionary<string, object> {
            { KEY_SELECTED, _selected },
            { KEY_OWNED, new List<string>(_owned) }
        };
        await CloudSaveService.Instance.Data.Player.SaveAsync(data);
    }
}
