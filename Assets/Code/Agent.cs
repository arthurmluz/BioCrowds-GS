﻿/// ---------------------------------------------
/// Contact: Henry Braun
/// Brief: Defines an Agent
/// Thanks to VHLab for original implementation
/// Date: November 2017 
/// ---------------------------------------------

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;

namespace Biocrowds.Core
{
    public class Agent : MonoBehaviour
    {
        private const float UPDATE_NAVMESH_INTERVAL = 1.0f;

        //agent radius
        public float agentRadius;
        //agent speed
        public Vector3 _velocity;
        //max speed
        [SerializeField]
        private float _maxSpeed = 1.5f;

        //goal
        public GameObject Goal;
        // Multiple goals
        public List<GameObject> goalsList;
        public List<float> goalsWaitList;
        public bool isWaiting = false;
        [SerializeField]
        private float waitCount = 0f;

        [SerializeField]
        private int goalIndex = 0;
        public bool removeWhenGoalReached;

        public SpawnArea spawnArea;

        public float goalDistThreshold = 1.0f;

        //list with all auxins in his personal space
        [SerializeField]
        private List<Auxin> _auxins = new List<Auxin>();
        public List<Auxin> Auxins
        {
            get { return _auxins; }
            set { _auxins = value; }
        }

        //agent cell
        [SerializeField]
        private Cell _currentCell;
        public Cell CurrentCell
        {
            get { return _currentCell; }
            set { _currentCell = value; }
        }

        private World _world;
        public World World
        {
            get { return _world; }
            set { _world = value; }
        }

        private int _totalX;
        private int _totalZ;

        private NavMeshPath _navMeshPath;

        public VisualAgent _visualAgent;

        //time elapsed (to calculate path just between an interval of time)
        public float _elapsedTime;
        //auxins distance vector from agent
        public List<Vector3> _distAuxin;

        /*-----------Paravisis' model-----------*/
        private bool _isDenW = false; //  avoid recalculation
        private float _denW;    //  avoid recalculation
        private Vector3 _rotation; //orientation vector (movement)
        private Vector3 _goalPosition; //goal position
        private Vector3 _dirAgentGoal; //diff between goal and agent

        public int auxinCount;

        void Start()
        {
            _navMeshPath = new NavMeshPath();
            if (_visualAgent == null) _visualAgent = GetComponentInChildren<VisualAgent>();
           

            _goalPosition = Goal.transform.position;
            _dirAgentGoal = _goalPosition - transform.position;
            if (_visualAgent != null) _visualAgent.Initialize(transform.position, this);
            //cache world info
            _totalX = Mathf.FloorToInt(_world.Dimension.x / 2.0f) - 1;
            _totalZ = Mathf.FloorToInt(_world.Dimension.y / 2.0f);
        }

        public void NavmeshStep(float _timeStep)
        {
            //clear agent´s information
            ClearAgent();
            
            // Update the way to the goal every second.
            _elapsedTime += _timeStep;

            if (_elapsedTime > UPDATE_NAVMESH_INTERVAL)
            {
                UpdateGoalPositionAndNavmesh();
            }

            //draw line to goal
            if (_navMeshPath != null && SceneController.ShowNavMeshCorners)
            {
                for (int i = 0; i < _navMeshPath.corners.Length - 1; i++)
                    Debug.DrawLine(_navMeshPath.corners[i], _navMeshPath.corners[i + 1], Color.red);
            }
        }

        /*void Update()
        {
            //clear agent´s information
            ClearAgent();

            // Update the way to the goal every second.
            _elapsedTime += 0.02f;

            if (_elapsedTime > UPDATE_NAVMESH_INTERVAL)
            {
                UpdateGoalPositionAndNavmesh();
            }

            //draw line to goal
            for (int i = 0; i < _navMeshPath.corners.Length - 1; i++)
                Debug.DrawLine(_navMeshPath.corners[i], _navMeshPath.corners[i + 1], Color.red);
        }*/

        private void UpdateGoalPositionAndNavmesh()
        {
            if (goalIndex >= goalsList.Count)
                return;

            _elapsedTime = 0.0f;

            //calculate agent path
            //bool foundPath = NavMesh.CalculatePath(transform.position, Goal.transform.position, NavMesh.AllAreas, _navMeshPath);
            bool foundPath = NavMesh.CalculatePath(transform.position, goalsList[goalIndex].transform.position,
                NavMesh.AllAreas, _navMeshPath);
            //update its goal if path is found
            if (foundPath)
            {
                _goalPosition = new Vector3(_navMeshPath.corners[1].x, 0f, _navMeshPath.corners[1].z);
                _dirAgentGoal = _goalPosition - transform.position;
            }
            else
            {
                _goalPosition = goalsList[goalIndex].transform.position;
                _dirAgentGoal = _goalPosition - transform.position;
            }
        }

        public void UpdateVisualAgent()
        {
            if (_visualAgent != null) _visualAgent.Step();
        }

