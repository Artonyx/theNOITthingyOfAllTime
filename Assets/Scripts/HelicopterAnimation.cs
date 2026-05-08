using UnityEngine;

/// <summary>
/// Handles two separate concerns:
///  1. Rotates the helicopter to face its movement direction (transform rotation)
///  2. Keeps the Animator running constantly for the rotor spin animation
///
/// ANIMATOR SETUP:
///  - One state with your rotor animation clip, set to loop
///  - No parameters needed — it just plays continuously
/// </summary>
[RequireComponent(typeof(Animator))]
public class HelicopterAnimation : MonoBehaviour
{
    [Tooltip("How fast the helicopter rotates to face its movement direction.")]
    public float rotationSpeed = 10f;

    private Animator _animator;
    private Vector2  _lastPosition;

    private void Awake()
    {
        _animator     = GetComponent<Animator>();
        _lastPosition = new Vector2(transform.position.x, transform.position.y);
    }

    private void Update()
    {
        Vector2 currentPos = new Vector2(transform.position.x, transform.position.y);
        Vector2 delta      = currentPos - _lastPosition;

        if (delta.sqrMagnitude > 0.00001f)
        {
            float targetAngle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg + 90f;
            float smoothAngle = Mathf.LerpAngle(
                transform.eulerAngles.z, targetAngle, rotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f, smoothAngle);
        }

        _lastPosition = currentPos;
    }
}