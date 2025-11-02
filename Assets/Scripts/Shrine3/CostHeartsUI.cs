using UnityEngine;
using UnityEngine.UI;

public class CostHeartsUI : MonoBehaviour
{
    [Header("Left ? Right")]
    public Image heart1;
    public Image heart2;
    public Image heart3;

    Image[] arr;

    void Awake()
    {
        arr = new[] { heart1, heart2, heart3 };
        SetCost(0);
    }

    // cost in [0..3]
    public void SetCost(int cost)
    {
        cost = Mathf.Clamp(cost, 0, 3);
        for (int i = 0; i < arr.Length; i++)
        {
            if (!arr[i]) continue;
            // Show only the first N hearts; hide the rest
            arr[i].gameObject.SetActive(i < cost);
        }
    }
}
