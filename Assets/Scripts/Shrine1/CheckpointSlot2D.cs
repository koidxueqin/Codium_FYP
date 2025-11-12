// CheckpointSlot2D.cs
using UnityEngine;

public class CheckpointSlot2D : MonoBehaviour
{
    [Header("Snap")]
    public Transform snapPoint;
    [Tooltip("Optional visual hint to toggle when empty/nearby")]
    public GameObject hintVisual;

    [Header("Runtime")]
    public CodeBlock2D occupied;

    public bool IsEmpty => occupied == null;

    public bool CanAccept(CodeBlock2D cb) => occupied == null && cb != null;

    public void Snap(CodeBlock2D cb)
    {
        occupied = cb;
        cb.transform.position = snapPoint.position;
        cb.transform.rotation = snapPoint.rotation;
        cb.OnSnapped();
        if (hintVisual) hintVisual.SetActive(false);
    }

    public void ClearIfOccupied()
    {
        if (occupied)
        {
            Destroy(occupied.gameObject);
            occupied = null;
        }
        if (hintVisual) hintVisual.SetActive(true);
    }
}