        //clear agent´s informations
        void ClearAgent()
        {
            //re-set inicial values
            _denW = 0;
            _distAuxin.Clear();
            _isDenW = false;
            _rotation = new Vector3(0f, 0f, 0f);
            _dirAgentGoal = _goalPosition - transform.position;
        }

        //walk
        public void MovementStep(float _timeStep)
        {
            if (_velocity.sqrMagnitude > 0.0f)
                transform.Translate(_velocity * _timeStep, Space.World);
        }

        public void WaitStep(float _timeStep)
        {
            if (goalIndex != goalsWaitList.Count - 1 && goalIndex + 1 > goalsWaitList.Count)
            {
                //Debug.LogError("No wait defined for current goal");
                return;
            }
            if (isWaiting)
            {
                waitCount += _timeStep;
                if (waitCount >= goalsWaitList[goalIndex])
                {
                    isWaiting = false;
                    goalIndex++;
                    UpdateGoalPositionAndNavmesh();
                }
            }
            else if (IsAtCurrentGoal() && goalIndex < goalsList.Count - 1)
            {
                if (goalsWaitList[goalIndex] >= 0.1f)
                {
                    waitCount = 0.0f;
                    isWaiting = true;
                }
                else
                {
                    waitCount = 0.0f;
                    goalIndex++;
                    UpdateGoalPositionAndNavmesh();
                }
            }
        }

        //The calculation formula starts here
        //the ideia is to find m=SUM[k=1 to n](Wk*Dk)
        //where k iterates between 1 and n (number of auxins), Dk is the vector to the k auxin and Wk is the weight of k auxin
        //the weight (Wk) is based on the degree resulting between the goal vector and the auxin vector (Dk), and the
        //distance of the auxin from the agent
        public void CalculateDirection()
        {
            //for each agent´s auxin
            for (int k = 0; k < _distAuxin.Count; k++)
            {
                //calculate W
                float valorW = CalculaW(k);
                if (_denW < 0.0001f)
                    valorW = 0.0f;

                //sum the resulting vector * weight (Wk*Dk)
                _rotation += valorW * _distAuxin[k] * _maxSpeed;
            }
        }

        //calculate W
        float CalculaW(int indiceRelacao)
        {
            //calculate F (F is part of weight formula)
            float fVal = GetF(indiceRelacao);

            if (!_isDenW)
            {
                _denW = 0f;

                //for each agent´s auxin
                for (int k = 0; k < _distAuxin.Count; k++)
                {
                    //calculate F for this k index, and sum up
                    _denW += GetF(k);
                }
                _isDenW = true;
            }

            return fVal / _denW;
        }

        //calculate F (F is part of weight formula)
        float GetF(int pRelationIndex)
        {
            //distance between auxin´s distance and origin 
            float Ymodule = Vector3.Distance(_distAuxin[pRelationIndex], Vector3.zero);
            //distance between goal vector and origin
            float Xmodule = _dirAgentGoal.normalized.magnitude;

            float dot = Vector3.Dot(_distAuxin[pRelationIndex], _dirAgentGoal.normalized);

            if (Ymodule < 0.00001f)
                return 0.0f;

            //return the formula, defined in thesis
            return (float)((1.0 / (1.0 + Ymodule)) * (1.0 + ((dot) / (Xmodule * Ymodule))));
        }

        //calculate speed vector    
        public void CalculateVelocity()
        {
            //distance between movement vector and origin
            float moduleM = Vector3.Distance(_rotation, Vector3.zero);

            //multiply for PI
            float s = moduleM * Mathf.PI;

            //if it is bigger than maxSpeed, use maxSpeed instead
            if (s > _maxSpeed)
                s = _maxSpeed;

            //Debug.Log("vetor M: " + m + " -- modulo M: " + s);
            if (moduleM > 0.0001f)
            {
                //calculate speed vector
                _velocity = s * (_rotation / moduleM);
            }
            else
            {
                //else, go idle
                _velocity = Vector3.zero;
            }
        }

        //find all auxins near him (Voronoi Diagram)
        //call this method from game controller, to make it sequential for each agent
        public void FindNearAuxins()
        {
            //clear them all, for obvious reasons
            _auxins.Clear();

            //get all auxins on my cell
            List<Auxin> cellAuxins = _currentCell.Auxins;

            //iterate all cell auxins to check distance between auxins and agent
            for (int i = 0; i < cellAuxins.Count; i++)
            {
                //see if the distance between this agent and this auxin is smaller than the actual value, and inside agent radius
                float distanceSqr = (transform.position - cellAuxins[i].Position).sqrMagnitude;
                if (distanceSqr < cellAuxins[i].MinDistance && distanceSqr <= agentRadius * agentRadius)
                {
                    //take the auxin!
                    //if this auxin already was taken, need to remove it from the agent who had it
                    if (cellAuxins[i].IsTaken)
                        cellAuxins[i].Agent.Auxins.Remove(cellAuxins[i]);

                    //auxin is taken
                    cellAuxins[i].IsTaken = true;

                    //auxin has agent
                    cellAuxins[i].Agent = this;
                    //update min distance
                    cellAuxins[i].MinDistance = distanceSqr;
                    //update my auxins
                    _auxins.Add(cellAuxins[i]);
                }
            }

            FindCell();
        }

