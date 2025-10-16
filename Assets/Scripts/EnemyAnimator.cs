using UnityEngine;

public class EnemyAnimator : MonoBehaviour
{
    [SerializeField] private Animator anim;
    public void Hurt(bool v) => anim.SetBool("monsterHurt", v);
}
