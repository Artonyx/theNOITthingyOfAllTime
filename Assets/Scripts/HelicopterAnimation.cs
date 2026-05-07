using UnityEngine;

[RequireComponent(typeof(Animator))]
public class HelicopterAnimation : MonoBehaviour
{
    private const int DIR_UP = 0;
    private const int DIR_RIGHT = 1;
    private const int DIR_DOWN = 2;
    private const int DIR_LEFT = 3;

    private Animator _animator;
    private Vector2 _lastPosition;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _lastPosition = new Vector2(transform.position.x, transform.position.y);
    }

    private void Update()
    {
        Vector2 currentPos = new Vector2(transform.position.x, transform.position.y);
        Vector2 delta = currentPos - _lastPosition;
        bool isMoving = delta.sqrMagnitude > 0.00001f;

        if (isMoving)
        {
            int dir;
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
                dir = delta.x >= 0 ? DIR_RIGHT : DIR_LEFT;
            else
                dir = delta.y >= 0 ? DIR_UP : DIR_DOWN;

            _animator.SetInteger("Direction", dir);
        }

        // Keep rotor/body animation running all the time.
        _animator.SetBool("IsMoving", true);

        _lastPosition = currentPos;
    }
}
