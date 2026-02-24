using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public class targetScript : MonoBehaviour
{
    [SerializeField] Transform target;
    NavMeshAgent agent;
    NavMeshHit closestHit;
    Vector3 sourcePosition = Vector3.zero;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;
        agent.updateUpAxis = false;
        if (NavMesh.SamplePosition(sourcePosition, out closestHit, 500, 1))
        {
            target.transform.position = closestHit.position;
            target.AddComponent<NavMeshAgent>();
        }
        else
        {
            Debug.Log("Sadness");
        }
    }

    private void Update()
    {
        agent.SetDestination(target.position);
    }
}
