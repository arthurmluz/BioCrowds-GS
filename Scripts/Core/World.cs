/// ---------------------------------------------
/// Contact: Henry Braun
/// Brief: Defines the world environment
/// Thanks to VHLab for original implementation
/// Date: November 2017 
/// ---------------------------------------------

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Collections;
using Unity.AI;
using Unity.AI.Navigation;
using System.Linq;
using Unity.Profiling;

namespace Biocrowds.Core
{
    public class World : MonoBehaviour
    {
        private const int MaxCachedTeleportPositions = 16;

        private sealed class TeleportPositionCache
        {
            public readonly List<Vector3> insidePositions = new List<Vector3>();
            public Vector3 outsidePosition;
            public bool hasOutsidePosition;
        }

        [Header("Simulation Configuration")]
        public SimulationConfiguration.MarkerSpawnMethod markerSpawnMethod;

        [SerializeField] public bool simulateOnUpdate = true;
        [SerializeField] public bool fastLoadingCells = false;
        [SerializeField] public bool fastLoadingAgents = false;

        [SerializeField] public float MAX_AGENTS = 0;
        //agent radius
        [SerializeField] public float AGENT_RADIUS = 1.00f;

        //radius for auxin collide
        [SerializeField] public float AUXIN_RADIUS = 0.1f;

        //density
        [SerializeField] public float AUXIN_DENSITY = 0.50f;

        [SerializeField] public float GOAL_DISTANCE_THRESHOLD = 1.0f;


        [Header("Terrain Setting")]
        public MeshFilter planeMeshFilter;

        [SerializeField] protected Terrain _terrain;
        [SerializeField] protected Transform _terrainObject;
        [SerializeField] protected NavMeshSurface _navmeshObject;

        [SerializeField]
        protected Vector2 _dimension = new Vector2(30.0f, 20.0f);
        public Vector2 Dimension
        {
            get { return _dimension; }
        }

        [SerializeField]
        protected Vector2 _offset = new Vector2(0.0f, 0.0f);
        private Bounds _simulationBlockBounds;
        private bool _hasSimulationBlockBounds;
        public Bounds SimulationBlockBounds => _simulationBlockBounds;
        public bool HasSimulationBlockBounds => _hasSimulationBlockBounds;
        public Vector2 Offset
        {
            get { return _offset; }
        }

        //agent prefab
        [SerializeField]
        protected List<Agent> _agentPrefabList;

        [SerializeField]
        protected Cell _cellPrefab;

        protected Auxin _auxinPrefab;


        [SerializeField]
        public List<Agent> _agents = new List<Agent>();
        public List<Agent> _finishedAgents = new List<Agent>();
        List<Cell> _cells = new List<Cell>();
        List<Auxin> _auxins = new List<Auxin>();

        [SerializeField] protected List<SpawnArea> spawnAreas;
        public List<SpawnArea> Areas => spawnAreas;

        [SerializeField]
        protected Transform _agentsContainer;
        protected int _newAgentID = 0;

        public List<Cell> Cells
        {
            get { return _cells; }
        }

        public List<Auxin> Auxins
        {
            get { return _auxins; }
        }

        [SerializeField]
        protected MarkerSpawner _markerSpawner = null;
        private GameObject _markerlessNavMeshVolumes;
        private readonly List<Auxin> _teleportInsideCandidates = new List<Auxin>();
        private readonly List<Auxin> _teleportOutsideCandidates = new List<Auxin>();
        private readonly Dictionary<SpawnArea, Dictionary<GameObject, TeleportPositionCache>> _teleportPositionCache =
            new Dictionary<SpawnArea, Dictionary<GameObject, TeleportPositionCache>>();
        private static readonly ProfilerMarker SpawnAreaUpdateMarker = new ProfilerMarker("BioCrowds.World.SpawnAreas");
        private static readonly ProfilerMarker VisualAgentUpdateMarker = new ProfilerMarker("BioCrowds.World.VisualAgents");
        private static readonly ProfilerMarker AuxinResetMarker = new ProfilerMarker("BioCrowds.World.ResetAuxins");
        private static readonly ProfilerMarker AuxinAssignmentMarker = new ProfilerMarker("BioCrowds.World.FindNearAuxins");
        private static readonly ProfilerMarker AgentMovementMarker = new ProfilerMarker("BioCrowds.World.AgentMovement");
        private static readonly ProfilerMarker TeleportMarker = new ProfilerMarker("BioCrowds.World.Teleports");
        private static readonly ProfilerMarker TeleportBufferMarker = new ProfilerMarker("BioCrowds.World.Teleport.Buffer");
        private static readonly ProfilerMarker TeleportDestinationMarker = new ProfilerMarker("BioCrowds.World.Teleport.Destination");
        private static readonly ProfilerMarker TeleportCacheBuildMarker = new ProfilerMarker("BioCrowds.World.Teleport.CacheBuild");
        private static readonly ProfilerMarker TeleportCacheSelectionMarker = new ProfilerMarker("BioCrowds.World.Teleport.CacheSelection");
        private static readonly ProfilerMarker TeleportCacheInsideScanMarker = new ProfilerMarker("BioCrowds.World.Teleport.CacheInsideScan");
        private static readonly ProfilerMarker TeleportCacheInsidePathsMarker = new ProfilerMarker("BioCrowds.World.Teleport.CacheInsidePaths");
        private static readonly ProfilerMarker TeleportCacheFallbackMarker = new ProfilerMarker("BioCrowds.World.Teleport.CacheFallback");
        private static readonly ProfilerMarker TeleportCellMarker = new ProfilerMarker("BioCrowds.World.Teleport.CellAssignment");
        private static readonly ProfilerMarker GoalHandlingMarker = new ProfilerMarker("BioCrowds.World.GoalHandling");
        private static readonly ProfilerMarker NavMeshStepMarker = new ProfilerMarker("BioCrowds.World.NavmeshStep");


