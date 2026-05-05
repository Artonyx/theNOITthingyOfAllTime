using UnityEngine;

[RequireComponent(typeof(Animator))]
public class CitizenAnimation : MonoBehaviour
{
    private const int DIR_UP    = 0;
    private const int DIR_RIGHT = 1;
    private const int DIR_DOWN  = 2;
    private const int DIR_LEFT  = 3;

    private Animator _animator;
    private Vector2  _lastPosition;
    private bool     _wasMoving = false;

    private void Awake()
    {
        _animator     = GetComponent<Animator>();
        _lastPosition = new Vector2(transform.position.x, transform.position.y);
    }

    private void Update()
    {
        Vector2 currentPos = new Vector2(transform.position.x, transform.position.y);
        Vector2 delta      = currentPos - _lastPosition;
        bool    isMoving   = delta.sqrMagnitude > 0.00001f;

        if (isMoving)
        {
            // Snap to dominant axis for clean cardinal direction
            int dir;
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
                dir = delta.x >= 0 ? DIR_RIGHT : DIR_LEFT;
            else
                dir = delta.y >= 0 ? DIR_UP : DIR_DOWN;

            _animator.SetInteger("Direction", dir);
        }

        // Only update IsMoving when it actually changes to avoid
        // redundant animator calls every frame
        if (isMoving != _wasMoving)
        {
            _animator.SetBool("IsMoving", isMoving);
            _wasMoving = isMoving;
        }

        _lastPosition = currentPos;
    }
}