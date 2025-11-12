// File: WorldShrineUnlocker.cs
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;

public class WorldShrineUnlocker : MonoBehaviour
{
    [Tooltip("If left empty, this script will find all ShrinePortal2D in the scene automatically.")]
    public ShrinePortal2D[] portals;

    async void Start()
    {
        // Find portals if not assigned
        if (portals == null || portals.Length == 0)
            portals = FindObjectsOfType<ShrinePortal2D>(includeInactive: true);

        // Optimistically lock until we load
        foreach (var p in portals) p?.ApplyLocked(p.startLocked);

        await EnsureUgsAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            Debug.LogWarning("[WorldShrineUnlocker] Not signed in, leaving default lock state.");
            return;
        }

        // Collect required keys from all portals (skip empties = always unlocked)
        var keys = new HashSet<string>(
            portals.Where(p => p != null && !string.IsNullOrEmpty(p.requiredClearedKey))
                   .Select(p => p.requiredClearedKey)
        );

        var flags = new Dictionary<string, bool>();

        if (keys.Count > 0)
        {
            try
            {
                var data = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

                foreach (var k in keys)
                    flags[k] = data.TryGetValue(k, out var v) && v.Value.GetAs<bool>();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[WorldShrineUnlocker] Cloud load failed: {ex.Message}");
                // Optional: fallback to PlayerPrefs if you mirrored clears
                foreach (var k in keys)
                    flags[k] = PlayerPrefs.GetInt(k, 0) == 1;
            }
        }

        // Apply to each portal
        foreach (var portal in portals)
        {
            if (!portal) continue;

            // No requirement => always unlocked
            if (string.IsNullOrEmpty(portal.requiredClearedKey))
            {
                portal.ApplyLocked(false);
                continue;
            }

            // Unlock if flag true, else keep locked
            bool unlocked = flags.TryGetValue(portal.requiredClearedKey, out var val) && val;
            portal.ApplyLocked(!unlocked);
        }
    }

    // --- same pattern as ProfileUI ---
    static async Task EnsureUgsAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await UGSLogin.WhenSignedIn; // assuming you have this helper as in your project
    }
}