        //max auxins on the ground
        protected bool _isReady;
        public bool Ready => _isReady;

        protected bool _isFinished;
        public bool Finished => _isFinished;
        public bool SimulationSkipped { get; set; }
        public System.Action OnAfterUpdate = null;
        public System.Action OnSimulationFinished = null;
        public System.Action<SpawnArea, Agent> OnAgentSpawned = null;
        public System.Action<Agent> OnAgentFinished = null;

        protected float _simulationTime;
        public float SimulationTime => _simulationTime;

        public MarkerSpawner MarkerSpawner => _markerSpawner;

        protected void Awake()
        {
            _newAgentID = 0;
            if (spawnAreas.Count == 0)
                spawnAreas = FindObjectsOfType<SpawnArea>().ToList();

            if (planeMeshFilter != null)
            {
                if (planeMeshFilter.name != "Plane")
                    Debug.LogWarning("PlaneMeshFilter Mesh isn't a Plane. " +
                        "The difference in scale may cause unintended behavior.");

                _dimension = new Vector2(Mathf.Ceil(planeMeshFilter.transform.localScale.x * 10f),
                    Mathf.Ceil(planeMeshFilter.transform.localScale.z * 10f));
                _dimension.x += _dimension.x % 2;
                _dimension.y += _dimension.y % 2;

                _offset = new Vector2(Mathf.Round(planeMeshFilter.transform.position.x),
                    Mathf.Round(planeMeshFilter.transform.position.z));
                _offset.x -= (_dimension.x / 2f);
                _offset.y -= (_dimension.y / 2f);

                planeMeshFilter.gameObject.SetActive(false);
            }
        }

        public void SetDimensionAndOffset(Vector2 dimension, Vector2 offset)
        {
            this._dimension = dimension;
            this._offset = offset;
        }

        public void SetSimulationBlockBounds(Bounds bounds)
        {
            _simulationBlockBounds = bounds;
            _hasSimulationBlockBounds = true;
        }

        public void LoadWorld()
        {
            var markerSpawnerMethods = transform.GetComponentsInChildren<MarkerSpawner>();
            _markerSpawner = markerSpawnerMethods.First(p => p.spawnMethod == markerSpawnMethod);

            StartCoroutine(SetupWorld());
        }

        // Use this for initialization
        protected virtual IEnumerator SetupWorld()
        {
            //Application.runInBackground = true;

            //change terrain size according informed
            //if (_terrainObject == null)
            //    _terrainObject = _terrain.transform;
            //if(_terrain != null)
            //    _terrain.terrainData.size = new Vector3(_dimension.x, _terrain.terrainData.size.y, _dimension.y);
            //_terrainObject.position = new Vector3(_offset.x, _terrainObject.position.y, _offset.y);

            //GameObjectUtility.SetStaticEditorFlags(_terrainObject.gameObject, StaticEditorFlags.NavigationStatic);

            //build the navmesh at runtime
            //NavMeshBuilder.BuildNavMesh();
            //UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
            _navmeshObject.BuildNavMesh();

            //create all cells based on dimension
            yield return StartCoroutine(CreateCells());

            yield return StartCoroutine(_markerSpawner.CreateMarkers(_cells, _auxins));
            _teleportPositionCache.Clear();

            if (CreateMarkerlessNavMeshVolumes())
            {
                _navmeshObject.BuildNavMesh();
                _markerlessNavMeshVolumes.SetActive(false);
                Destroy(_markerlessNavMeshVolumes);
                _markerlessNavMeshVolumes = null;
            }

            RelocateNavMeshDisconnectedBuildingExits(0.5f);

            //populate cells with auxins
            //yield return StartCoroutine(DartThrowing());

            //create our agents
            yield return StartCoroutine(CreateAgents());

            //wait a little bit to start moving
            yield return new WaitForSeconds(0.1f);
            _isReady = true;
            _isFinished = false;
            _simulationTime = 0;
        }

        protected virtual IEnumerator CreateCells()
        {
            Transform cellPool = new GameObject("Cells").transform;
            cellPool.gameObject.SetActive(false);
            Vector3 _spawnPos = new Vector3();

            for (int i = 0; i < _dimension.x / 2; i++) //i + agentRadius * 2
            {
                for (int j = 0; j < _dimension.y / 2; j++) // j + agentRadius * 2
                {
                    //instantiante a new cell
                    _spawnPos.x = (1.0f + (i * 2.0f)) + _offset.x;
                    _spawnPos.z = (1.0f + (j * 2.0f)) + _offset.y;

                    Cell newCell = Instantiate(_cellPrefab, _spawnPos, Quaternion.Euler(90.0f, 0.0f, 0.0f), cellPool);

                    //change its name
                    newCell.name = "Cell [" + i + "][" + j + "]";

                    //metadata for optimization
                    newCell.X = i;
                    newCell.Z = j;

                    newCell.ShowMesh(SceneController.ShowCells);

                    _cells.Add(newCell);

                    if(!fastLoadingCells)
                        yield return null;
                }
            }
            yield return null;
        }

