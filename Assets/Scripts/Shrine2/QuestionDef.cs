// QuestionDef.cs
using UnityEngine;

public enum AnswerKind { StringLiteral, Number, Identifier }

[CreateAssetMenu(menuName = "Shrine/Question", fileName = "Question")]
public class QuestionDef : ScriptableObject
{
    public string topic;

    [TextArea] public string instruction;

    [Header("Shrine1")]
    [Tooltip("Shown before the blank, e.g. print(\"")]
    public string prefix;

    [Tooltip("Shown after the blank, e.g. \")")]
    public string suffix;

    [Tooltip("Type the player must drop into the blank.")]
    public AnswerKind expectedKind;

    [Tooltip("Raw value for the correct block. For StringLiteral do NOT include quotes.")]
    public string correctAnswer;

    [Tooltip("Three or more wrong options; controller picks 3.")]
    public string[] distractors;

    [TextArea] public string whyCorrect;
}