        private void FindCell()
        {
            //distance from agent to cell, to define agent new cell
            float distanceToCellSqr = (transform.position - _currentCell.transform.position).sqrMagnitude; //Vector3.Distance(transform.position, _currentCell.transform.position);

            //cap the limits
            //[ ][ ][ ]
            //[ ][X][ ]
            //[ ][ ][ ]
            if (_currentCell.X > 0)
                CheckAuxins(ref distanceToCellSqr, _world.Cells[(_currentCell.X - 1) * _totalZ + (_currentCell.Z + 0)]);

            if (_currentCell.X > 0 && _currentCell.Z < _totalZ - 1)
                CheckAuxins(ref distanceToCellSqr, _world.Cells[(_currentCell.X - 1) * _totalZ + (_currentCell.Z + 1)]);

            if (_currentCell.X > 0 && _currentCell.Z > 0)
                CheckAuxins(ref distanceToCellSqr, _world.Cells[(_currentCell.X - 1) * _totalZ + (_currentCell.Z - 1)]);

            if (_currentCell.Z < _totalZ - 1)
                CheckAuxins(ref distanceToCellSqr, _world.Cells[(_currentCell.X + 0) * _totalZ + (_currentCell.Z + 1)]);

            if (_currentCell.X < _totalX && _currentCell.Z < _totalZ - 1)
                CheckAuxins(ref distanceToCellSqr, _world.Cells[(_currentCell.X + 1) * _totalZ + (_currentCell.Z + 1)]);

            if (_currentCell.X < _totalX)
                CheckAuxins(ref distanceToCellSqr, _world.Cells[(_currentCell.X + 1) * _totalZ + (_currentCell.Z + 0)]);

            if (_currentCell.X < _totalX && _currentCell.Z > 0)
                CheckAuxins(ref distanceToCellSqr, _world.Cells[(_currentCell.X + 1) * _totalZ + (_currentCell.Z - 1)]);

            if (_currentCell.Z > 0)
                CheckAuxins(ref distanceToCellSqr, _world.Cells[(_currentCell.X + 0) * _totalZ + (_currentCell.Z - 1)]);

        }

        private void CheckAuxins(ref float pDistToCellSqr, Cell pCell)
        {
            //get all auxins on neighbourcell
            List<Auxin> cellAuxins = pCell.Auxins;

            //iterate all cell auxins to check distance between auxins and agent
            for (int c = 0; c < cellAuxins.Count; c++)
            {
                //see if the distance between this agent and this auxin is smaller than the actual value, and smaller than agent radius
                float distanceSqr = (transform.position - cellAuxins[c].Position).sqrMagnitude;
                if (distanceSqr < cellAuxins[c].MinDistance && distanceSqr <= agentRadius * agentRadius)
                {
                    //take the auxin
                    //if this auxin already was taken, need to remove it from the agent who had it
                    if (cellAuxins[c].IsTaken)
                        cellAuxins[c].Agent.Auxins.Remove(cellAuxins[c]);

                    //auxin is taken
                    cellAuxins[c].IsTaken = true;
                    //auxin has agent
                    cellAuxins[c].Agent = this;
                    //update min distance
                    cellAuxins[c].MinDistance = distanceSqr;
                    //update my auxins
                    _auxins.Add(cellAuxins[c]);
                }
            }

            //see distance to this cell
            float distanceToNeighbourCell = (transform.position - pCell.transform.position).sqrMagnitude; 
            if (distanceToNeighbourCell < pDistToCellSqr)
            {
                pDistToCellSqr = distanceToNeighbourCell;
                _currentCell = pCell;
            }
        }
        public bool IsAtCurrentGoal()
        {
            //Debug.Log(name + " : " + Vector3.Distance(transform.position, _goalPosition));
            Vector2 agentPos = new Vector2(transform.position.x, transform.position.z);
            Vector2 goalPos = new Vector2(goalsList[goalIndex].transform.position.x,
                goalsList[goalIndex].transform.position.z);
            return (Vector2.Distance(agentPos, goalPos) <= goalDistThreshold);
        }

        public bool IsAtFinalGoal()
        {
            //Debug.Log(name + " : " + Vector3.Distance(transform.position, goalsList[goalsList.Count - 1].transform.position));
            Vector2 agentPos = new Vector2(transform.position.x, transform.position.z);
            Vector2 goalPos = new Vector2(goalsList[goalsList.Count - 1].transform.position.x,
                goalsList[goalsList.Count - 1].transform.position.z);
            return (Vector2.Distance(agentPos, goalPos) <= goalDistThreshold);
        }
    }
}