        private bool CreateMarkerlessNavMeshVolumes()
        {
            if (_markerlessNavMeshVolumes != null)
            {
                Destroy(_markerlessNavMeshVolumes);
                _markerlessNavMeshVolumes = null;
            }

            if (!_hasSimulationBlockBounds ||
                _simulationBlockBounds.size.x <= 0f ||
                _simulationBlockBounds.size.z <= 0f)
                return false;

            const float tileSize = 0.5f;
            int gridWidth = Mathf.CeilToInt(_simulationBlockBounds.size.x / tileSize);
            int gridHeight = Mathf.CeilToInt(_simulationBlockBounds.size.z / tileSize);
            bool[,] tilesWithMarkers = new bool[gridWidth, gridHeight];
            int supportedTileCount = 0;

            foreach (Auxin auxin in _auxins)
            {
                Vector3 markerPosition = auxin.Position;
                if (markerPosition.x < _simulationBlockBounds.min.x ||
                    markerPosition.x > _simulationBlockBounds.max.x ||
                    markerPosition.z < _simulationBlockBounds.min.z ||
                    markerPosition.z > _simulationBlockBounds.max.z)
                    continue;

                int x = Mathf.Clamp(
                    Mathf.FloorToInt((markerPosition.x - _simulationBlockBounds.min.x) / tileSize),
                    0,
                    gridWidth - 1);
                int z = Mathf.Clamp(
                    Mathf.FloorToInt((markerPosition.z - _simulationBlockBounds.min.z) / tileSize),
                    0,
                    gridHeight - 1);

                if (!tilesWithMarkers[x, z])
                {
                    tilesWithMarkers[x, z] = true;
                    supportedTileCount++;
                }
            }

            bool[,] reachableTiles = FindMarkerTilesReachableFromBlockExits(
                tilesWithMarkers,
                gridWidth,
                gridHeight,
                tileSize);
            RelocateExitBuildingsToReachableMarkers(
                reachableTiles,
                gridWidth,
                gridHeight,
                tileSize);

            if (supportedTileCount == gridWidth * gridHeight)
                return false;

            _markerlessNavMeshVolumes = new GameObject("Markerless NavMesh Exclusions");
            _markerlessNavMeshVolumes.transform.SetParent(_navmeshObject.transform, false);
            int notWalkableArea = UnityEngine.AI.NavMesh.GetAreaFromName("Not Walkable");
            bool[,] processedTiles = new bool[gridWidth, gridHeight];

            for (int z = 0; z < gridHeight; z++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    if (tilesWithMarkers[x, z] || processedTiles[x, z])
                        continue;

                    int rectangleWidth = 1;
                    while (x + rectangleWidth < gridWidth &&
                           !tilesWithMarkers[x + rectangleWidth, z] &&
                           !processedTiles[x + rectangleWidth, z])
                    {
                        rectangleWidth++;
                    }

                    int rectangleHeight = 1;
                    while (z + rectangleHeight < gridHeight)
                    {
                        bool fullRow = true;
                        for (int offsetX = 0; offsetX < rectangleWidth; offsetX++)
                        {
                            int row = z + rectangleHeight;
                            if (tilesWithMarkers[x + offsetX, row] || processedTiles[x + offsetX, row])
                            {
                                fullRow = false;
                                break;
                            }
                        }

                        if (!fullRow)
                            break;

                        rectangleHeight++;
                    }

                    for (int offsetX = 0; offsetX < rectangleWidth; offsetX++)
                    {
                        for (int offsetZ = 0; offsetZ < rectangleHeight; offsetZ++)
                            processedTiles[x + offsetX, z + offsetZ] = true;
                    }

                    GameObject exclusion = new GameObject($"No Markers [{x},{z}] {rectangleWidth}x{rectangleHeight}");
                    exclusion.transform.SetParent(_markerlessNavMeshVolumes.transform);
                    float minX = _simulationBlockBounds.min.x + x * tileSize;
                    float minZ = _simulationBlockBounds.min.z + z * tileSize;
                    float width = Mathf.Min(rectangleWidth * tileSize, _simulationBlockBounds.max.x - minX);
                    float length = Mathf.Min(rectangleHeight * tileSize, _simulationBlockBounds.max.z - minZ);
                    exclusion.transform.position = new Vector3(
                        minX + width * 0.5f,
                        _simulationBlockBounds.center.y,
                        minZ + length * 0.5f);

                    Unity.AI.Navigation.NavMeshModifierVolume volume =
                        exclusion.AddComponent<Unity.AI.Navigation.NavMeshModifierVolume>();
                    volume.area = notWalkableArea;
                    volume.size = new Vector3(width, 100f, length);
                }
            }

            return true;
        }

