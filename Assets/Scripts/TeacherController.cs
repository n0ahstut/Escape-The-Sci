using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum TeacherType
{
    MrDahlem,       // Gym - Instructions
    MsMcGuigan,     // English - Pop quiz
    MrNoody         // History - Name continent
}

public enum TaskState
{
    NotStarted,
    InProgress,
    Completed,
    Failed
}

public class TeacherController : MonoBehaviour, PlayerController.IInteractable
{
    [Header("Teacher Settings")]
    [SerializeField] private TeacherType teacherType;
    [SerializeField] private string teacherName;

    [Header("Task UI")]
    [SerializeField] private GameObject taskUIPanelPrefab;

    private GameObject taskUIPanel;
    private TextMeshProUGUI dialogueText;
    private TextMeshProUGUI questionText;
    private TMP_InputField answerInput;
    private Button submitButton;
    private Button closeButton;

    [Header("Gym Task (Mr. Dahlem)")]
    [SerializeField] private GameObject basketballPrefab;
    [SerializeField] private Transform shootPoint;
    [SerializeField] private Transform hoopTarget;
    [SerializeField] private float shootForce = 10f;

    private TaskState currentTaskState = TaskState.NotStarted;
    private string currentQuestion;
    private string currentAnswer;
    private PlayerController playerController;

    private static readonly Dictionary<TeacherType, string> teacherNames = new Dictionary<TeacherType, string>
    {
        { TeacherType.MrDahlem, "Mr. Dahlem" },
        { TeacherType.MsMcGuigan, "Ms. Guig" },
        { TeacherType.MrNoody, "Mr. Noody" }
    };

    private static readonly Dictionary<TeacherType, string> roomNames = new Dictionary<TeacherType, string>
    {
        { TeacherType.MrDahlem, "Gym" },
        { TeacherType.MsMcGuigan, "English Class" },
        { TeacherType.MrNoody, "History Class" }
    };

    // Math problems
    private static readonly (string question, string answer)[] mathProblems = new[]
    {
        ("What is 15 × 7?", "105"),
        ("What is 144 ÷ 12?", "12"),
        ("What is 23 + 89?", "112"),
        ("What is 256 - 178?", "78"),
        ("What is 13²?", "169"),
        ("What is √81?", "9"),
        ("What is 17 × 6?", "102"),
        ("What is 200 ÷ 8?", "25")
    };

    // English questions
    private static readonly (string question, string answer)[] englishQuestions = new[]
    {
        ("What is the past tense of 'run'?", "ran"),
        ("What is a word that means the same as 'happy'?", "joyful"),
        ("What punctuation ends a question?", "?"),
        ("What is the plural of 'child'?", "children"),
        ("What type of word is 'quickly'?", "adverb"),
        ("What is the opposite of 'ancient'?", "modern"),
        ("Complete: 'Neither here ___ there'", "nor"),
        ("What is a group of lions called?", "pride")
    };

    // History questions
    private static readonly (string question, string answer)[] historyQuestions = new[]
    {
        ("Which continent is Egypt located on?", "africa"),
        ("Which continent is Brazil located on?", "south america"),
        ("Which continent is Japan located on?", "asia"),
        ("Which continent is France located on?", "europe"),
        ("Which continent is Australia located on?", "australia"),
        ("Which continent is Canada located on?", "north america"),
        ("Which continent is the Amazon Rainforest primarily in?", "south america"),
        ("Which continent is the Sahara Desert located on?", "africa")
    };

    public TeacherType Type => teacherType;
    public string TeacherName => string.IsNullOrEmpty(teacherName) ? teacherNames[teacherType] : teacherName;
    public string RoomName => roomNames[teacherType];
    public TaskState CurrentTaskState => currentTaskState;

    public void SetTeacherType(TeacherType type)
    {
        teacherType = type;
        teacherName = teacherNames[type];
    }

    private void Start()
    {
        if (string.IsNullOrEmpty(teacherName))
        {
            teacherName = teacherNames[teacherType];
        }

        SetupUI();
    }

