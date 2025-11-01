// CodeRunnerUI2D.cs
using UnityEngine;
using TMPro;

public class CodeRunnerUI2D : MonoBehaviour
{
    [Header("UI")]
    public TMP_InputField input;
    public TMP_Text errorHint;
    public Camera cam;

    [Header("Spawn")]
    public Transform spawnPoint;
    public float spawnClearRadius = 0.35f;
    public LayerMask codeBlockMask;
    public Transform blocksParent;
    public GameObject codeBlockPrefab;

    [Header("Refs")]
    public ShrineController2D shrine;

    // Replace OnRun() with this stricter logic
    public void OnRun()
    {
        Debug.Log("RUN");


        if (!input || !spawnPoint || !blocksParent || !codeBlockPrefab || !shrine)
        {
            Debug.LogError($"CodeRunnerUI2D not wired. "
                + $"input:{input} spawn:{spawnPoint} parent:{blocksParent} prefab:{codeBlockPrefab} shrine:{shrine}");
            if (errorHint) { errorHint.text = "Runner not wired. Check Inspector."; errorHint.gameObject.SetActive(true); }
            return;
        }
        // Block if a loose block exists anywhere
        if (currentLoose != null && !currentLoose.isSnappedToSlot)
        {
            ShowError("No space to spawn! Move the block away.");
            return;
        }

        var (kind, label, raw) = ValueToken.Parse(input.text);
        if (kind == TokenKind.Invalid)
        {
            shrine.PenalizeInvalidInput("Invalid input. Strings need quotes \"text\"; integers are bare 123.");
            ShowError("Invalid token.");
            return;
        }

        // Spawn new loose block
        var go = Instantiate(codeBlockPrefab, spawnPoint.position, Quaternion.identity, blocksParent);
        var cb = go.GetComponent<CodeBlock2D>();
        cb.Init(kind, label);

        cb.canvas.worldCamera = cam;

        currentLoose = cb; // track it
        if (errorHint) errorHint.gameObject.SetActive(false);

        shrine.NotifySpawned(cb);

       
    }

    // Called by Shrine when a block gets snapped to slot
    public void NotifySnapped(CodeBlock2D cb)
    {
        if (currentLoose == cb) currentLoose = null;
    }


    public void ShowError(string msg)
    {
        if (!errorHint) return;
        errorHint.text = msg;
        errorHint.gameObject.SetActive(true);
    }

    // Add a field:
    CodeBlock2D currentLoose;

   


}
