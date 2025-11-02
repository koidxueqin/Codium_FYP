using UnityEngine;

public class PlayerCarry3 : MonoBehaviour
{
    [Header("Settings")]
    public float pickupRadius = 1.1f;
    public float dropSnapRadius = 0.8f;
    public LayerMask codeBlockMask;

    Transform carryAnchor;
    Shrine3Controller shrine;
    CheckpointSlot2D submitSlot, trashSlot;

    CodeBlock2D carried;

    public void Init(Shrine3Controller ctrl, Transform anchor, CheckpointSlot2D submit, CheckpointSlot2D trash)
    {
        shrine = ctrl; carryAnchor = anchor; submitSlot = submit; trashSlot = trash;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (carried == null) TryPickUpNearest();
            else TryDrop();
        }

        if (carried != null && carryAnchor != null)
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
        shrine?.NotifyPickedUp(carried);
    }

    void TryDrop()
    {
        if (!carried) return;

        // choose nearest valid slot within radius
        CheckpointSlot2D target = null;
        float dSubmit = submitSlot ? Vector2.Distance(carryAnchor.position, submitSlot.snapPoint.position) : float.MaxValue;
        float dTrash = trashSlot ? Vector2.Distance(carryAnchor.position, trashSlot.snapPoint.position) : float.MaxValue;

        if (dSubmit <= dropSnapRadius && submitSlot.IsEmpty) target = submitSlot;
        if (dTrash <= dropSnapRadius && trashSlot.IsEmpty && (target == null || dTrash < dSubmit)) target = trashSlot;

        if (target == null)
        {
            // free drop
            carried.OnDroppedFree();
            carried = null;
            shrine?.NotifyDroppedFree(null);
            return;
        }

        // Snap and notify
        if (target == submitSlot) shrine?.NotifyDroppedToSubmit(submitSlot, carried);
        else shrine?.NotifyDroppedToTrash(trashSlot, carried);

        carried = null;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (carryAnchor == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(carryAnchor.position, dropSnapRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
#endif
}
