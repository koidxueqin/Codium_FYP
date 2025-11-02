using UnityEngine;

[CreateAssetMenu(fileName = "Shrine3Set", menuName = "Shrine3/Set", order = 2)]
public class Shrine3Set : ScriptableObject
{
    public Shrine3Question[] questions;

    [Header("Stars")]
    public int threeStarMinScore = 12;
    public int twoStarMinScore = 8;
    public int oneStarMinScore = 4;
}
