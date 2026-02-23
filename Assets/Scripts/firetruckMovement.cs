using UnityEditor.Localization.Plugins.XLIFF.V20;
using UnityEngine;
using UnityEngine.AI;

public class firetruckMovement : MonoBehaviour
{
    private NavMeshAgent agent;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0;

            RaycastHit2D hit = Physics2D.Raycast(mouseWorld, Vector2.zero);
            if (hit.collider != null && hit.collider.CompareTag("Building"))
            {
                NavMeshHit navHit;
                if (NavMesh.SamplePosition(transform.position, out navHit, 1f, NavMesh.AllAreas))
                    agent.Warp(navHit.position);

                agent.SetDestination(hit.collider.transform.position);
            }
        }
    }
}

