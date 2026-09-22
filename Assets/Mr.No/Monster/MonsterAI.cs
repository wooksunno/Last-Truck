using UnityEngine;
using UnityEngine.AI;

public class MonsterAI : MonoBehaviour
{
    private Transform playerTarget;
    private NavMeshAgent agent;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        // "Player" 태그를 가진 오브젝트를 찾아서 그 트랜스폼을 타겟으로 지정합니다.
        GameObject playerObj = GameObject.FindWithTag("Player");
        
        if (playerObj != null)
        {
            playerTarget = playerObj.transform;
        }
        else
        {
            Debug.LogError("씬에 'Player' 태그를 가진 오브젝트가 없습니다!");
        }
    }

    void Update()
    {
        // 타겟이 있을 때만 추적합니다.
        if (playerTarget != null)
        {
            agent.SetDestination(playerTarget.position);
        }
    }
}
