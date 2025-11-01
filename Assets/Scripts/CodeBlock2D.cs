// CodeBlock2D.cs
using UnityEngine;
using TMPro;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class CodeBlock2D : MonoBehaviour
{
    [Header("Refs")]
    public TMP_Text worldLabel;
    public Rigidbody2D rb;
    public BoxCollider2D col;
    public Canvas canvas;

    [Header("State (read-only)")]
    public TokenKind kind;
    public string valueText;
    public bool isSnappedToSlot;   // true once placed into a slot

    public void Init(TokenKind k, string label)
    {
        kind = k;
        valueText = label;
        if (worldLabel) worldLabel.text = label;
        rb.bodyType = RigidbodyType2D.Dynamic;
        col.isTrigger = false;
        isSnappedToSlot = false;
    }

    public void OnPickedUp()
    {
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        col.isTrigger = true;
    }

    public void OnDroppedFree()
    {
        // Not used in this design (no free-drops), but kept for completeness.
        rb.bodyType = RigidbodyType2D.Dynamic;
        col.isTrigger = false;
    }

    public void OnSnapped()
    {
        rb.bodyType = RigidbodyType2D.Kinematic;
        col.isTrigger = true;
        isSnappedToSlot = true;
    }


}
