using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;

public class ShopCard : MonoBehaviour
{
    [Header("Refs")]
    public OutfitInventoryUGS inventory;   // assign in Inspector
    public Outfit outfit;                   // assign this card's Outfit SO

    [Header("UI")]
    public Button buyButton;
    public Button equipButton;
    public TMP_Text priceText;
    public TMP_Text statusText;             // "Locked" / "Equipped"
    public TMP_Text coinsText;              // optional

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
        if (outfit == null)
        {
            Debug.LogError($"[ShopCard] 'outfit' not assigned on {name}");
            ToggleExclusive(showStatus: true, status: "Missing Outfit");
            return;
        }
        if (inventory == null)
        {
            Debug.LogError($"[ShopCard] 'inventory' not assigned on {name}");
            ToggleExclusive(showStatus: true, status: "Missing Inventory");
            return;
        }

        if (priceText) priceText.text = outfit.price.ToString();
        if (buyButton) buyButton.onClick.AddListener(async () => await OnBuy());
        if (equipButton) equipButton.onClick.AddListener(async () => await OnEquip());

        // >>> wait until inventory has loaded ownership/selection
        await inventory.WhenReady;

        await RefreshUI();
    }

    private async void OnSelectedChanged(string newlySelectedId)
    {
        await RefreshUI();
    }

    async Task RefreshUI()
    {
        if (inventory == null || outfit == null) return;

        bool owned = inventory.IsOwned(outfit.outfitId);
        bool selected = inventory.IsSelected(outfit.outfitId);

        if (!owned)
        {
            int coins = await inventory.GetCoinsAsync();
            if (coins >= outfit.price && outfit.price > 0)
                ToggleExclusive(showBuy: true);                     // Buy only
            else
                ToggleExclusive(showStatus: true, status: "Locked"); // Locked only
            return;
        }

        if (selected)
            ToggleExclusive(showStatus: true, status: "Equipped");   // Status only
        else
            ToggleExclusive(showEquip: true);                        // Equip only
    }

    async Task OnBuy()
    {
        var (ok, coinsAfter) = await inventory.PurchaseAsync(outfit);
        if (!ok)
        {
            ToggleExclusive(showStatus: true, status: "Not enough coins");
            return;
        }
        if (coinsText) coinsText.text = coinsAfter.ToString();
        await RefreshUI();
    }

    async Task OnEquip()
    {
        bool ok = await inventory.EquipAsync(outfit);
        if (!ok)
        {
            ToggleExclusive(showStatus: true, status: "Locked");
            return;
        }
        await RefreshUI();
    }

    void ToggleExclusive(bool showBuy = false, bool showEquip = false, bool showStatus = false, string status = "")
    {
        if (buyButton) buyButton.gameObject.SetActive(showBuy);
        if (equipButton) equipButton.gameObject.SetActive(showEquip);

        if (statusText)
        {
            statusText.gameObject.SetActive(showStatus);
            if (showStatus) statusText.text = status;
        }
    }
}
