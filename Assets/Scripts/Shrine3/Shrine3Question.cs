using UnityEngine;

[CreateAssetMenu(fileName = "Shrine3Question", menuName = "Shrine3/Question", order = 1)]
public class Shrine3Question : ScriptableObject
{
    [TextArea] public string prompt;
    [Range(1, 3)] public int costHearts = 1;
    [Min(1f)] public float timeLimitSeconds = 30f;

    [Header("Answers")]
    [Tooltip("Exact case-insensitive matches allowed. Trimmed before compare.")]
    public string[] acceptedAnswers;

    [Tooltip("Optional regex patterns, case-insensitive.")]
    public string[] acceptedRegex;

    [Header("Feedback")]
    [TextArea] public string[] failHints;
    [TextArea] public string successExplanation;
}
