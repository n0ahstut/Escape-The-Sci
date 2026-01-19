using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game Settings")]
    [SerializeField] private int maxDetentions = 3;
    [SerializeField] private int menuSceneIndex = 0;
    [SerializeField] private int gameSceneIndex = 2;
    [SerializeField] private int detentionSceneIndex = 4;
    [SerializeField] private float detentionDuration = 1f;

    [Header("Bell Settings")]
    [SerializeField] private float bellInterval = 60f;
    
    [Header("UI References")]
    [SerializeField] private GameObject pauseMenuUI;
    [SerializeField] private GameObject gameOverUI;

    [Header("Debug")]
    [SerializeField] private int debugDetentionCount = 0;
    [SerializeField] private bool useDebugDetentions = false;

    public enum GameState
    {
        Playing,
        Paused,
        Detention,
        GameOver
    }

    public enum BellStatus
    {
        Green,
        Yellow,
        Red
    }

    private GameState currentState = GameState.Playing;
    private int currentDetentions = 0;
    private float playTime = 0f;
    private bool isInitialized = false;
    
    private float bellTimer;
    private BellStatus currentBellStatus = BellStatus.Green;
    private int tasksCompleted = 0;
    private int tasksFailed = 0;

    public GameState CurrentState => currentState;
    public int CurrentDetentions => currentDetentions;
    public int MaxDetentions => maxDetentions;
    public float PlayTime => playTime;
    public float BellTimer => bellTimer;
    public float BellInterval => bellInterval;
    public BellStatus CurrentBellStatus => currentBellStatus;
    public int TasksCompleted => tasksCompleted;
    public int TasksFailed => tasksFailed;

    public delegate void GameStateChangedHandler(GameState newState);
    public event GameStateChangedHandler OnGameStateChanged;

    public delegate void DetentionHandler(int currentCount, int maxCount);
    public event DetentionHandler OnDetentionReceived;

    public delegate void BellHandler();
    public event BellHandler OnBellRing;

    public delegate void BellStatusChangedHandler(BellStatus status);
    public event BellStatusChangedHandler OnBellStatusChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.buildIndex == gameSceneIndex && currentState != GameState.Detention)
        {
            ResetForNewGame();
        }
        else if (scene.buildIndex == gameSceneIndex && currentState == GameState.Detention)
        {
            // Coming back from detention - just reset gameplay state, keep detentions
            ResetGameplayState();
        }
    }

    private void ResetForNewGame()
    {
        currentDetentions = 0;
        currentState = GameState.Playing;
        playTime = 0f;
        bellTimer = bellInterval;
        tasksCompleted = 0;
        tasksFailed = 0;
        Time.timeScale = 1f;
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        Debug.Log("Game reset for new playthrough");
    }

    private void ResetGameplayState()
    {
        // Keep currentDetentions!
        playTime = 0f;
        bellTimer = bellInterval;
        Time.timeScale = 1f;
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        Debug.Log($"Returning from detention. Detentions: {currentDetentions}/{maxDetentions}");
    }

    private void Initialize()
    {
        if (isInitialized) return;
        
        if (useDebugDetentions)
        {
            currentDetentions = debugDetentionCount;
        }
        else
        {
            currentDetentions = 0;
        }
        
        bellTimer = bellInterval;
        isInitialized = true;
    }

    private void OnValidate()
    {
        if (useDebugDetentions && Application.isPlaying)
        {
            currentDetentions = debugDetentionCount;
        }
    }

    private void Update()
    {
        if (currentState == GameState.Playing)
        {
            playTime += Time.deltaTime;
            UpdateBellTimer();
        }

        if (Input.GetKeyDown(KeyCode.Escape) && currentState == GameState.Playing)
        {
            PauseGame();
        }
        else if (Input.GetKeyDown(KeyCode.Escape) && currentState == GameState.Paused)
        {
            ResumeGame();
        }
    }

    private void UpdateBellTimer()
    {
        bellTimer -= Time.deltaTime;
        
        BellStatus newStatus;
        if (bellTimer > 30f)
        {
            newStatus = BellStatus.Green;
        }
        else if (bellTimer > 10f)
        {
            newStatus = BellStatus.Yellow;
        }
        else
        {
            newStatus = BellStatus.Red;
        }
        
        if (newStatus != currentBellStatus)
        {
            currentBellStatus = newStatus;
            OnBellStatusChanged?.Invoke(currentBellStatus);
        }
        
        if (bellTimer <= 0f)
        {
            RingBell();
        }
    }

    private void RingBell()
    {
        bellTimer = bellInterval;
        currentBellStatus = BellStatus.Green;
        OnBellRing?.Invoke();
        OnBellStatusChanged?.Invoke(currentBellStatus);
        Debug.Log("Bell rang!");
    }

    public void ResetBellTimer()
    {
        bellTimer = bellInterval;
        currentBellStatus = BellStatus.Green;
        OnBellStatusChanged?.Invoke(currentBellStatus);
    }

    public void CompleteTask()
    {
        tasksCompleted++;
        ResetBellTimer();
        Debug.Log($"Task completed! Total: {tasksCompleted}");
    }

    public void FailTask()
    {
        tasksFailed++;
        Debug.Log($"Task failed! Total: {tasksFailed}");
    }

    public void PauseGame()
    {
        if (currentState != GameState.Playing) return;

        currentState = GameState.Paused;
        Time.timeScale = 0f;
        
        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(true);
        }
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        OnGameStateChanged?.Invoke(currentState);
    }

    public void ResumeGame()
    {
        if (currentState != GameState.Paused) return;

        currentState = GameState.Playing;
        Time.timeScale = 1f;
        
        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(false);
        }
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        OnGameStateChanged?.Invoke(currentState);
    }

    public void GiveDetention()
    {
        if (currentState == GameState.Detention || currentState == GameState.GameOver) return;
        
        currentDetentions++;
        SaveDetentionCount();
        
        OnDetentionReceived?.Invoke(currentDetentions, maxDetentions);
        
        Debug.Log($"Detention received! {currentDetentions}/{maxDetentions}");

        if (currentDetentions >= maxDetentions)
        {
            TriggerGameOver("Too many detentions!");
        }
        else
        {
            StartCoroutine(DetentionRoutine());
        }
    }

    private IEnumerator DetentionRoutine()
    {
        currentState = GameState.Detention;
        OnGameStateChanged?.Invoke(currentState);
        
        SceneManager.LoadScene(detentionSceneIndex);
        
        yield return new WaitForSeconds(detentionDuration);
        
        // Load game scene while still in Detention state
        SceneManager.LoadScene(gameSceneIndex);
        
        // Wait a frame for scene to load before changing state
        yield return null;
        
        currentState = GameState.Playing;
        OnGameStateChanged?.Invoke(currentState);
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void TriggerGameOver(string reason)
    {
        currentState = GameState.GameOver;
        Time.timeScale = 0f;
        
        Debug.Log($"Game Over: {reason}");
        
        if (gameOverUI != null)
        {
            gameOverUI.SetActive(true);
        }
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        OnGameStateChanged?.Invoke(currentState);
    }

    public void TriggerWin()
    {
        Debug.Log("You Win!");
    }

    public void RestartGame()
    {
        ResetDetentions();
        Time.timeScale = 1f;
        currentState = GameState.Playing;
        playTime = 0f;
        bellTimer = bellInterval;
        tasksCompleted = 0;
        tasksFailed = 0;
        SceneManager.LoadScene(gameSceneIndex);
    }

    public void ReturnToMenu()
    {
        ResetDetentions();
        Time.timeScale = 1f;
        currentState = GameState.Playing;
        playTime = 0f;
        bellTimer = bellInterval;
        tasksCompleted = 0;
        tasksFailed = 0;
        SceneManager.LoadScene(menuSceneIndex);
    }

    public void QuitGame()
    {
        SaveDetentionCount();
        
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }

    private void SaveDetentionCount()
    {
        PlayerPrefs.SetInt("DetentionCount", currentDetentions);
        PlayerPrefs.Save();
    }

    public void ResetDetentions()
    {
        currentDetentions = 0;
        PlayerPrefs.SetInt("DetentionCount", 0);
        PlayerPrefs.Save();
    }

    public void LoadDetentionCount()
    {
        currentDetentions = PlayerPrefs.GetInt("DetentionCount", 0);
    }
}
