// ShrineQuestionSet.cs
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Shrine/Question Set", fileName = "ShrineQuestionSet")]
public class ShrineQuestionSet : ScriptableObject
{
    public List<QuestionDef> questions = new();
}
