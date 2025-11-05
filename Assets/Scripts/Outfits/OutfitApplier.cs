using UnityEngine;
using UnityEngine.U2D.Animation;

public class OutfitApplier : MonoBehaviour
{
    [SerializeField] private SpriteLibrary spriteLibrary; // Drag Player's SpriteLibrary here
    private const string SaveKey = "selected_outfit";

    public void Apply(Outfit outfit)
    {
        if (!spriteLibrary || !outfit || !outfit.spriteLibrary) return;
        spriteLibrary.spriteLibraryAsset = outfit.spriteLibrary;

        // Local quick-restore (optional but handy)
        PlayerPrefs.SetString(SaveKey, outfit.outfitId);
        PlayerPrefs.Save();
    }

    // Optional helper if you ever want to restore from PlayerPrefs alone
    public void LoadAndApplyFromList(Outfit[] allOutfits)
    {
        var id = PlayerPrefs.GetString(SaveKey, string.Empty);
        if (string.IsNullOrEmpty(id) || allOutfits == null) return;
        foreach (var o in allOutfits)
        {
            if (o && o.outfitId == id)
            {
                Apply(o);
                break;
            }
        }
    }
}
