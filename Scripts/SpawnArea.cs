using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnArea : MonoBehaviour
{
    public enum SpawnSide
    {
        North,
        South
    }

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
    public bool repeatingRemoveWhenGoalReached;
    public List<GameObject> repeatingGoalList;
    public List<float> repeatingWaitList;
    public SpawnSide spawnSide = SpawnSide.North;
    private float cycleCounter = 0.0f;
    private bool cycleReady = false;


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
        if (cycleLenght == 0.0f || quantitySpawnedEachCycle == 0 || (limitRepeatingSpawn && quantityLimitToSpawn == 0))
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
            float x = Random.Range(_collider.bounds.min.x, _collider.bounds.max.x);
            float zOffset = Mathf.Max(0.01f, Mathf.Min(0.25f, _collider.bounds.size.z * 0.1f));
            float z = spawnSide == SpawnSide.North ? _collider.bounds.max.z + zOffset : _collider.bounds.min.z - zOffset;
            point = new Vector3(x, height, z);
        }
        else
        {   
            float x = transform.position.x + 0.5f * Random.Range(-transform.lossyScale.x, transform.lossyScale.x);
            float zOffset = Mathf.Max(0.01f, Mathf.Min(0.25f, transform.lossyScale.z * 0.1f));
            float z = transform.position.z + (spawnSide == SpawnSide.North ? transform.lossyScale.z * 0.5f + zOffset : -transform.lossyScale.z * 0.5f - zOffset);
            point = new Vector3(x, height, z);
        }
        
        return _collider.ClosestPoint(point);
    }
    public void ShowMesh(bool _show)
    {
        _meshRenderer.enabled = _show;
    }
}
