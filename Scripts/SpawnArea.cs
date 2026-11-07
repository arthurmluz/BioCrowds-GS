using Biocrowds.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnArea : MonoBehaviour
{
    private Collider _collider;
    private MeshRenderer _meshRenderer;

    [Header("Initial Spawner Settings")]
    public int initialNumberOfAgents;
    public bool initialRemoveWhenGoalReached;
    public List<GameObject> initialAgentsGoalList;
    public List<float> initialWaitList;

    [Header("Repeating Spawner Settings")]
    public bool limitRepeatingSpawn = false;
    public int quantityLimitToSpawn = 1;
    public float cycleLenght = 1.0f;    
    public int quantitySpawnedEachCycle;
    public bool capSpawnCountToRemaining;
    public bool repeatingRemoveWhenGoalReached;
    public List<GameObject> repeatingGoalList;
    public List<float> repeatingWaitList;
    private float cycleCounter = 0.0f;
    private bool cycleReady = false;
    public bool useInitialSpawnDelay;
    public float initialSpawnDelay;
    private bool initialSpawnDelayElapsed;

    [Header("Entering Spawner Settings")]
    public bool setGoalOnEnter = false;
    public bool teleportToGoalOnEnter = false;
    public float maximumAgentDensityForTeleport;
    public List<GameObject> enteringGoalList;
    public List<Agent> teleportBuffer;

    public bool CycleReady { get => cycleReady;  }
    public bool Finished => (limitRepeatingSpawn && quantityLimitToSpawn <= 0) || quantitySpawnedEachCycle == 0;
    public Collider Collider => _collider;

    private void Awake()
    {
        if (_collider == null)
            _collider = GetComponent<Collider>();
        if (_meshRenderer == null)
            _meshRenderer = GetComponent<MeshRenderer>();

        cycleCounter = 0.0f;
        cycleReady = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha2))
            Debug.Log(GetRandomPoint());
    }

    public void UpdateSpawnCounter(float dt)
    {
        if (quantitySpawnedEachCycle == 0 || (limitRepeatingSpawn && quantityLimitToSpawn == 0))
            return;

        if (useInitialSpawnDelay && !initialSpawnDelayElapsed)
        {
            cycleCounter += dt;
            if (cycleCounter < Mathf.Max(0f, initialSpawnDelay))
                return;

            cycleCounter = 0f;
            initialSpawnDelayElapsed = true;
            cycleReady = true;
            return;
        }

        initialSpawnDelayElapsed = true;
        if (cycleLenght == 0.0f)
            return;

        cycleCounter += dt;
        if (cycleCounter >= cycleLenght)
        {
            cycleCounter -= cycleLenght;
            cycleReady = true;
        }
    }

    public void ResetCycleReady()
    {
        cycleReady = false;
    }

    public Vector3 GetRandomPoint(float height = 0.0f)
    {
        Vector3 point;
        if (_collider.enabled)
        {
            point = new Vector3(
                Random.Range(_collider.bounds.min.x, _collider.bounds.max.x),
                height,
                Random.Range(_collider.bounds.min.z, _collider.bounds.max.z)
            );
        }
        else
        {   
            point = new Vector3(transform.position.x + 0.5f * Random.Range(-transform.lossyScale.x, transform.lossyScale.x),
                                height,
                                transform.position.z + 0.5f * Random.Range(-transform.lossyScale.z, transform.lossyScale.z) );
        }
        
        return _collider.ClosestPoint(point);
    }
    public bool IsInsideArea(Vector3 point, float height = 0.0f)
    {
        Bounds bounds;
        if (_collider.enabled)
            bounds = _collider.bounds;            
        else
            bounds = new Bounds(transform.position, transform.lossyScale);

        return bounds.Contains(point);
    }
    public void ShowMesh(bool _show)
    {
        _meshRenderer.enabled = _show;
    }

    public void AgentEntered(Agent agent)
    {
        if (setGoalOnEnter)
        {
            agent.goalsList.Clear();
            agent.goalsList.AddRange(enteringGoalList);
            agent.goalIndex = 0;
        }

        if (teleportToGoalOnEnter)
        {
            teleportBuffer.Add(agent);
            if (enteringGoalList.Count > 0)
            {
                SpawnArea destination = enteringGoalList[0].GetComponentInChildren<SpawnArea>();
                if (destination != null && destination.maximumAgentDensityForTeleport > 0f)
                    agent.isWaiting = true;
            }
        }
    }
}
