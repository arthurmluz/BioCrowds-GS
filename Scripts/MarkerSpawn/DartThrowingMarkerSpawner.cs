using Biocrowds.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DartThrowingMarkerSpawner : MarkerSpawner
{
    public int maxThrowingRetries = 500;

    public override IEnumerator CreateMarkers(List<Cell> cells, List<Auxin> auxins)
    {
        _auxinsContainer = new GameObject("Markers").transform;
        _cellSize = cells[0].transform.localScale.x;

        _maxMarkersPerCell = Mathf.RoundToInt(MarkerDensity / (MarkerRadius * MarkerRadius));

        // Generate a number of markers for each Cell
        for (int c = 0; c < cells.Count; c++)
        {
            StartCoroutine(PopulateCell(cells[c], auxins, c));
        }

        yield break;
    }

    private IEnumerator PopulateCell(Cell cell, List<Auxin> auxins, int cellIndex)
    {
        float cellHalfSize = (_cellSize / 2.0f) * (1.0f - (MarkerRadius/2f));

        // Set this counter to break the loop if it is taking too long (maybe there is no more space)
        int oldseed = Random.seed;

        for (int i = 0; i < _maxMarkersPerCell; i++)
        {           
            // Search position for new Marker
            Vector3 targetPosition;

            if (!GetRandomPositionInCell(cell, out targetPosition, cellHalfSize))
                continue;            

            // Creates new Marker and sets its data
            Auxin newMarker = Instantiate(auxinPrefab, targetPosition, Quaternion.identity, _auxinsContainer);
            SetLayerRecursively(newMarker.gameObject, LayerMask.NameToLayer("CellsHidden"));
            newMarker.transform.localScale = Vector3.one * MarkerRadius;
            newMarker.name = "Marker [" + cellIndex + "][" + i + "]";
            newMarker.Cell = cell;
            newMarker.Position = targetPosition;
            newMarker.ShowMesh(SceneController.ShowAuxins);

            auxins.Add(newMarker);
            cell.Auxins.Add(newMarker);
        }
        Random.InitState(oldseed);
        Random.Range(0, 1);
        yield break;
    }

    private bool GetRandomPositionInCell(Cell cell, out Vector3 position, float? cellHalfSize = null)
    {
        float chs = cellHalfSize ?? (_cellSize / 2.0f) * (1.0f - (MarkerRadius / 2f));

        // Search position for new Marker
        float x = Random.Range(-chs, chs);
        float z = Random.Range(-chs, chs);
        position = new Vector3(x, 0f, z) + cell.transform.position;
        bool found = ValidPositionInCell(cell, position);

        float _tries = 0;
        while (!found && _tries < maxThrowingRetries)
        {
            x = Random.Range(-chs, chs);
            z = Random.Range(-chs, chs);
            position = new Vector3(x, 0f, z) + cell.transform.position;
            found = ValidPositionInCell(cell, position);
            _tries++;
        }       

        return found;
    }

    private bool ValidPositionInCell(Cell cell, Vector3 position)
    {
        return !HasObstacleNearby(position) &&
               !HasMarkersNearby(position, cell.Auxins) &&
               IsOnNavmesh(position);
    }

}
