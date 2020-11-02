using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class NavAgentController : MonoBehaviour
{
    public Camera cam;

    public NavMeshAgent agent;

    public IKInsectController insectController;

    public Transform follow;

    void Start()
    {
        agent.updateRotation = false;
        agent.updatePosition = false;

        StartCoroutine(MoveCoroutine());
    }

    void FixedUpdate()
    {
        agent.nextPosition = transform.position;
    }

    //coroutine to update the insect controller with info from the nav agent every 0.2 second.
    private IEnumerator MoveCoroutine()
    {
        while (true)
        {
            Ray ray = new Ray(follow.position, Vector3.down);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                agent.SetDestination(hit.point);
            }
            if(agent.remainingDistance > agent.stoppingDistance)
            {
                insectController.AgentMove(agent.desiredVelocity);
            }
            else
            {
                insectController.AgentMove(Vector3.zero);
            }
            yield return new WaitForSeconds(0.2f);
        }
    }
}
