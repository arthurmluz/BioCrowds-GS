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
using UnityEngine.AI;
using System.Linq;

namespace Biocrowds.Core
{
    public class World : MonoBehaviour
    {
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
        public Vector2 Offset
        {
            get { return _offset; }
        }

        //agent prefab
        [SerializeField]
        protected List<Agent> _agentPrefabList;

        [SerializeField]
        protected Cell _cellPrefab;

        [SerializeField]
        protected Auxin _auxinPrefab;


        [SerializeField]
        public List<Agent> _agents = new List<Agent>();
        public List<Agent> _finishedAgents = new List<Agent>();
        List<Cell> _cells = new List<Cell>();
        List<Auxin> _auxins = new List<Auxin>();

        [SerializeField] protected List<SpawnArea> spawnAreas;
        public List<SpawnArea> Areas => spawnAreas;

        public List<Rect> MarkerExclusionZones { get; set; } = new List<Rect>();

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


        //max auxins on the ground
        protected bool _isReady;
        public bool Ready => _isReady;

        protected bool _isFinished;
        public bool Finished => _isFinished;        
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
            Debug.Log(_auxins.Count/_cells.Count);

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
            Vector3 _spawnPos = new Vector3();

            for (int i = 0; i < _dimension.x / 2; i++) //i + agentRadius * 2
            {
                for (int j = 0; j < _dimension.y / 2; j++) // j + agentRadius * 2
                {
                    //instantiante a new cell
                    _spawnPos.x = (1.0f + (i * 2.0f)) + _offset.x;
                    _spawnPos.z = (1.0f + (j * 2.0f)) + _offset.y;

                    Cell newCell = Instantiate(_cellPrefab, _spawnPos, Quaternion.Euler(90.0f, 0.0f, 0.0f), cellPool);
                    SetLayerRecursively(newCell.gameObject, LayerMask.NameToLayer("CellsHidden"));

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

            // Update de Navmesh for each agent 
            for (int i = 0; i < _agents.Count; i++)
                _agents[i].UpdateVisualAgent();

            //reset auxins
            for (int i = 0; i < _cells.Count; i++)
                for (int j = 0; j < _cells[i].Auxins.Count; j++)
                    _cells[i].Auxins[j].ResetAuxin();

           

            //find nearest auxins for each agent
            for (int i = 0; i < _agents.Count; i++)
                _agents[i].FindNearAuxins();

            for (int i = 0; i < _agents.Count; i++)
                _agents[i].auxinCount = _agents[i].Auxins.Count;
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
                if (!_agents[i].isWaiting)
                    _agents[i].MovementStep(deltaTime);

                _agents[i].WaitStep(deltaTime);
                //if (_agents[i].IsAtCurrentGoal() && !_agents[i].isWaiting)


                if (_agents[i].removeWhenGoalReached && _agents[i].IsAtFinalGoal())
                    _agentsToRemove.Add(_agents[i]);
            }

            foreach(Agent a in _agentsToRemove)
            {
                _agents.Remove(a);
                _finishedAgents.Add(a);
                a.gameObject.SetActive(false);
                OnAgentFinished?.Invoke(a);
            }
            _agentsToRemove.Clear();

            // Update de Navmesh for each agent 
            for (int i = 0; i < _agents.Count; i++)
                _agents[i].NavmeshStep(deltaTime);            

            // check if finished
            _isFinished = _agents.Count == 0 && spawnAreas.All(x => x.Finished);

            OnAfterUpdate?.Invoke();

            if (_isFinished)
                OnSimulationFinished?.Invoke();            
        }

        protected Cell GetClosestCellToPoint (Vector3 point)
        {
            float _minDist = Vector3.Distance(point, _cells[0].transform.position);
            int _minIndex = 0;
            for (int i = 1; i < _cells.Count; i ++)
            {
                if (Vector3.Distance(point, _cells[i].transform.position) < _minDist)
                {
                    _minDist = Vector3.Distance(point, _cells[i].transform.position);
                    _minIndex = i;
                }
            }

            return _cells[_minIndex];
        }

