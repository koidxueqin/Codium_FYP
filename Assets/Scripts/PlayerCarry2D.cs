// PlayerCarry2D.cs
using UnityEngine;

public class PlayerCarry2D : MonoBehaviour
{
    [Header("Settings")]
    public Transform carryAnchor;
    public float pickupRadius = 1.1f;
    public LayerMask codeBlockMask;
    public LayerMask slotMask;

    [Header("External")]
    public ShrineController2D shrine; // for calling OnBlockSnapped

    CodeBlock2D carried;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (carried == null) TryPickUpNearest();
            else TryDropOnSlot();
        }

        if (carried != null)
        {
            carried.transform.position = carryAnchor.position;
        }
    }

    void TryPickUpNearest()
    {
        var hit = Physics2D.OverlapCircle((Vector2)transform.position, pickupRadius, codeBlockMask);
        if (!hit) return;
        var cb = hit.GetComponent<CodeBlock2D>();
        if (!cb || cb.isSnappedToSlot) return; // can't pick from slot
        carried = cb;
        carried.OnPickedUp();
    }

    void TryDropOnSlot()
    {
        var hit = Physics2D.OverlapCircle((Vector2)transform.position, 0.6f, slotMask);
        if (!hit) return;
        var slot = hit.GetComponent<CheckpointSlot2D>();
        if (!slot || !slot.CanAccept(carried)) return;

        slot.Snap(carried);
        // tell shrine which block was snapped so runner clears loose reference
        shrine.NotifySnapped(slot.occupied);

        carried = null;
        shrine.OnAnySlotUpdated();
    }

    public bool IsCarrying => carried != null;
}
