using UnityEngine;

[RequireComponent(typeof(Animator))]
public class FiretruckAnimation : MonoBehaviour
{
    private const int DIR_UP    = 0;
    private const int DIR_RIGHT = 1;
    private const int DIR_DOWN  = 2;
    private const int DIR_LEFT  = 3;

    private Animator _animator;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    public void SetMovementDirection(Vector2 direction)
    {
        int dir;
        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            dir = direction.x >= 0 ? DIR_RIGHT : DIR_LEFT;
        else
            dir = direction.y >= 0 ? DIR_UP : DIR_DOWN;

        _animator.SetInteger("Direction", dir);
    }
}