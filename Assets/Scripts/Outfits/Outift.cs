using UnityEngine;
using UnityEngine.U2D.Animation;

[CreateAssetMenu(menuName = "Outfits/Outfit")]
public class Outfit : ScriptableObject
{
    public string outfitId;
    public SpriteLibraryAsset spriteLibrary;
    public int price;
}
