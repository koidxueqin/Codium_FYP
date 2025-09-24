using UnityEngine;
using TMPro;

public class CharacterSelectUI : MonoBehaviour
{
    public TMP_Text status;

    async void OnEnable()
    {
        // Ensure state is loaded (safe to call again)
        try { await PlayerCharacterStore.LoadAsync(); } catch { }
        UpdateStatus();
    }

    public async void SelectById(string id)
    {
        if (await PlayerCharacterStore.TrySelectAsync(id))
            UpdateStatus();
        else if (status) status.text = $"Locked: {id}";
    }

    void UpdateStatus()
    {
        if (status) status.text = "Selected: " + PlayerCharacterStore.GetSelected();
    }
}
