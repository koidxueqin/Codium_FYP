using UnityEngine;

public class ScoreSubmitter : MonoBehaviour
{
    [SerializeField] string displayNameOverride = ""; // leave empty to keep UGS name

    public void SubmitFinal(int finalScore)
    {
        _ = CodiumLeaderboards.SubmitAsync(finalScore, CodiumLeaderboards.DefaultId, displayNameOverride);
    }
}