        private void RelocateNavMeshDisconnectedBuildingExits(float markerSnapDistance)
        {
            SpawnArea[] blockExits = FindObjectsOfType<SpawnArea>()
                .Where(area => area.name == "ExitBlock" || area.name == "BlockExit")
                .ToArray();
            SpawnArea[] buildingExits = FindObjectsOfType<SpawnArea>()
                .Where(area => area.name == "ExitBuildings")
                .ToArray();
            UnityEngine.AI.NavMeshPath path = new UnityEngine.AI.NavMeshPath();
            Dictionary<Vector3, bool> markerPathCache = new Dictionary<Vector3, bool>();

            foreach (SpawnArea buildingExit in buildingExits)
            {
                float exitSnapDistance = Mathf.Max(
                    markerSnapDistance,
                    buildingExit.transform.lossyScale.magnitude);
                if (HasCompleteNavMeshPathToAnyExit(
                    buildingExit.transform.position,
                    exitSnapDistance,
                    blockExits,
                    markerSnapDistance,
                    path))
                    continue;

                Auxin closestMarker = _auxins
                    .OrderBy(marker => (marker.Position - buildingExit.transform.position).sqrMagnitude)
                    .FirstOrDefault(marker =>
                {
                    if (!markerPathCache.TryGetValue(marker.Position, out bool hasCompletePath))
                    {
                        hasCompletePath = HasCompleteNavMeshPathToAnyExit(
                            marker.Position,
                            markerSnapDistance,
                            blockExits,
                            markerSnapDistance,
                            path);
                        markerPathCache[marker.Position] = hasCompletePath;
                    }

                    return hasCompletePath;
                });

                if (closestMarker == null)
                {
                    Debug.LogWarning(
                        $"ExitBuildings '{buildingExit.name}' has no marker with a complete NavMesh path to a block exit.",
                        buildingExit);
                    buildingExit.gameObject.SetActive(false);
                    continue;
                }

                buildingExit.transform.position = closestMarker.Position;
            }
        }

        private bool HasCompleteNavMeshPathToAnyExit(
            Vector3 position,
            float positionSnapDistance,
            SpawnArea[] blockExits,
            float exitSnapDistance,
            UnityEngine.AI.NavMeshPath path)
        {
            if (!UnityEngine.AI.NavMesh.SamplePosition(
                position,
                out UnityEngine.AI.NavMeshHit startHit,
                positionSnapDistance,
                UnityEngine.AI.NavMesh.AllAreas))
                return false;

            foreach (SpawnArea blockExit in blockExits)
            {
                float snapDistance = Mathf.Max(
                    exitSnapDistance,
                    blockExit.transform.lossyScale.magnitude);
                if (!UnityEngine.AI.NavMesh.SamplePosition(
                    blockExit.transform.position,
                    out UnityEngine.AI.NavMeshHit goalHit,
                    snapDistance,
                    UnityEngine.AI.NavMesh.AllAreas))
                    continue;

                if (UnityEngine.AI.NavMesh.CalculatePath(
                    startHit.position,
                    goalHit.position,
                    UnityEngine.AI.NavMesh.AllAreas,
                    path) &&
                    path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete &&
                    path.corners.Length > 0)
                    return true;
            }

            return false;
        }

        private bool[,] FindMarkerTilesReachableFromBlockExits(
            bool[,] tilesWithMarkers,
            int gridWidth,
            int gridHeight,
            float tileSize)
        {
            bool[,] reachableTiles = new bool[gridWidth, gridHeight];
            Queue<Vector2Int> frontier = new Queue<Vector2Int>();
            SpawnArea[] blockExits = FindObjectsOfType<SpawnArea>()
                .Where(area => area.name == "ExitBlock" || area.name == "BlockExit")
                .ToArray();

            foreach (SpawnArea blockExit in blockExits)
            {
                Bounds exitBounds = blockExit.Collider != null
                    ? blockExit.Collider.bounds
                    : new Bounds(blockExit.transform.position, blockExit.transform.lossyScale);
                exitBounds.Expand(new Vector3(tileSize * 2f, 0f, tileSize * 2f));

                foreach (Auxin marker in _auxins)
                {
                    Vector3 position = marker.Position;
                    if (position.x < exitBounds.min.x || position.x > exitBounds.max.x ||
                        position.z < exitBounds.min.z || position.z > exitBounds.max.z ||
                        !TryGetBlockTile(position, gridWidth, gridHeight, tileSize, out int x, out int z) ||
                        !tilesWithMarkers[x, z] || reachableTiles[x, z])
                        continue;

                    reachableTiles[x, z] = true;
                    frontier.Enqueue(new Vector2Int(x, z));
                }
            }

            Vector2Int[] directions =
            {
                Vector2Int.right,
                Vector2Int.left,
                Vector2Int.up,
                Vector2Int.down
            };

            while (frontier.Count > 0)
            {
                Vector2Int tile = frontier.Dequeue();
                foreach (Vector2Int direction in directions)
                {
                    int x = tile.x + direction.x;
                    int z = tile.y + direction.y;
                    if (x < 0 || x >= gridWidth || z < 0 || z >= gridHeight ||
                        reachableTiles[x, z] || !tilesWithMarkers[x, z])
                        continue;

                    reachableTiles[x, z] = true;
                    frontier.Enqueue(new Vector2Int(x, z));
                }
            }

            return reachableTiles;
        }

