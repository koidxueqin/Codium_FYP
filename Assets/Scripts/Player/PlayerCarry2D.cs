// PlayerCarry2D.cs
using UnityEngine;

public class PlayerCarry2D : MonoBehaviour
{
    [Header("Settings")]
    public Transform carryAnchor;
    public float pickupRadius = 1.1f;

    // Not used anymore for dropping, but keep for pickup filtering
    public LayerMask codeBlockMask;
    public LayerMask slotMask;

    [Header("External")]
    public Shrine1Controller shrine; // must be assigned

    [Header("Snap")]
    [Tooltip("Max world distance from carried block to slot.snapPoint to allow snapping.")]
    public float dropSnapRadius = 0.8f;

    CodeBlock2D carried;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (carried == null) TryPickUpNearest();
            else TryDropToActiveSlot();
        }

        if (carried != null)
            carried.transform.position = carryAnchor.position;
    }

    void TryPickUpNearest()
    {
        var hit = Physics2D.OverlapCircle((Vector2)transform.position, pickupRadius, codeBlockMask);
        if (!hit) return;

        var cb = hit.GetComponent<CodeBlock2D>();
        if (!cb || cb.isSnappedToSlot) return;

        carried = cb;
        carried.OnPickedUp();
    }

    // NEW: distance-based snapping to the controller's single active slot
    void TryDropToActiveSlot()
    {
        if (!carried) return;
        if (shrine == null || shrine.slots == null || shrine.slots.Length == 0) return;

        var slot = shrine.slots[0];
        if (slot == null || slot.snapPoint == null) return;

        // Measure distance from carried block to the snapPoint (no collider needed)
        float dist = Vector2.Distance(carried.transform.position, slot.snapPoint.position);
        if (dist > dropSnapRadius) return;
        if (!slot.CanAccept(carried)) return;

        slot.Snap(carried);
        shrine.NotifySnapped(slot.occupied);
        carried = null;
    }

    public bool IsCarrying => carried != null;

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (carried == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(carryAnchor ? carryAnchor.position : transform.position, dropSnapRadius);
    }
#endif
}
