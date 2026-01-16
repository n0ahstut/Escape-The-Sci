using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

public class MazeGenerator : MonoBehaviour
{
    [Header("Maze Settings")]
    [SerializeField] private MazeCell _mazeCellPrefab;
    [SerializeField] private MazeCell _roomPrefab;
    [SerializeField] private MazeCell _bathroomPrefab;
    [SerializeField] private int _mazeWidth = 10;
    [SerializeField] private int _mazeDepth = 10;
    [SerializeField] private float _cellSize = 1f;

    [Header("Generation Settings")]
    [SerializeField] private bool _animateGeneration = true;
    [SerializeField] private float _generationStepDelay = 0.05f;
    [SerializeField] private int _numberOfRooms = 3;
    [SerializeField] private int _numberOfBathrooms = 3;

    [Header("Spawning")]
    [SerializeField] private GameObject _playerPrefab;
    [SerializeField] private GameObject _deanPrefab;
    [SerializeField] private float _spawnY = 12f;
    [SerializeField] private float _deanHeightOffset = -0.3f;
    
    [Header("Teacher Prefabs")]
    [SerializeField] private GameObject _mrDahlemPrefab;
    [SerializeField] private GameObject _msMcGuiganPrefab;
    [SerializeField] private GameObject _mrNoodyPrefab;
    [SerializeField] private float _teacherHeightOffset = -0.3f;
    [SerializeField] private Vector3 _teacherSpawnOffset = new Vector3(5f, 0f, 5f);

    [Header("Gym Equipment")]
    [SerializeField] private GameObject _basketballPrefab;
    [SerializeField] private GameObject _hoopPrefab;
    [SerializeField] private Vector3 _basketballOffset = new Vector3(2f, 0.5f, 0f);
    [SerializeField] private Vector3 _hoopOffset = new Vector3(0f, 0f, 8f);
    [SerializeField] private float _hoopRotation = 0f;

    [Header("Student Spawning")]
    [SerializeField] private GameObject _studentPrefab;
    [SerializeField] private float _studentHeightOffset = -0.3f;

    [Header("Maze Colors")]
    [SerializeField] private Color _hallwayWallColor = new Color(1f, 1f, 0.6f, 1f); // Light yellow
    [SerializeField] private Color _roofColor = new Color(0.2f, 0.8f, 0.2f, 1f); // Green
    [SerializeField] private bool _createRoof = true;
    [SerializeField] private float _roofHeight = 4f;

    [Header("Debug")]
    [SerializeField] private bool _debugSpawnNearDahlem = false;

    private MazeCell[,] _mazeGrid;
    private List<Vector2Int> _roomPositions = new List<Vector2Int>();
    private List<Vector2Int> _bathroomPositions = new List<Vector2Int>();
    private GameObject _playerInstance;
    private List<GameObject> _deanInstances = new List<GameObject>();
    private List<GameObject> _teacherInstances = new List<GameObject>();
    private List<GameObject> _studentInstances = new List<GameObject>();
    
    private enum Corner { BottomLeft, BottomRight, TopLeft, TopRight }
    private Corner _playerCorner;

    public float CellSize => _cellSize;
    public int MazeWidth => _mazeWidth;
    public int MazeDepth => _mazeDepth;
    public List<Vector2Int> RoomPositions => _roomPositions;
    public List<Vector2Int> BathroomPositions => _bathroomPositions;

    public static MazeGenerator Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
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
        for (int x = 0; x < _mazeWidth; x++)
        {
            for (int z = 0; z < _mazeDepth; z++)
            {
                Vector3 position = new Vector3(x * _cellSize, 0, z * _cellSize);
                MazeCell cell = Instantiate(_mazeCellPrefab, position, Quaternion.identity, transform);
                _mazeGrid[x, z] = cell;
            }
        }

