using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MazeGenerator : MonoBehaviour
{
    [SerializeField]
    private MazeCell _mazeCellPrefab;

    [SerializeField]
    private MazeCell _roomPrefab;

    [SerializeField]
    private int _mazeWidth = 10;

    [SerializeField]
    private int _mazeDepth = 10;

    [SerializeField]
    private float _cellSize = 1f;

    [SerializeField]
    private bool _animateGeneration = true;

    [SerializeField]
    private float _generationStepDelay = 0.05f;

    [SerializeField]
    private int _numberOfRooms = 5;

    private MazeCell[,] _mazeGrid;
    private List<Vector2Int> _roomPositions = new List<Vector2Int>();

    void Start()
    {
        _mazeGrid = new MazeCell[_mazeWidth, _mazeDepth];

        if (_animateGeneration)
        {
            StartCoroutine(GenerateMazeRoutine());
        }
        else
        {
            GenerateMaze();
        }
    }

    private void GenerateMaze()
    {
        // Create all cells
        for (int x = 0; x < _mazeWidth; x++)
        {
            for (int z = 0; z < _mazeDepth; z++)
            {
                Vector3 position = new Vector3(x * _cellSize, 0, z * _cellSize);
                MazeCell cell = Instantiate(_mazeCellPrefab, position, Quaternion.identity, transform);
                _mazeGrid[x, z] = cell;
            }
        }

        // Generate maze using recursive backtracking
        GenerateMazeRecursive(0, 0);

        // Place special rooms
        PlaceRooms();
    }

    private IEnumerator GenerateMazeRoutine()
    {
        // Create all cells
        for (int x = 0; x < _mazeWidth; x++)
        {
            for (int z = 0; z < _mazeDepth; z++)
            {
                Vector3 position = new Vector3(x * _cellSize, 0, z * _cellSize);
                MazeCell cell = Instantiate(_mazeCellPrefab, position, Quaternion.identity, transform);
                _mazeGrid[x, z] = cell;
            }
        }

        yield return new WaitForSeconds(0.5f);

        // Generate maze using recursive backtracking with animation
        yield return StartCoroutine(GenerateMazeRecursiveRoutine(0, 0));

        // Place special rooms after maze generation
        PlaceRooms();
    }

    private void GenerateMazeRecursive(int x, int z)
    {
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        Vector2Int current = new Vector2Int(x, z);

        _mazeGrid[current.x, current.y].Visit();
        stack.Push(current);

        while (stack.Count > 0)
        {
            current = stack.Peek();
            List<Vector2Int> unvisitedNeighbors = GetUnvisitedNeighbors(current.x, current.y);

            if (unvisitedNeighbors.Count > 0)
            {
                Vector2Int chosen = unvisitedNeighbors[Random.Range(0, unvisitedNeighbors.Count)];
                RemoveWallBetween(current, chosen);
                _mazeGrid[chosen.x, chosen.y].Visit();
                stack.Push(chosen);
            }
            else
            {
                stack.Pop();
            }
        }
    }

    private IEnumerator GenerateMazeRecursiveRoutine(int x, int z)
    {
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        Vector2Int current = new Vector2Int(x, z);

        _mazeGrid[current.x, current.y].Visit();
        stack.Push(current);

        while (stack.Count > 0)
        {
            current = stack.Peek();
            List<Vector2Int> unvisitedNeighbors = GetUnvisitedNeighbors(current.x, current.y);

            if (unvisitedNeighbors.Count > 0)
            {
                Vector2Int chosen = unvisitedNeighbors[Random.Range(0, unvisitedNeighbors.Count)];
                RemoveWallBetween(current, chosen);
                _mazeGrid[chosen.x, chosen.y].Visit();
                stack.Push(chosen);

                yield return new WaitForSeconds(_generationStepDelay);
            }
            else
            {
                stack.Pop();
            }
        }
    }

    private void PlaceRooms()
    {
        Vector2Int startCell = new Vector2Int(0, 0);
        int roomsPlaced = 0;
        int minDistanceBetweenRooms = 4; // Minimum cells between rooms

        // Create a list of ALL valid cells in the maze (excluding start and edges)
        List<Vector2Int> allValidCells = new List<Vector2Int>();
        for (int x = 1; x < _mazeWidth - 1; x++)
        {
            for (int z = 1; z < _mazeDepth - 1; z++)
            {
                // Only include cells that are at least distance 3 from start
                int distanceFromStart = Mathf.Abs(x - startCell.x) + Mathf.Abs(z - startCell.y);
                if (distanceFromStart >= 3)
                {
                    allValidCells.Add(new Vector2Int(x, z));
                }
            }
        }

        // Shuffle the entire list using Fisher-Yates algorithm
        for (int i = allValidCells.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Vector2Int temp = allValidCells[i];
            allValidCells[i] = allValidCells[j];
            allValidCells[j] = temp;
        }

        // Try to place rooms from the shuffled list
        foreach (Vector2Int candidate in allValidCells)
        {
            if (roomsPlaced >= _numberOfRooms)
                break;

            // Check if this position is far enough from other rooms
            bool tooClose = false;
            foreach (Vector2Int roomPos in _roomPositions)
            {
                float distance = Vector2.Distance(
                    new Vector2(candidate.x, candidate.y),
                    new Vector2(roomPos.x, roomPos.y)
                );

                if (distance < minDistanceBetweenRooms)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                ReplaceWithRoom(candidate.x, candidate.y);
                _roomPositions.Add(candidate);
                roomsPlaced++;
                int distFromStart = Mathf.Abs(candidate.x) + Mathf.Abs(candidate.y);
                Debug.Log($"Room {roomsPlaced} placed at ({candidate.x}, {candidate.y}) - Manhattan distance from start: {distFromStart}");
            }
        }

        if (roomsPlaced < _numberOfRooms)
        {
            Debug.LogWarning($"Could only place {roomsPlaced} rooms out of {_numberOfRooms} requested. Try increasing maze size or decreasing number of rooms/minimum distance.");
        }
    }

    private void ReplaceWithRoom(int x, int z)
    {
        if (_roomPrefab == null)
        {
            Debug.LogWarning("Room Prefab is not assigned!");
            return;
        }

        // Store wall states from old cell
        bool hasLeftWall = _mazeGrid[x, z].HasLeftWall();
        bool hasRightWall = _mazeGrid[x, z].HasRightWall();
        bool hasFrontWall = _mazeGrid[x, z].HasFrontWall();
        bool hasBackWall = _mazeGrid[x, z].HasBackWall();

        // Destroy old cell
        Destroy(_mazeGrid[x, z].gameObject);

        // Create room cell
        Vector3 position = new Vector3(x * _cellSize, 0, z * _cellSize);
        MazeCell roomCell = Instantiate(_roomPrefab, position, Quaternion.identity, transform);
        roomCell.Visit();
        _mazeGrid[x, z] = roomCell;

        // Apply wall states to new room
        if (!hasLeftWall)
            roomCell.ClearLeftWall();
        if (!hasRightWall)
            roomCell.ClearRightWall();
        if (!hasFrontWall)
            roomCell.ClearFrontWall();
        if (!hasBackWall)
            roomCell.ClearBackWall();
    }

    private List<Vector2Int> GetValidNeighbors(int x, int z)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();

        if (x + 1 < _mazeWidth)
            neighbors.Add(new Vector2Int(x + 1, z));
        if (x - 1 >= 0)
            neighbors.Add(new Vector2Int(x - 1, z));
        if (z + 1 < _mazeDepth)
            neighbors.Add(new Vector2Int(x, z + 1));
        if (z - 1 >= 0)
            neighbors.Add(new Vector2Int(x, z - 1));

        return neighbors;
    }

    private List<Vector2Int> GetCellsAtDistance(Vector2Int start, int distance)
    {
        List<Vector2Int> cells = new List<Vector2Int>();

        for (int x = 0; x < _mazeWidth; x++)
        {
            for (int z = 0; z < _mazeDepth; z++)
            {
                // Fixed: was using start.y instead of start.y for z coordinate
                int manhattanDistance = Mathf.Abs(x - start.x) + Mathf.Abs(z - start.y);
                if (manhattanDistance >= distance && manhattanDistance < distance + 2)
                {
                    cells.Add(new Vector2Int(x, z));
                }
            }
        }

        // Shuffle the entire list for better randomization
        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int temp = cells[i];
            int randomIndex = Random.Range(i, cells.Count);
            cells[i] = cells[randomIndex];
            cells[randomIndex] = temp;
        }

        return cells;
    }

    private List<Vector2Int> GetUnvisitedNeighbors(int x, int z)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();

        if (x + 1 < _mazeWidth && !_mazeGrid[x + 1, z].IsVisited)
            neighbors.Add(new Vector2Int(x + 1, z));
        if (x - 1 >= 0 && !_mazeGrid[x - 1, z].IsVisited)
            neighbors.Add(new Vector2Int(x - 1, z));
        if (z + 1 < _mazeDepth && !_mazeGrid[x, z + 1].IsVisited)
            neighbors.Add(new Vector2Int(x, z + 1));
        if (z - 1 >= 0 && !_mazeGrid[x, z - 1].IsVisited)
            neighbors.Add(new Vector2Int(x, z - 1));

        return neighbors;
    }

    private void RemoveWallBetween(Vector2Int current, Vector2Int neighbor)
    {
        if (neighbor.x > current.x)
        {
            _mazeGrid[current.x, current.y].ClearRightWall();
            _mazeGrid[neighbor.x, neighbor.y].ClearLeftWall();
        }
        else if (neighbor.x < current.x)
        {
            _mazeGrid[current.x, current.y].ClearLeftWall();
            _mazeGrid[neighbor.x, neighbor.y].ClearRightWall();
        }
        else if (neighbor.y > current.y)
        {
            _mazeGrid[current.x, current.y].ClearFrontWall();
            _mazeGrid[neighbor.x, neighbor.y].ClearBackWall();
        }
        else if (neighbor.y < current.y)
        {
            _mazeGrid[current.x, current.y].ClearBackWall();
            _mazeGrid[neighbor.x, neighbor.y].ClearFrontWall();
        }
    }

    public MazeCell GetMazeCell(int x, int z)
    {
        if (x >= 0 && x < _mazeWidth && z >= 0 && z < _mazeDepth)
            return _mazeGrid[x, z];
        return null;
    }
}