    private void SetupUI()
    {
        if (taskUIPanelPrefab != null && taskUIPanel == null)
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            taskUIPanel = Instantiate(taskUIPanelPrefab, canvas.transform);
            taskUIPanel.SetActive(false);
        }

        if (taskUIPanel != null)
        {
            dialogueText = taskUIPanel.transform.Find("DialogueText")?.GetComponent<TextMeshProUGUI>();
            questionText = taskUIPanel.transform.Find("QuestionText")?.GetComponent<TextMeshProUGUI>();
            answerInput = taskUIPanel.transform.Find("AnswerInput")?.GetComponent<TMP_InputField>();
            submitButton = taskUIPanel.transform.Find("SubmitButton")?.GetComponent<Button>();
            closeButton = taskUIPanel.transform.Find("CloseButton")?.GetComponent<Button>();

            if (submitButton != null)
            {
                submitButton.onClick.AddListener(OnSubmitAnswer);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(CloseTaskUI);
            }
        }
    }

    public void Interact(PlayerController player)
    {
        playerController = player;

        // Mr. Dahlem just gives instructions, no task
        if (teacherType == TeacherType.MrDahlem)
        {
            ShowGameInstructions();
            return;
        }

        switch (currentTaskState)
        {
            case TaskState.NotStarted:
                StartTask();
                break;
            case TaskState.InProgress:
                ShowTaskUI();
                break;
            case TaskState.Completed:
                ShowDialogue($"{teacherName}: Good job! You've already completed this task.");
                break;
            case TaskState.Failed:
                ShowDialogue($"{teacherName}: You failed the task. The deans have been notified.");
                break;
        }
    }

    private void ShowGameInstructions()
    {
        SetupUI();

        if (taskUIPanel != null)
        {
            taskUIPanel.SetActive(true);

            // Resize panel to be bigger
            RectTransform panelRect = taskUIPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.sizeDelta = new Vector2(800, 500);
            }
        }

        if (dialogueText != null)
        {
            dialogueText.text = $"{teacherName}: Let me tell you how things work.";
            dialogueText.fontSize = 18;
        }

        if (questionText != null)
        {
            questionText.text = "HOW TO WIN:\n" +
                "Complete tasks for the other 2 teachers\n" +
                "(Ms. Guig, Mr. Noody)\n\n" +
                "WATCH OUT FOR DEANS:\n" +
                "Deans patrol the hallways\n" +
                "Get caught 3 times = Game Over\n\n" +
                "SAFE ZONES:\n" +
                "Classrooms (blue) and bathrooms (green) are safe\n\n" +
                "Press E to close";
            questionText.fontSize = 20;
        }

        // Hide answer input and submit button for instructions
        if (answerInput != null)
        {
            answerInput.gameObject.SetActive(false);
        }

        if (submitButton != null)
        {
            submitButton.gameObject.SetActive(false);
        }

        // Hide close button - use E to close instead
        if (closeButton != null)
        {
            closeButton.gameObject.SetActive(false);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (playerController != null)
        {
            playerController.SetMovementEnabled(false);
        }

        // Start listening for E to close
        StartCoroutine(WaitForCloseInput());
    }

    private IEnumerator WaitForCloseInput()
    {
        // Wait a frame so the E that opened it doesn't close it
        yield return null;
        yield return null;

        while (taskUIPanel != null && taskUIPanel.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                CloseTaskUI();
                yield break;
            }
            yield return null;
        }
    }

    private void StartTask()
    {
        currentTaskState = TaskState.InProgress;

        switch (teacherType)
        {
            case TeacherType.MrDahlem:
                StartGymTask();
                break;
            case TeacherType.MsMcGuigan:
                StartEnglishTask();
                break;
            case TeacherType.MrNoody:
                StartHistoryTask();
                break;
        }
    }

    private void StartGymTask()
    {
        ShowDialogue($"{teacherName}: Welcome to the gym! Shoot a 3-pointer to pass. Press E near the ball to shoot.");
        // Gym task is handled separately through basketball interaction
    }

    private void StartMathTask()
    {
        var problem = mathProblems[Random.Range(0, mathProblems.Length)];
        currentQuestion = problem.question;
        currentAnswer = problem.answer;

        ShowTaskUI();
        if (dialogueText != null)
        {
            dialogueText.text = $"{teacherName}: Solve this math problem!";
        }
        if (questionText != null)
        {
            questionText.text = currentQuestion;
        }
    }

    private void StartEnglishTask()
    {
        var question = englishQuestions[Random.Range(0, englishQuestions.Length)];
        currentQuestion = question.question;
        currentAnswer = question.answer;

        ShowTaskUI();
        if (dialogueText != null)
        {
            dialogueText.text = $"{teacherName}: Time for a pop quiz!";
        }
        if (questionText != null)
        {
            questionText.text = currentQuestion;
        }
    }

    private void StartHistoryTask()
    {
        var question = historyQuestions[Random.Range(0, historyQuestions.Length)];
        currentQuestion = question.question;
        currentAnswer = question.answer;

        ShowTaskUI();
        if (dialogueText != null)
        {
            dialogueText.text = $"{teacherName}: Let's test your geography knowledge!";
        }
        if (questionText != null)
        {
            questionText.text = currentQuestion;
        }
    }

    private void ShowTaskUI()
    {
        if (taskUIPanel != null)
        {
            taskUIPanel.SetActive(true);

            if (answerInput != null)
            {
                answerInput.text = "";
                answerInput.Select();
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (playerController != null)
            {
                playerController.SetMovementEnabled(false);
            }
        }
    }

    private void CloseTaskUI()
    {
        if (taskUIPanel != null)
        {
            taskUIPanel.SetActive(false);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerController != null)
        {
            playerController.SetMovementEnabled(true);
        }
    }

    private void OnSubmitAnswer()
    {
        if (answerInput == null) return;

        string playerAnswer = answerInput.text.Trim().ToLower();

        if (playerAnswer == currentAnswer.ToLower())
        {
            CompleteTask();
        }
        else
        {
            FailTask();
        }
    }

    public void CompleteTask()
    {
        currentTaskState = TaskState.Completed;
        CloseTaskUI();

        ShowDialogue($"{teacherName}: Excellent work! You may proceed.");

        if (GameManager.Instance != null)
        {
            GameManager.Instance.CompleteTask();
        }

        // Disperse all students
        StudentController.DisperseAllStudents();

        Debug.Log($"Task completed for {teacherName}!");
    }

    public void FailTask()
    {
        currentTaskState = TaskState.Failed;
        CloseTaskUI();

        ShowDialogue($"{teacherName}: That's incorrect. I'm reporting you to the deans.");

        if (GameManager.Instance != null)
        {
            GameManager.Instance.FailTask();
        }

        ReportToDeans();

        Debug.Log($"Task failed for {teacherName}!");
    }

    private void ReportToDeans()
    {
        DeanController.AlertAllDeans();
        Debug.Log($"{teacherName} reported player to the deans!");
    }

    private void ShowDialogue(string message)
    {
        Debug.Log(message);
        // You can expand this to show UI dialogue
    }

    // Called by basketball when it goes through hoop
    public void OnBasketballScore()
    {
        if (teacherType == TeacherType.MrDahlem && currentTaskState == TaskState.InProgress)
        {
            CompleteTask();
        }
    }

    // Called when time runs out
    public void OnTimeExpired()
    {
        if (currentTaskState == TaskState.InProgress)
        {
            FailTask();
        }
    }

    public void ResetTask()
    {
        currentTaskState = TaskState.NotStarted;
        currentQuestion = "";
        currentAnswer = "";
    }

    private void OnDestroy()
    {
        if (submitButton != null)
        {
            submitButton.onClick.RemoveListener(OnSubmitAnswer);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseTaskUI);
        }
    }
}
