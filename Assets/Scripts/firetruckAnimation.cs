using UnityEngine;

public class firetruckAnimation : MonoBehaviour
{
    private Animator animator;
    private Vector3 lastPos;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        animator = GetComponent<Animator>();
        lastPos = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 movement = transform.position - lastPos;
        movement.Normalize();
        animator.SetFloat("MoveX", movement.x);
        animator.SetFloat("MoveY", movement.y);
        lastPos = transform.position;
    }
}
