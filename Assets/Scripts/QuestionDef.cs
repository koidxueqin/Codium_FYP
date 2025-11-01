// QuestionDef.cs
using UnityEngine;
using System.Collections.Generic;

public enum ValidationMode { PerBlankExact, EvaluateToNumber }
public enum AnswerKind { StringLiteral, Number, Identifier }

// BlankRule stays simple
[System.Serializable]
public class BlankRule
{
    public AnswerKind kind;
    public List<string> acceptedValues = new();
    [TextArea] public string hintForWrong;   // (optional) restore hint here
}

// Add instruction here (question-level)
[CreateAssetMenu(menuName = "Shrine/Question", fileName = "Question")]
public class QuestionDef : ScriptableObject
{
    public string topic;
    [TextArea] public string instruction;    // ? NEW: shows in Inspector
    [TextArea] public string[] codeTemplateLines;
    [Range(1, 3)] public int blanksCount = 1;
    public ValidationMode mode = ValidationMode.PerBlankExact;
    public List<BlankRule> blanks = new() { new BlankRule() };
    public int targetValue;
    [TextArea] public string whyCorrect;
    [TextArea] public string whyWrongGeneral;
}
