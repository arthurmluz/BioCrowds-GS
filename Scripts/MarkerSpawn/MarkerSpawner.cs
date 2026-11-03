using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Biocrowds.Core;
using UnityEngine.AI;

public abstract class MarkerSpawner : MonoBehaviour
{
    // Start is called before the first frame update
    public SimulationConfiguration.MarkerSpawnMethod spawnMethod;
    //radius for auxin collide
    public float MarkerRadius = 0.1f;

    //density
    public float MarkerDensity = 0.65f;

    protected Transform _auxinsContainer;

    protected float _cellSize;
    public int _maxMarkersPerCell;

    public Auxin auxinPrefab;
    private readonly Collider[] obstacleOverlapResults = new Collider[1];
    private int obstacleLayerMask = -1;

    public abstract IEnumerator CreateMarkers(List<Cell> _cells, List<Auxin> _auxins);

    protected bool HasMarkersNearby(Vector3 test, List<Auxin> others)
    {
        float markerRadiusSqr = MarkerRadius * MarkerRadius;
        for (int i = 0; i < others.Count; i++)
            if ((test - others[i].Position).sqrMagnitude < markerRadiusSqr)
                return true;
        return false;
    }

    protected bool HasObstacleNearby(Vector3 test)
    {
        if (obstacleLayerMask == -1)
            obstacleLayerMask = 1 << LayerMask.NameToLayer("Obstacle");

        return Physics.OverlapSphereNonAlloc(
            test,
            MarkerRadius + 0.1f,
            obstacleOverlapResults,
            obstacleLayerMask) > 0;
    }

    protected bool IsOnNavmesh(Vector3 test, out Vector3 position, float snap = 0.05f)
    {
        if(NavMesh.SamplePosition(test, out NavMeshHit hit, snap, 1 << NavMesh.GetAreaFromName("Walkable")))
        {
            position = hit.position;
            return true;
        }
        position = Vector3.zero;
        return false;
    }
    protected bool IsOnNavmesh(Vector3 test, float snap = 0.05f)
    {
        return IsOnNavmesh(test, out Vector3 v, snap);
    }
}