        private void RelocateExitBuildingsToReachableMarkers(
            bool[,] reachableTiles,
            int gridWidth,
            int gridHeight,
            float tileSize)
        {
            SpawnArea[] buildingExits = FindObjectsOfType<SpawnArea>()
                .Where(area => area.name == "ExitBuildings")
                .ToArray();

            foreach (SpawnArea buildingExit in buildingExits)
            {
                if (TryGetBlockTile(buildingExit.transform.position, gridWidth, gridHeight, tileSize, out int currentX, out int currentZ) &&
                    reachableTiles[currentX, currentZ])
                    continue;

                Collider buildingCollider = buildingExit.transform.parent != null
                    ? buildingExit.transform.parent.GetComponentInParent<Collider>()
                    : null;
                Auxin closestMarker = null;
                float closestDistance = float.PositiveInfinity;

                foreach (Auxin marker in _auxins)
                {
                    if (!TryGetBlockTile(marker.Position, gridWidth, gridHeight, tileSize, out int x, out int z) ||
                        !reachableTiles[x, z])
                        continue;

                    if (buildingCollider != null &&
                        marker.Position.x >= buildingCollider.bounds.min.x &&
                        marker.Position.x <= buildingCollider.bounds.max.x &&
                        marker.Position.z >= buildingCollider.bounds.min.z &&
                        marker.Position.z <= buildingCollider.bounds.max.z)
                        continue;

                    float distance = (marker.Position - buildingExit.transform.position).sqrMagnitude;
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestMarker = marker;
                    }
                }

                if (closestMarker == null)
                {
                    Debug.LogWarning(
                        $"ExitBuildings '{buildingExit.name}' has no marker-supported position connected to a block exit.",
                        buildingExit);
                    continue;
                }

                buildingExit.transform.position = closestMarker.Position;
            }
        }

        private bool TryGetBlockTile(
            Vector3 position,
            int gridWidth,
            int gridHeight,
            float tileSize,
            out int x,
            out int z)
        {
            x = 0;
            z = 0;
            if (position.x < _simulationBlockBounds.min.x || position.x > _simulationBlockBounds.max.x ||
                position.z < _simulationBlockBounds.min.z || position.z > _simulationBlockBounds.max.z)
                return false;

            x = Mathf.Clamp(
                Mathf.FloorToInt((position.x - _simulationBlockBounds.min.x) / tileSize),
                0,
                gridWidth - 1);
            z = Mathf.Clamp(
                Mathf.FloorToInt((position.z - _simulationBlockBounds.min.z) / tileSize),
                0,
                gridHeight - 1);
            return true;
        }

        protected virtual IEnumerator DartThrowing()
        {
            //lets set the qntAuxins for each cell according the density estimation
            float densityToQnt = AUXIN_DENSITY;

            Transform auxinPool = new GameObject("Auxins").transform;

            densityToQnt *= 2f / (2.0f * AUXIN_RADIUS);
            densityToQnt *= 2f / (2.0f * AUXIN_RADIUS);

            
            int _maxAuxins = (int)Mathf.Floor(densityToQnt);

            //for each cell, we generate its auxins
            for (int c = 0; c < _cells.Count; c++)
            {
                //Dart throwing auxins
                //use this flag to break the loop if it is taking too long (maybe there is no more space)
                int flag = 0;
                for (int i = 0; i < _maxAuxins; i++)
                {
                    float x = Random.Range(_cells[c].transform.position.x - 0.99f, _cells[c].transform.position.x + 0.99f);
                    float z = Random.Range(_cells[c].transform.position.z - 0.99f, _cells[c].transform.position.z + 0.99f);

                    //see if there are auxins in this radius. if not, instantiante
                    List<Auxin> allAuxinsInCell = _cells[c].Auxins;
                    bool createAuxin = true;
                    for (int j = 0; j < allAuxinsInCell.Count; j++)
                    {
                        float distanceAASqr = (new Vector3(x, 0f, z) - allAuxinsInCell[j].Position).sqrMagnitude;

                        //if it is too near no need to add another
                        if (distanceAASqr < AUXIN_RADIUS * AUXIN_RADIUS)
                        {
                            createAuxin = false;
                            break;
                        }
                    }

                    //if i have found no auxin, i still need to check if is there obstacles on the way
                    if (createAuxin)
                    {
                        //sphere collider to try to find the obstacles
                        //NavMeshHit hit;
                        //createAuxin = NavMesh.Raycast(new Vector3(x, 2f, z), new Vector3(x, -2f, z), out hit, 1 << NavMesh.GetAreaFromName("Walkable")); //NavMesh.GetAreaFromName("Walkable")); // NavMesh.AllAreas);
                        //createAuxin = NavMesh.SamplePosition(new Vector3(x, 0.0f, z), out hit, 0.1f, 1 << NavMesh.GetAreaFromName("Walkable"));
                        //bool isBlocked = _obstacleCollider.bounds.Contains(new Vector3(x, 0.0f, z));
                        Collider[] hitColliders = Physics.OverlapSphere(new Vector3(x, 0f, z), AUXIN_RADIUS + 0.1f, 1 << LayerMask.NameToLayer("Obstacle"));
                        createAuxin = (hitColliders.Length == 0);
                    }

                    //check if auxin can be created there
                    if (createAuxin)
                    {
                        Auxin newAuxin = Instantiate(_auxinPrefab, new Vector3(x, 0.0f, z), Quaternion.identity, auxinPool);

                        //change its name
                        newAuxin.name = "Auxin [" + c + "][" + i + "]";
                        //this auxin is from this cell
                        newAuxin.Cell = _cells[c];
                        //set position
                        newAuxin.Position = new Vector3(x, 0f, z);

                        newAuxin.ShowMesh(SceneController.ShowAuxins);

                        _auxins.Add(newAuxin);
                        //add this auxin to this cell
                        _cells[c].Auxins.Add(newAuxin);

                        //reset the flag
                        flag = 0;

                        //speed up the demonstration a little bit...
                        if (i % 200 == 0)
                            yield return null;
                    }
                    else
                    {
                        //else, try again
                        flag++;
                        i--;
                    }

                    //if flag is above qntAuxins (*2 to have some more), break;
                    if (flag > _maxAuxins * 2)
                    {
                        //reset the flag
                        flag = 0;
                        break;
                    }
                }
            }
        }

        protected virtual IEnumerator CreateAgents()
        {
            _agentsContainer = new GameObject("Agents").transform;
          
            //instantiate agents
            foreach (SpawnArea _area in spawnAreas)
            {
                for (int i = 0; i < _area.initialNumberOfAgents; i ++)
                {
                    if (MAX_AGENTS == 0 || _agents.Count < MAX_AGENTS)
                        SpawnNewAgentInArea(_area, true);

                    if(!fastLoadingAgents)
                        yield return null;
                }
            }
            yield return null;
        }

        private void Update()
        {
            if (simulateOnUpdate)
                Update(Time.deltaTime);
        }

        // Update is called once per frame
        public void Update(float deltaTime)
        {            
            if (!_isReady || _isFinished)
                return;

            // Update simulation time
            _simulationTime += deltaTime;

            using (SpawnAreaUpdateMarker.Auto())
            {
                foreach (SpawnArea _area in spawnAreas)
                {
                    _area.UpdateSpawnCounter(deltaTime);
                    if (_area.CycleReady)
                    {
                        for (int i = 0; i < _area.quantitySpawnedEachCycle; i++)
                        {
                            if (MAX_AGENTS == 0 || _agents.Count < MAX_AGENTS)
                                SpawnNewAgentInArea(_area, false);
                        }
                    }
                    _area.ResetCycleReady();
                }
            }

            // Update de Navmesh for each agent 
            using (VisualAgentUpdateMarker.Auto())
            {
                for (int i = 0; i < _agents.Count; i++)
                    _agents[i].UpdateVisualAgent();
            }

            //reset auxins
            using (AuxinResetMarker.Auto())
            {
                for (int i = 0; i < _cells.Count; i++)
                    for (int j = 0; j < _cells[i].Auxins.Count; j++)
                        _cells[i].Auxins[j].ResetAuxin();
            }

            //find nearest auxins for each agent
            using (AuxinAssignmentMarker.Auto())
            {
                for (int i = 0; i < _agents.Count; i++)
                    _agents[i].FindNearAuxins();

                for (int i = 0; i < _agents.Count; i++)
                    _agents[i].auxinCount = _agents[i].Auxins.Count;
            }
            /*
             * to find where the agent must move, we need to get the vectors from the agent to each auxin he has, and compare with 
             * the vector from agent to goal, generating a angle which must lie between 0 (best case) and 180 (worst case)
             * The calculation formula was taken from the Bicho´s master thesis and from Paravisi OSG implementation.
            */
            /*for each agent:
            1 - for each auxin near him, find the distance vector between it and the agent
            2 - calculate the movement vector 
            3 - calculate speed vector 
            4 - step
            */

            List<Agent> _agentsToRemove = new List<Agent>();
            //for (int i = 0; i < _maxAgents; i++)
            using (AgentMovementMarker.Auto())
            {
                for (int i = 0; i < _agents.Count; i++)
                {
                    //find the agent
                    List<Auxin> agentAuxins = _agents[i].Auxins;

                    //vector for each auxin
                    for (int j = 0; j < agentAuxins.Count; j++)
                    {
                        //add the distance vector between it and the agent
                        _agents[i]._distAuxin.Add(agentAuxins[j].Position - _agents[i].transform.position);
                    }

                    //calculate the movement vector
                    _agents[i].CalculateDirection();
                    //calculate speed vector
                    _agents[i].CalculateVelocity();
                    //step
                    if (_agents[i].shouldMove)
                        _agents[i].MovementStep(deltaTime);

                    _agents[i].WaitStep(deltaTime);

                    _agents[i].wasTeleported = false;
                }
            }

            // teleporta agentes buffered de cada spawn
            using (TeleportMarker.Auto())
            {
                foreach (SpawnArea _area in spawnAreas)
                    TeleportBufferedAgents(_area);
            }

            using (GoalHandlingMarker.Auto())
            {
                for (int i = 0; i < _agents.Count; i++)
                {
                    if (_agents[i].IsAtCurrentGoal())
                    {
                        SpawnArea goal = _agents[i].GetCurrentGoal()?.GetComponentInChildren<SpawnArea>();
                        if (goal != null)
                        {   // depednendo do goal, coloca agentes pro buffer de teleporte
                            goal.AgentEntered(_agents[i]);
                        }
                    }

                    //if (_agents[i].IsAtCurrentGoal() && !_agents[i].isWaiting)               
                    if (_agents[i].removeWhenGoalReached && _agents[i].IsAtFinalGoal())
                        _agentsToRemove.Add(_agents[i]);
                }

                foreach (Agent a in _agentsToRemove)
                {
                    _agents.Remove(a);
                    _finishedAgents.Add(a);
                    a.gameObject.SetActive(false);
                    OnAgentFinished?.Invoke(a);
                }
                _agentsToRemove.Clear();
            }

            // Update de Navmesh for each agent 
            using (NavMeshStepMarker.Auto())
            {
                for (int i = 0; i < _agents.Count; i++)
                    _agents[i].NavmeshStep(deltaTime);
            }

            // check if finished
            _isFinished = _agents.Count == 0 && spawnAreas.All(x => x.Finished);

            OnAfterUpdate?.Invoke();

            if (_isFinished)
                OnSimulationFinished?.Invoke();            
        }

        protected Cell GetClosestCellToPoint (Vector3 point)
        {
            int cellsAlongX = Mathf.CeilToInt(_dimension.x * 0.5f);
            int cellsAlongZ = Mathf.CeilToInt(_dimension.y * 0.5f);
            int cellX = Mathf.Clamp(
                Mathf.RoundToInt((point.x - _offset.x - 1f) * 0.5f),
                0,
                cellsAlongX - 1);
            int cellZ = Mathf.Clamp(
                Mathf.RoundToInt((point.z - _offset.y - 1f) * 0.5f),
                0,
                cellsAlongZ - 1);
            int cellIndex = cellX * cellsAlongZ + cellZ;
            if (cellIndex >= 0 && cellIndex < _cells.Count)
                return _cells[cellIndex];

            float minDistanceSqr = (point - _cells[0].transform.position).sqrMagnitude;
            int minIndex = 0;
            for (int i = 1; i < _cells.Count; i++)
            {
                float distanceSqr = (point - _cells[i].transform.position).sqrMagnitude;
                if (distanceSqr < minDistanceSqr)
                {
                    minDistanceSqr = distanceSqr;
                    minIndex = i;
                }
            }

            return _cells[minIndex];
        }

        protected void SpawnNewAgent(Vector3 _pos, bool _removeWhenGoalReached, List<GameObject> _goalList)
        {
            Agent newAgent = Instantiate(_agentPrefabList[Random.Range(0, _agentPrefabList.Count)],
                                         _pos, Quaternion.identity, _agentsContainer);
            newAgent.name = "Agent [" + GetNewAgentID() + "]";  //name
            newAgent.CurrentCell = GetClosestCellToPoint(_pos);
            newAgent.agentRadius = AGENT_RADIUS;  //agent radius
            newAgent.Goal = _goalList[0];  //agent goal
            newAgent.goalsList = _goalList;
            newAgent.removeWhenGoalReached = _removeWhenGoalReached;
            newAgent.World = this;
            _agents.Add(newAgent);
            OnAgentSpawned?.Invoke(null, newAgent);
        }

        protected void SpawnNewAgentInArea(SpawnArea _area, bool _isInitialSpawn)
        {
            List<GameObject> goalList = _isInitialSpawn ? _area.initialAgentsGoalList : _area.repeatingGoalList;
            if (!TryGetRandomPoinInArea(_area, goalList[0], out Vector3 _pos))
            {
                SimulationSkipped = true;
                return;
            }

            Agent newAgent = Instantiate(_agentPrefabList[Random.Range(0, _agentPrefabList.Count)], 
                _pos, Quaternion.identity, _agentsContainer);
            newAgent.name = "Agent [" + GetNewAgentID() + "]";  //name
            newAgent.CurrentCell = GetClosestCellToPoint(_pos);
            newAgent.agentRadius = AGENT_RADIUS;  //agent radius
            newAgent.goalDistThreshold = GOAL_DISTANCE_THRESHOLD;
            if (_isInitialSpawn)
            {
                newAgent.Goal = _area.initialAgentsGoalList[0];  //agent goal
                newAgent.goalsList.AddRange(_area.initialAgentsGoalList);
                newAgent.removeWhenGoalReached = _area.initialRemoveWhenGoalReached;
                newAgent.goalsWaitList = _area.initialWaitList;
            }
            else
            {
                newAgent.Goal = _area.repeatingGoalList[0];  //agent goal
                newAgent.goalsList.AddRange(_area.repeatingGoalList);
                newAgent.removeWhenGoalReached = _area.repeatingRemoveWhenGoalReached;
                newAgent.goalsWaitList = _area.repeatingWaitList;
                if(_area.limitRepeatingSpawn)
                    _area.quantityLimitToSpawn--;
            }
            newAgent.World = this;
            newAgent.Initialize();

            _agents.Add(newAgent);
            OnAgentSpawned?.Invoke(_area, newAgent);
        }

        protected void TeleportBufferedAgents(SpawnArea _area)
        {
            using (TeleportBufferMarker.Auto())
            {
                if (_area.teleportBuffer.Count == 0)
                    return;

                GameObject goal = _area.enteringGoalList[0];
                SpawnArea area = goal.GetComponentInChildren<SpawnArea>();
                foreach (Agent agent in _area.teleportBuffer)
                {
                    if (area != null)
                    {
                        Vector3 teleportPosition;
                        using (TeleportDestinationMarker.Auto())
                        {
                            if (!TryGetRandomPoinInArea(area, goal, out teleportPosition))
                            {
                                SimulationSkipped = true;
                                Debug.LogWarning($"Skipping entire simulation because teleport for area '{area.name}' has no reachable position for goal '{goal.name}'.");
                                continue;
                            }
                        }

                        agent.transform.position = teleportPosition;
                    }
                    else
                    {
                        agent.transform.position = goal.transform.position;
                    }

                    using (TeleportCellMarker.Auto())
                        agent.CurrentCell = GetClosestCellToPoint(agent.transform.position);
                    agent.wasTeleported = true;
                }
                _area.teleportBuffer.Clear();
            }
        }

        protected bool TryGetRandomPoinInArea(SpawnArea _area, GameObject goal, out Vector3 position)
        {
            position = default;
            int oldSeed = Random.seed;
            if (!_teleportPositionCache.TryGetValue(_area, out Dictionary<GameObject, TeleportPositionCache> goalCaches))
            {
                goalCaches = new Dictionary<GameObject, TeleportPositionCache>();
                _teleportPositionCache.Add(_area, goalCaches);
            }

            if (!goalCaches.TryGetValue(goal, out TeleportPositionCache cache))
            {
                using (TeleportCacheBuildMarker.Auto())
                {
                    cache = BuildTeleportPositionCache(_area, goal);
                    goalCaches.Add(goal, cache);
                }
            }

            bool found = false;
            using (TeleportCacheSelectionMarker.Auto())
            {
                if (cache.insidePositions.Count > 0)
                {
                    position = cache.insidePositions[Random.Range(0, cache.insidePositions.Count)];
                    found = true;
                }
                else if (cache.hasOutsidePosition)
                {
                    position = cache.outsidePosition;
                    found = true;
                }
            }

            // return seed to oldstate to not disrturb random sequentiation
            Random.InitState(oldSeed);

            if (!found)
            {
                SimulationSkipped = true;
                Debug.LogWarning($"Could not find any reachable spawn position for area '{_area.name}' and goal '{goal.name}'. Skipping the entire simulation.");
                return false;
            }

            return true;
        }

        private TeleportPositionCache BuildTeleportPositionCache(SpawnArea area, GameObject goal)
        {
            TeleportPositionCache cache = new TeleportPositionCache();
            _teleportInsideCandidates.Clear();
            _teleportOutsideCandidates.Clear();

            using (TeleportCacheInsideScanMarker.Auto())
            {
                foreach (Auxin auxin in Auxins)
                {
                    if (area.IsInsideArea(auxin.Position))
                        _teleportInsideCandidates.Add(auxin);
                }
            }

            Vector3 goalPosition = goal.transform.position;
            Collider goalCollider = goal.GetComponent<Collider>();
            if (goalCollider != null)
                goalPosition = goalCollider.ClosestPoint(goalPosition);

            if (!UnityEngine.AI.NavMesh.SamplePosition(
                goalPosition,
                out UnityEngine.AI.NavMeshHit goalHit,
                10f,
                UnityEngine.AI.NavMesh.AllAreas))
                return cache;

            UnityEngine.AI.NavMeshPath path = new UnityEngine.AI.NavMeshPath();
            using (TeleportCacheInsidePathsMarker.Auto())
            {
                for (int i = _teleportInsideCandidates.Count - 1; i > 0; i--)
                {
                    int randomIndex = Random.Range(0, i + 1);
                    Auxin auxin = _teleportInsideCandidates[i];
                    _teleportInsideCandidates[i] = _teleportInsideCandidates[randomIndex];
                    _teleportInsideCandidates[randomIndex] = auxin;
                }

                foreach (Auxin auxin in _teleportInsideCandidates)
                {
                    if (HasCompleteNavMeshPath(auxin.Position, goalHit.position, path))
                    {
                        cache.insidePositions.Add(auxin.Position);
                        if (cache.insidePositions.Count >= MaxCachedTeleportPositions)
                            break;
                    }
                }
            }

            if (cache.insidePositions.Count > 0)
                return cache;

            using (TeleportCacheFallbackMarker.Auto())
            {
                foreach (Auxin auxin in Auxins)
                {
                    if (!area.IsInsideArea(auxin.Position))
                        _teleportOutsideCandidates.Add(auxin);
                }

                Vector3 areaPosition = area.transform.position;
                _teleportOutsideCandidates.Sort((first, second) =>
                {
                    float firstDistance = (first.Position - areaPosition).sqrMagnitude;
                    float secondDistance = (second.Position - areaPosition).sqrMagnitude;
                    return firstDistance.CompareTo(secondDistance);
                });

                foreach (Auxin auxin in _teleportOutsideCandidates)
                {
                    if (!HasCompleteNavMeshPath(auxin.Position, goalHit.position, path))
                        continue;

                    cache.outsidePosition = auxin.Position;
                    cache.hasOutsidePosition = true;
                    break;
                }
            }

            return cache;
        }

        private bool HasCompleteNavMeshPath(
            Vector3 startPosition,
            Vector3 goalPosition,
            UnityEngine.AI.NavMeshPath path)
        {
            return UnityEngine.AI.NavMesh.SamplePosition(
                    startPosition,
                    out UnityEngine.AI.NavMeshHit startHit,
                    AGENT_RADIUS,
                    UnityEngine.AI.NavMesh.AllAreas) &&
                UnityEngine.AI.NavMesh.CalculatePath(
                    startHit.position,
                    goalPosition,
                    UnityEngine.AI.NavMesh.AllAreas,
                    path) &&
                path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete;
        }

        protected int GetNewAgentID()
        {
            _newAgentID++;
            return _newAgentID - 1;
        }

        public void ShowAuxinMeshes (bool p_enable)
        {
            foreach (Auxin _a in Auxins)
                _a.ShowMesh(p_enable);
        }
        public void ShowCellMeshes(bool p_enable)
        {
            foreach (Cell _c in Cells)
                _c.ShowMesh(p_enable);
        }
    }
}