        GenerateMazeRecursive(0, 0);
        PlaceRooms();
        BakeNavMesh();
        StartCoroutine(SpawnAfterNavMesh());
    }

    private IEnumerator SpawnAfterNavMesh()
    {
        // Wait a frame for NavMesh to be ready
        yield return null;
        yield return null;
        
        ColorMazeWalls();
        if (_createRoof)
        {
            CreateRoof();
        }
        
        SpawnTeachers();
        SpawnStudents();
        SpawnPlayer();
        SpawnDeans();
    }

    private IEnumerator GenerateMazeRoutine()
    {
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
        yield return StartCoroutine(GenerateMazeRecursiveRoutine(0, 0));

        PlaceRooms();
        BakeNavMesh();
        
        // Wait for NavMesh to be ready
        yield return null;
        yield return null;
        
        ColorMazeWalls();
        if (_createRoof)
        {
            CreateRoof();
        }
        
        SpawnTeachers();
        SpawnStudents();
        SpawnPlayer();
        SpawnDeans();
    }

    private void BakeNavMesh()
    {
        NavMeshSurface navMeshSurface = GetComponent<NavMeshSurface>();
        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
            Debug.Log("NavMesh baked successfully!");
        }
        else
        {
            Debug.LogWarning("NavMeshSurface component not found on MazeGenerator! Add it manually.");
        }
    }

    private void ColorMazeWalls()
    {
        // Create a light yellow material for hallway walls
        Material hallwayMaterial = new Material(Shader.Find("Standard"));
        hallwayMaterial.color = _hallwayWallColor;
        
        // Create a blue material for bathroom walls
        Material bathroomMaterial = new Material(Shader.Find("Standard"));
        bathroomMaterial.color = new Color(0.6f, 0.8f, 1f, 1f); // Light blue
        
        for (int x = 0; x < _mazeWidth; x++)
        {
            for (int z = 0; z < _mazeDepth; z++)
            {
                MazeCell cell = _mazeGrid[x, z];
                if (cell == null) continue;
                
                // Color bathrooms light blue
                if (cell.IsBathroom)
                {
                    Renderer[] renderers = cell.GetComponentsInChildren<Renderer>();
                    foreach (Renderer renderer in renderers)
                    {
                        if (renderer.gameObject.name.ToLower().Contains("floor")) continue;
                        renderer.material = bathroomMaterial;
                    }
                }
                // Color hallways light yellow (skip rooms)
                else if (!cell.IsRoom)
                {
                    Renderer[] renderers = cell.GetComponentsInChildren<Renderer>();
                    foreach (Renderer renderer in renderers)
                    {
                        if (renderer.gameObject.name.ToLower().Contains("floor")) continue;
                        renderer.material = hallwayMaterial;
                    }
                }
            }
        }
        
        Debug.Log("Maze walls colored!");
    }

    private void CreateRoof()
    {
        // Create a green material for the roof
        Material roofMaterial = new Material(Shader.Find("Standard"));
        roofMaterial.color = _roofColor;
        
        // Create a single large roof plane
        GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.name = "MazeRoof";
        roof.transform.SetParent(transform);
        
        // Size it to cover the entire maze (1.5x for extra coverage)
        float roofWidth = _mazeWidth * _cellSize * 1.5f;
        float roofDepth = _mazeDepth * _cellSize * 1.5f;
        
        roof.transform.localScale = new Vector3(roofWidth, 0.2f, roofDepth);
        roof.transform.position = new Vector3(
            140f,
            _spawnY + _roofHeight,
            roofDepth / 2f + 10f
        );
        
        // Apply material
        Renderer roofRenderer = roof.GetComponent<Renderer>();
        roofRenderer.material = roofMaterial;
        
        // Remove collider so player can't bump into it from below
        Collider roofCollider = roof.GetComponent<Collider>();
        if (roofCollider != null)
        {
            Destroy(roofCollider);
        }
        
        Debug.Log($"Roof created at Y={_spawnY + _roofHeight}");
    }

    private void SpawnPlayer()
    {
        if (_playerPrefab == null)
        {
            Debug.LogWarning("Player prefab not assigned!");
            return;
        }

        Vector3 spawnPos;
        
        if (_debugSpawnNearDahlem && _teacherInstances.Count > 0)
        {
            // Spawn near Mr. Dahlem (first teacher)
            spawnPos = _teacherInstances[0].transform.position + new Vector3(2f, 0, 2f);
            spawnPos.y = _spawnY;
            Debug.Log($"DEBUG: Player spawned near Mr. Dahlem: {spawnPos}");
        }
        else
        {
            _playerCorner = (Corner)Random.Range(0, 4);
            spawnPos = GetCornerPosition(_playerCorner);
            Debug.Log($"Player spawned at {_playerCorner} corner: {spawnPos}");
        }
        
        _playerInstance = Instantiate(_playerPrefab, spawnPos, Quaternion.identity);
        _playerInstance.tag = "Player";
    }

    private void SpawnTeachers()
    {
        GameObject[] teacherPrefabs = new GameObject[]
        {
            _mrDahlemPrefab,
            _msMcGuiganPrefab,
            _mrNoodyPrefab
        };
        
        string[] teacherNames = new string[]
        {
            "Mr. Dahlem",
            "Ms. Guig",
            "Mr. Noody"
        };
        
        TeacherType[] teacherTypes = new TeacherType[]
        {
            TeacherType.MrDahlem,
            TeacherType.MsMcGuigan,
            TeacherType.MrNoody
        };
        
        for (int i = 0; i < _roomPositions.Count && i < teacherPrefabs.Length; i++)
        {
            if (teacherPrefabs[i] == null)
            {
                Debug.LogWarning($"Teacher prefab for {teacherNames[i]} not assigned!");
                continue;
            }
            
            Vector2Int roomPos = _roomPositions[i];
            MazeCell roomCell = _mazeGrid[roomPos.x, roomPos.y];
            
            // Use the room cell's transform position plus offset
            Vector3 spawnPos = new Vector3(
                roomCell.transform.position.x + _teacherSpawnOffset.x,
                _spawnY + _teacherHeightOffset,
                roomCell.transform.position.z + _teacherSpawnOffset.z
            );
            
            Debug.Log($"Teacher spawn Y: {spawnPos.y}, SpawnY: {_spawnY}, HeightOffset: {_teacherHeightOffset}");
            
            GameObject teacher = Instantiate(teacherPrefabs[i], spawnPos, Quaternion.identity);
            teacher.name = teacherNames[i];
            
            // Parent teacher to the room so they stay together
            teacher.transform.SetParent(roomCell.transform);
            
            TeacherController teacherController = teacher.GetComponent<TeacherController>();
            if (teacherController != null)
            {
                teacherController.SetTeacherType(teacherTypes[i]);
            }
            
            _teacherInstances.Add(teacher);
            Debug.Log($"{teacherNames[i]} spawned in room at world position {spawnPos}, cell ({roomPos.x}, {roomPos.y}), IsRoom: {roomCell.IsRoom}");
            
            // Spawn gym equipment for Mr. Dahlem
            if (teacherTypes[i] == TeacherType.MrDahlem)
            {
                SpawnGymEquipment(spawnPos, roomCell);
            }
        }
        
        if (_roomPositions.Count < teacherPrefabs.Length)
        {
            Debug.LogWarning($"Not enough rooms for all teachers! Rooms: {_roomPositions.Count}, Teachers: {teacherPrefabs.Length}");
        }
    }

    private void SpawnGymEquipment(Vector3 teacherPos, MazeCell roomCell)
    {
        // Spawn basketball near the teacher
        if (_basketballPrefab != null)
        {
            Vector3 ballPos = teacherPos + _basketballOffset;
            GameObject ball = Instantiate(_basketballPrefab, ballPos, Quaternion.identity);
            ball.name = "Basketball";
            ball.transform.SetParent(roomCell.transform);
            Debug.Log($"Basketball spawned at {ballPos}");
        }
        else
        {
            Debug.LogWarning("Basketball prefab not assigned!");
        }
        
        // Spawn hoop across from teacher
        if (_hoopPrefab != null)
        {
            Vector3 hoopPos = teacherPos + _hoopOffset;
            GameObject hoop = Instantiate(_hoopPrefab, hoopPos, Quaternion.Euler(0, _hoopRotation, 0));
            hoop.name = "Hoop";
            hoop.transform.SetParent(roomCell.transform);
            Debug.Log($"Hoop spawned at {hoopPos}");
        }
        else
        {
            Debug.LogWarning("Hoop prefab not assigned!");
        }
    }

    private Vector3 GetCornerPosition(Corner corner)
    {
        int x = 0;
        int z = 0;
        
        switch (corner)
        {
            case Corner.BottomLeft:
                x = 2;
                z = 2;
                break;
            case Corner.BottomRight:
                x = _mazeWidth - 3;
                z = 2;
                break;
            case Corner.TopLeft:
                x = 2;
                z = _mazeDepth - 3;
                break;
            case Corner.TopRight:
                x = _mazeWidth - 3;
                z = _mazeDepth - 3;
                break;
        }
        
        return new Vector3(x * _cellSize, _spawnY, z * _cellSize);
    }

    private void SpawnDeans()
    {
        if (_deanPrefab == null)
        {
            Debug.LogWarning("Dean prefab not assigned!");
            return;
        }

        List<Corner> deanCorners = new List<Corner>();
        foreach (Corner corner in System.Enum.GetValues(typeof(Corner)))
        {
            if (corner != _playerCorner)
            {
                deanCorners.Add(corner);
            }
        }

        for (int i = 0; i < deanCorners.Count; i++)
        {
            Vector3 spawnPos = GetCornerPosition(deanCorners[i]);
            spawnPos.y += _deanHeightOffset;
            
            GameObject dean = Instantiate(_deanPrefab, spawnPos, Quaternion.identity);
            dean.name = $"Dean_{i + 1}";
            
            DeanController deanController = dean.GetComponent<DeanController>();
            if (deanController != null)
            {
                Transform[] patrolPoints = GeneratePatrolPoints(deanCorners[i]);
                deanController.SetPatrolPoints(patrolPoints);
            }
            
            _deanInstances.Add(dean);
            Debug.Log($"Dean {i + 1} spawned at {deanCorners[i]} corner: {spawnPos}");
        }
    }

    private void SpawnStudents()
    {
        if (_studentPrefab == null)
        {
            Debug.LogWarning("Student prefab not assigned!");
            return;
        }

        int studentCount = 40; // More students
        
        GameObject studentParent = new GameObject("Students");
        
        for (int i = 0; i < studentCount; i++)
        {
            // Random position anywhere in the maze (avoiding edges)
            int x = Random.Range(1, _mazeWidth - 1);
            int z = Random.Range(1, _mazeDepth - 1);
            
            // Add some randomness within the cell
            float offsetX = Random.Range(0f, _cellSize);
            float offsetZ = Random.Range(0f, _cellSize);
            
            Vector3 spawnPos = new Vector3(
                (x * _cellSize) + offsetX,
                _spawnY + _studentHeightOffset,
                (z * _cellSize) + offsetZ
            );
            
            GameObject student = Instantiate(_studentPrefab, spawnPos, Quaternion.identity, studentParent.transform);
            student.name = $"Student_{i + 1}";
            
            // Random rotation
            student.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            
            // Make sure StudentController is attached
            StudentController controller = student.GetComponent<StudentController>();
            if (controller == null)
            {
                controller = student.AddComponent<StudentController>();
            }
            
            _studentInstances.Add(student);
        }
        
        Debug.Log($"Spawned {studentCount} students throughout the maze");
    }

    private Transform[] GeneratePatrolPoints(Corner startCorner)
    {
        List<Transform> points = new List<Transform>();
        
        GameObject patrolParent = new GameObject($"PatrolPoints_{startCorner}");
        
        // Generate patrol points spread across the maze
        int numPoints = 8;
        
        for (int i = 0; i < numPoints; i++)
        {
            // Random position anywhere in the maze (avoiding edges)
            int x = Random.Range(2, _mazeWidth - 2);
            int z = Random.Range(2, _mazeDepth - 2);
            
            GameObject point = new GameObject($"PatrolPoint_{i}");
            point.transform.position = new Vector3(x * _cellSize, _spawnY, z * _cellSize);
            point.transform.SetParent(patrolParent.transform);
            points.Add(point.transform);
        }
        
        return points.ToArray();
    }

    private Vector2Int GetCornerCell(Corner corner)
    {
        switch (corner)
        {
            case Corner.BottomLeft:
                return new Vector2Int(2, 2);
            case Corner.BottomRight:
                return new Vector2Int(_mazeWidth - 3, 2);
            case Corner.TopLeft:
                return new Vector2Int(2, _mazeDepth - 3);
            case Corner.TopRight:
                return new Vector2Int(_mazeWidth - 3, _mazeDepth - 3);
            default:
                return new Vector2Int(2, 2);
        }
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
        int bathroomsPlaced = 0;
        int minDistanceBetweenRooms = 4;

        List<Vector2Int> allValidCells = new List<Vector2Int>();
        for (int x = 1; x < _mazeWidth - 1; x++)
        {
            for (int z = 1; z < _mazeDepth - 1; z++)
            {
                int distanceFromStart = Mathf.Abs(x - startCell.x) + Mathf.Abs(z - startCell.y);
                if (distanceFromStart >= 3)
                {
                    allValidCells.Add(new Vector2Int(x, z));
                }
            }
        }

        // Shuffle cells
        for (int i = allValidCells.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Vector2Int temp = allValidCells[i];
            allValidCells[i] = allValidCells[j];
            allValidCells[j] = temp;
        }

        // Combined list to check distance against both rooms and bathrooms
        List<Vector2Int> allPlacedRooms = new List<Vector2Int>();

        // Place teacher rooms first
        foreach (Vector2Int candidate in allValidCells)
        {
            if (roomsPlaced >= _numberOfRooms)
                break;

            bool tooClose = false;
            foreach (Vector2Int roomPos in allPlacedRooms)
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
                allPlacedRooms.Add(candidate);
                roomsPlaced++;
                Debug.Log($"Room {roomsPlaced} placed at ({candidate.x}, {candidate.y})");
            }
        }

        // Place bathrooms
        foreach (Vector2Int candidate in allValidCells)
        {
            if (bathroomsPlaced >= _numberOfBathrooms)
                break;

            // Skip if already a room
            if (allPlacedRooms.Contains(candidate))
                continue;

            bool tooClose = false;
            foreach (Vector2Int roomPos in allPlacedRooms)
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
                ReplaceWithBathroom(candidate.x, candidate.y);
                _bathroomPositions.Add(candidate);
                allPlacedRooms.Add(candidate);
                bathroomsPlaced++;
                Debug.Log($"Bathroom {bathroomsPlaced} placed at ({candidate.x}, {candidate.y})");
            }
        }

        if (roomsPlaced < _numberOfRooms)
        {
            Debug.LogWarning($"Could only place {roomsPlaced} rooms out of {_numberOfRooms} requested.");
        }
        
        if (bathroomsPlaced < _numberOfBathrooms)
        {
            Debug.LogWarning($"Could only place {bathroomsPlaced} bathrooms out of {_numberOfBathrooms} requested.");
        }
    }

    private void ReplaceWithRoom(int x, int z)
    {
        bool hasLeftWall = _mazeGrid[x, z].HasLeftWall();
        bool hasRightWall = _mazeGrid[x, z].HasRightWall();
        bool hasFrontWall = _mazeGrid[x, z].HasFrontWall();
        bool hasBackWall = _mazeGrid[x, z].HasBackWall();

        Destroy(_mazeGrid[x, z].gameObject);

        Vector3 position = new Vector3(x * _cellSize, 0, z * _cellSize);
        MazeCell roomCell = Instantiate(_roomPrefab, position, Quaternion.identity, transform);
        roomCell.Visit();
        roomCell.SetAsRoom();
        _mazeGrid[x, z] = roomCell;

        if (!hasLeftWall)
            roomCell.ClearLeftWall();
        if (!hasRightWall)
            roomCell.ClearRightWall();
        if (!hasFrontWall)
            roomCell.ClearFrontWall();
        if (!hasBackWall)
            roomCell.ClearBackWall();
    }

    private void ReplaceWithBathroom(int x, int z)
    {
        bool hasLeftWall = _mazeGrid[x, z].HasLeftWall();
        bool hasRightWall = _mazeGrid[x, z].HasRightWall();
        bool hasFrontWall = _mazeGrid[x, z].HasFrontWall();
        bool hasBackWall = _mazeGrid[x, z].HasBackWall();

        Destroy(_mazeGrid[x, z].gameObject);

        Vector3 position = new Vector3(x * _cellSize, 0, z * _cellSize);
        MazeCell bathroomCell = Instantiate(_bathroomPrefab, position, Quaternion.identity, transform);
        bathroomCell.Visit();
        bathroomCell.SetAsRoom();
        bathroomCell.SetAsBathroom();
        _mazeGrid[x, z] = bathroomCell;

        if (!hasLeftWall)
            bathroomCell.ClearLeftWall();
        if (!hasRightWall)
            bathroomCell.ClearRightWall();
        if (!hasFrontWall)
            bathroomCell.ClearFrontWall();
        if (!hasBackWall)
            bathroomCell.ClearBackWall();
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
        MazeCell currentCell = _mazeGrid[current.x, current.y];
        MazeCell neighborCell = _mazeGrid[neighbor.x, neighbor.y];

        if (neighbor.x > current.x)
        {
            currentCell.ClearRightWall();
            neighborCell.ClearLeftWall();
        }
        else if (neighbor.x < current.x)
        {
            currentCell.ClearLeftWall();
            neighborCell.ClearRightWall();
        }
        else if (neighbor.y > current.y)
        {
            currentCell.ClearFrontWall();
            neighborCell.ClearBackWall();
        }
        else if (neighbor.y < current.y)
        {
            currentCell.ClearBackWall();
            neighborCell.ClearFrontWall();
        }
    }

    public MazeCell GetMazeCell(int x, int z)
    {
        if (x >= 0 && x < _mazeWidth && z >= 0 && z < _mazeDepth)
            return _mazeGrid[x, z];
        return null;
    }

    public Vector3 GetCellWorldPosition(int x, int z)
    {
        return new Vector3(x * _cellSize, _spawnY + _deanHeightOffset, z * _cellSize);
    }

    public Vector2Int GetCellFromWorldPosition(Vector3 worldPos)
    {
        int x = Mathf.RoundToInt(worldPos.x / _cellSize);
        int z = Mathf.RoundToInt(worldPos.z / _cellSize);
        return new Vector2Int(x, z);
    }

    public bool IsValidCell(int x, int z)
    {
        return x >= 0 && x < _mazeWidth && z >= 0 && z < _mazeDepth;
    }

    public bool IsCellRoom(int x, int z)
    {
        MazeCell cell = GetMazeCell(x, z);
        return cell != null && cell.IsRoom;
    }

    public bool IsCellBathroom(int x, int z)
    {
        MazeCell cell = GetMazeCell(x, z);
        return cell != null && cell.IsBathroom;
    }

    public bool IsCellSafeSpace(int x, int z)
    {
        MazeCell cell = GetMazeCell(x, z);
        return cell != null && (cell.IsRoom || cell.IsBathroom);
    }

    public GameObject GetPlayer()
    {
        return _playerInstance;
    }

    public List<GameObject> GetDeans()
    {
        return _deanInstances;
    }

    public GameObject GetDean(int index)
    {
        if (index >= 0 && index < _deanInstances.Count)
        {
            return _deanInstances[index];
        }
        return null;
    }

    public List<GameObject> GetTeachers()
    {
        return _teacherInstances;
    }

    public GameObject GetTeacher(int index)
    {
        if (index >= 0 && index < _teacherInstances.Count)
        {
            return _teacherInstances[index];
        }
        return null;
    }

    public List<GameObject> GetStudents()
    {
        return _studentInstances;
    }

    public GameObject GetStudent(int index)
    {
        if (index >= 0 && index < _studentInstances.Count)
        {
            return _studentInstances[index];
        }
        return null;
    }

    public Vector3 GetRandomSpawnPosition()
    {
        // Pick a random corner different from where deans might be
        Corner[] corners = { Corner.BottomLeft, Corner.BottomRight, Corner.TopLeft, Corner.TopRight };
        Corner randomCorner = corners[Random.Range(0, corners.Length)];
        
        Vector3 pos = GetCornerPosition(randomCorner);
        return pos;
    }
}
