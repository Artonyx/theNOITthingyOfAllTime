using UnityEngine;

/// <summary>
/// Drives the firetruck Animator using a single "Direction" int parameter.
///
/// ANIMATOR SETUP — replace your MoveX/MoveY float conditions with this:
///  1. Add a new Int parameter called "Direction"
///  2. Set each Any State transition condition to: Direction Equals
///       firetruckUp    → Direction == 0
///       firetruckRight → Direction == 1
///       firetruckDown  → Direction == 2
///       firetruckLeft  → Direction == 3
///       (idle/default is firetruckDown which is your Entry state — leave that as is)
///  3. On every transition: uncheck "Has Exit Time", set Transition Duration to 0
/// </summary>
[RequireComponent(typeof(Animator))]
public class FiretruckAnimation : MonoBehaviour
{
    // Matches the Direction int values above
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
        // Snap to the dominant axis to get a clean cardinal direction
        int dir;
        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            dir = direction.x >= 0 ? DIR_RIGHT : DIR_LEFT;
        else
            dir = direction.y >= 0 ? DIR_UP : DIR_DOWN;

        _animator.SetInteger("Direction", dir);
    }
}