using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class AvatarChanger : MonoBehaviour
{
    [System.Serializable]
    public struct OutfitSprite
    {
        public string outfitId;   // e.g., "default", "ninja", "mage"
        public Sprite sprite;     // sprite to show in the avatar frame
    }

    [Header("Refs")]
    [SerializeField] private OutfitInventoryUGS inventory; // same inventory used by your shop
    [SerializeField] private Image avatarImage;            // UI Image to display the avatar

    [Header("Mappings (by Outfit ID)")]
    [Tooltip("Add entries mapping each outfitId to the portrait sprite you want to show.")]
    [SerializeField] private List<OutfitSprite> mappings = new();

    [Header("Defaults")]
    [Tooltip("Outfit id treated as default/fallback (also used before Cloud Save finishes).")]
    [SerializeField] private string defaultOutfitId = "default";
    [Tooltip("Sprite shown on login (and when an id is unknown).")]
    [SerializeField] private Sprite defaultSprite;

    // Internal
    private readonly Dictionary<string, Sprite> _map = new();
    private bool _started;

    // ---- Unity lifecycle ----
    void Awake()
    {
        BuildMap();

        // On login: show default immediately (before any async calls)
        if (avatarImage)
        {
            avatarImage.sprite = ResolveSprite(defaultOutfitId) ?? defaultSprite;
            avatarImage.enabled = avatarImage.sprite != null;
            avatarImage.preserveAspect = true;
        }
    }

    void OnEnable()
    {
        if (inventory != null)
            inventory.SelectedChanged += OnSelectedChanged;
    }

    void OnDisable()
    {
        if (inventory != null)
            inventory.SelectedChanged -= OnSelectedChanged;
    }

    async void Start()
    {
        _started = true;

        if (!avatarImage)
        {
            Debug.LogWarning("[AvatarChanger] 'avatarImage' not assigned.");
            return;
        }

        if (inventory == null)
        {
            // No inventory: we can only ever show defaults or manual ApplyById calls
            return;
        }

        // Wait for Cloud Save so we read the true equipped id
        await inventory.WhenReady;

        // Refresh once after load
        ApplyById(string.IsNullOrEmpty(inventory.SelectedId) ? defaultOutfitId : inventory.SelectedId);
    }

    // ---- Public helpers ----
    /// <summary>Force an update using a specific outfitId (optional utility).</summary>
    public void ApplyById(string outfitId)
    {
        var sprite = ResolveSprite(string.IsNullOrEmpty(outfitId) ? defaultOutfitId : outfitId);
        if (avatarImage)
        {
            avatarImage.sprite = sprite ?? defaultSprite;
            avatarImage.enabled = avatarImage.sprite != null;
            avatarImage.preserveAspect = true;
        }
    }

    // ---- Event handlers ----
    private void OnSelectedChanged(string newId)
    {
        // Equip -> event fires -> switch instantly
        ApplyById(newId);
    }

    // ---- Internals ----
    private void BuildMap()
    {
        _map.Clear();
        foreach (var m in mappings)
        {
            if (!string.IsNullOrEmpty(m.outfitId) && m.sprite != null)
                _map[m.outfitId] = m.sprite; // last one wins if duplicates
        }
    }

    private Sprite ResolveSprite(string outfitId)
    {
        if (string.IsNullOrEmpty(outfitId)) outfitId = defaultOutfitId;
        if (_map.TryGetValue(outfitId, out var s) && s) return s;

        // If inventory is present, try to fall back to default id from there too
        if (outfitId != defaultOutfitId && _map.TryGetValue(defaultOutfitId, out var def) && def)
            return def;

        return defaultSprite;
    }
}
