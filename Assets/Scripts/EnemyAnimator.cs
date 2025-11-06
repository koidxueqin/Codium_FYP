using UnityEngine;

public class EnemyAnimator : MonoBehaviour
{
    [SerializeField] private Animator anim;

    // Parameters
    static readonly int H_MonsterHurt = Animator.StringToHash("monsterHurt");
    static readonly int H_MonsterDead = Animator.StringToHash("monsterDead");

    [Header("Death State Fallback")]
    [SerializeField] private string deathStateName = "monster2_dead"; // exact state name in Animator
    [SerializeField] private int deathLayer = 0;
    [SerializeField] private float crossFadeDuration = 0.1f;

    void Awake()
    {
        if (!anim) anim = GetComponentInChildren<Animator>();
    }

    bool HasParam(string name)
    {
        if (!anim) return false;
        foreach (var p in anim.parameters)
            if (p.name == name) return true;
        return false;
    }

    public void isHurt(bool hurt)
    {
        if (!anim) return;
        if (HasParam("monsterHurt"))
            anim.SetBool(H_MonsterHurt, hurt);
        // else ignore quietly
    }
    public void isDead(bool dead)
    {
        if (!anim) return;
        if (HasParam("monsterDead"))
            anim.SetBool(H_MonsterDead, dead);
        // else ignore quietly
    }

   
}