        protected void SpawnNewAgent(Vector3 _pos, bool _removeWhenGoalReached, 
            List<GameObject> _goalList)
        {
            Agent newAgent = Instantiate(_agentPrefabList[Random.Range(0, _agentPrefabList.Count)],
                _pos, Quaternion.identity, _agentsContainer);
            newAgent.name = "Agent [" + GetNewAgentID() + "]";  //name
            newAgent.CurrentCell = GetClosestCellToPoint(_pos);
            newAgent.agentRadius = AGENT_RADIUS;  //agent radius
            GameObject nearestGoal = GetNearestGoal(_pos, _goalList);
            newAgent.Goal = nearestGoal ?? _goalList[0];  //agent goal
            newAgent.goalsList = BuildSingleGoalList(newAgent.Goal);
            newAgent.goalsWaitList = BuildSingleWaitList();
            newAgent.removeWhenGoalReached = _removeWhenGoalReached;
            newAgent.World = this;
            _agents.Add(newAgent);
        }

        protected void SpawnNewAgentInArea(SpawnArea _area, bool _isInitialSpawn)
        {
            // Get a random point to a random cell
            Vector3 _pos = _area.GetRandomPoint();
            Cell c = GetClosestCellToPoint(_pos);

            int oldSeed = Random.seed;
            int tries = 0;
            bool found = c.Auxins.Count > 0;
            while (!found && tries < 500)
            {
                // while cell is not traversable, randomize another cell
                _pos = _area.GetRandomPoint();
                c = GetClosestCellToPoint(_pos);
                tries++;
                found = c.Auxins.Count > 0;
            }
            // return seed to oldstate to not disrturb random sequentiation
            Random.InitState(oldSeed);
            Random.Range(0, 1);

            if (!found)
            {
                Debug.LogError("Could not find cells with auxins to spawn agent");
                return;
            }

            _pos = c.Auxins[Random.Range(0, c.Auxins.Count)].Position;
                


            Agent newAgent = Instantiate(_agentPrefabList[Random.Range(0, _agentPrefabList.Count)], 
                _pos, Quaternion.identity, _agentsContainer);
            newAgent.name = "Agent [" + GetNewAgentID() + "]";  //name
            newAgent.CurrentCell = GetClosestCellToPoint(_pos);
            newAgent.agentRadius = AGENT_RADIUS;  //agent radius
            newAgent.goalDistThreshold = GOAL_DISTANCE_THRESHOLD;
            if (_isInitialSpawn)
            {
                GameObject nearestGoal = GetNearestGoal(_pos, _area.initialAgentsGoalList);
                newAgent.Goal = nearestGoal ?? _area.initialAgentsGoalList[0];  //agent goal
                newAgent.goalsList = BuildSingleGoalList(newAgent.Goal);
                newAgent.removeWhenGoalReached = _area.initialRemoveWhenGoalReached;
                newAgent.goalsWaitList = BuildSingleWaitList();
            }
            else
            {
                GameObject nearestGoal = GetNearestGoal(_pos, _area.repeatingGoalList);
                newAgent.Goal = nearestGoal ?? _area.repeatingGoalList[0];  //agent goal
                newAgent.goalsList = BuildSingleGoalList(newAgent.Goal);
                newAgent.removeWhenGoalReached = _area.repeatingRemoveWhenGoalReached;
                newAgent.goalsWaitList = BuildSingleWaitList();
                if(_area.limitRepeatingSpawn) {
                    _area.quantityLimitToSpawn--;
                    Debug.Log("Area " + _area.name + " has " + _area.quantityLimitToSpawn + " spawns left.");
                }
            }
            newAgent.World = this;
            newAgent.Initialize();

            _agents.Add(newAgent);

            OnAgentSpawned?.Invoke(_area, newAgent);
        }

        protected int GetNewAgentID()
        {
            _newAgentID++;
            return _newAgentID - 1;
        }

        private GameObject GetNearestGoal(Vector3 position, List<GameObject> goals)
        {
            if (goals == null || goals.Count == 0)
                return null;

            GameObject nearestGoal = null;
            float nearestDistanceSqr = float.MaxValue;

            foreach (GameObject goal in goals)
            {
                if (goal == null)
                    continue;

                float distanceSqr = (goal.transform.position - position).sqrMagnitude;
                if (distanceSqr < nearestDistanceSqr)
                {
                    nearestDistanceSqr = distanceSqr;
                    nearestGoal = goal;
                }
            }

            return nearestGoal;
        }

        private List<GameObject> BuildSingleGoalList(GameObject goal)
        {
            return goal != null ? new List<GameObject> { goal } : new List<GameObject>();
        }

        private List<float> BuildSingleWaitList()
        {
            return new List<float> { 0f };
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

        private void SetLayerRecursively(GameObject target, int layer)
        {
            if (target == null || layer < 0)
                return;

            target.layer = layer;
            foreach (Transform child in target.transform)
                SetLayerRecursively(child.gameObject, layer);
        }
    }
}