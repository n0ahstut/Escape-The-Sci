using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Basketball : MonoBehaviour, PlayerController.IInteractable
{
    [Header("Shot Meter Settings")]
    [SerializeField] private float meterSpeed = 2f;
    [SerializeField] private float greenZoneStart = 0.4f;
    [SerializeField] private float greenZoneEnd = 0.6f;
    
    [Header("Shot Animation")]
    [SerializeField] private float shotDuration = 1f;
    [SerializeField] private float arcHeight = 3f;
    
    [Header("References")]
    [SerializeField] private Transform hoopTarget;
    [SerializeField] private TeacherController gymTeacher;
    
    [Header("Audio")]
    [SerializeField] private AudioClip scoreSound;
    [SerializeField] private AudioClip missSound;
    
    [Header("Shot Meter UI")]
    [SerializeField] private GameObject shotMeterPrefab;
    
    private AudioSource audioSource;
    private bool isShooting = false;
    private bool hasScored = false;
    private float meterValue = 0f;
    private bool meterGoingUp = true;
    private Vector3 startPosition;
    private Coroutine meterCoroutine;
    
    // UI Elements
    private GameObject shotMeterUI;
    private RectTransform meterBackground;
    private RectTransform meterPointer;
    private RectTransform greenZone;
    private PlayerController playerController;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        startPosition = transform.position;
    }

    private void Start()
    {
        // Find Mr. Dahlem if not assigned
        if (gymTeacher == null)
        {
            TeacherController[] teachers = FindObjectsOfType<TeacherController>();
            foreach (TeacherController teacher in teachers)
            {
                if (teacher.Type == TeacherType.MrDahlem)
                {
                    gymTeacher = teacher;
                    break;
                }
            }
        }
        
        CreateShotMeterUI();
    }

    private Transform FindHoop()
    {
        // Try finding by HoopTrigger tag first
        GameObject hoopTrigger = GameObject.FindGameObjectWithTag("HoopTrigger");
        if (hoopTrigger != null)
        {
            Debug.Log($"Found HoopTrigger at world position: {hoopTrigger.transform.position}");
            return hoopTrigger.transform;
        }
        
        // Try finding by Hoop tag
        GameObject hoop = GameObject.FindGameObjectWithTag("Hoop");
        if (hoop != null)
        {
            Debug.Log($"Found Hoop at world position: {hoop.transform.position}");
            return hoop.transform;
        }
        
        Debug.LogWarning("No hoop found!");
        return null;
    }

    private void CreateShotMeterUI()
    {
        // Create canvas if needed
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }
        
        // Create shot meter container
        shotMeterUI = new GameObject("ShotMeterUI");
        shotMeterUI.transform.SetParent(canvas.transform);
        RectTransform containerRect = shotMeterUI.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0f);
        containerRect.anchorMax = new Vector2(0.5f, 0f);
        containerRect.pivot = new Vector2(0.5f, 0f);
        containerRect.anchoredPosition = new Vector2(0, 50);
        containerRect.sizeDelta = new Vector2(400, 40);
        
        // Create background bar
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(shotMeterUI.transform);
        meterBackground = bgObj.AddComponent<RectTransform>();
        meterBackground.anchorMin = Vector2.zero;
        meterBackground.anchorMax = Vector2.one;
        meterBackground.offsetMin = Vector2.zero;
        meterBackground.offsetMax = Vector2.zero;
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.3f, 0.3f, 0.3f, 0.9f);
        
        // Create red zones (left)
        GameObject redLeftObj = new GameObject("RedZoneLeft");
        redLeftObj.transform.SetParent(shotMeterUI.transform);
        RectTransform redLeftRect = redLeftObj.AddComponent<RectTransform>();
        redLeftRect.anchorMin = new Vector2(0, 0);
        redLeftRect.anchorMax = new Vector2(greenZoneStart, 1);
        redLeftRect.offsetMin = Vector2.zero;
        redLeftRect.offsetMax = Vector2.zero;
        Image redLeftImage = redLeftObj.AddComponent<Image>();
        redLeftImage.color = new Color(0.8f, 0.2f, 0.2f, 0.9f);
        
        // Create green zone
        GameObject greenObj = new GameObject("GreenZone");
        greenObj.transform.SetParent(shotMeterUI.transform);
        greenZone = greenObj.AddComponent<RectTransform>();
        greenZone.anchorMin = new Vector2(greenZoneStart, 0);
        greenZone.anchorMax = new Vector2(greenZoneEnd, 1);
        greenZone.offsetMin = Vector2.zero;
        greenZone.offsetMax = Vector2.zero;
        Image greenImage = greenObj.AddComponent<Image>();
        greenImage.color = new Color(0.2f, 0.8f, 0.2f, 0.9f);
        
        // Create red zone (right)
        GameObject redRightObj = new GameObject("RedZoneRight");
        redRightObj.transform.SetParent(shotMeterUI.transform);
        RectTransform redRightRect = redRightObj.AddComponent<RectTransform>();
        redRightRect.anchorMin = new Vector2(greenZoneEnd, 0);
        redRightRect.anchorMax = new Vector2(1, 1);
        redRightRect.offsetMin = Vector2.zero;
        redRightRect.offsetMax = Vector2.zero;
        Image redRightImage = redRightObj.AddComponent<Image>();
        redRightImage.color = new Color(0.8f, 0.2f, 0.2f, 0.9f);
        
        // Create pointer
        GameObject pointerObj = new GameObject("Pointer");
        pointerObj.transform.SetParent(shotMeterUI.transform);
        meterPointer = pointerObj.AddComponent<RectTransform>();
        meterPointer.anchorMin = new Vector2(0, 0);
        meterPointer.anchorMax = new Vector2(0, 1);
        meterPointer.pivot = new Vector2(0.5f, 0.5f);
        meterPointer.sizeDelta = new Vector2(6, 0);
        meterPointer.anchoredPosition = new Vector2(0, 0);
        Image pointerImage = pointerObj.AddComponent<Image>();
        pointerImage.color = Color.white;
        
        // Hide by default
        shotMeterUI.SetActive(false);
    }

    private void Update()
    {
        // Check for second E press while shooting
        if (isShooting && Input.GetKeyDown(KeyCode.E))
        {
            TakeShot();
        }
    }

    public void Interact(PlayerController player)
    {
        if (hasScored) return;
        
        playerController = player;
        
        if (!isShooting)
        {
            StartShotMeter();
        }
    }

    private void StartShotMeter()
    {
        isShooting = true;
        meterValue = 0f;
        meterGoingUp = true;
        
        shotMeterUI.SetActive(true);
        
        // Unlock cursor so player can see the meter
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        if (playerController != null)
        {
            playerController.SetMovementEnabled(false);
        }
        
        meterCoroutine = StartCoroutine(UpdateMeter());
    }

    private IEnumerator UpdateMeter()
    {
        while (isShooting)
        {
            // Move meter back and forth
            if (meterGoingUp)
            {
                meterValue += meterSpeed * Time.deltaTime;
                if (meterValue >= 1f)
                {
                    meterValue = 1f;
                    meterGoingUp = false;
                }
            }
            else
            {
                meterValue -= meterSpeed * Time.deltaTime;
                if (meterValue <= 0f)
                {
                    meterValue = 0f;
                    meterGoingUp = true;
                }
            }
            
            // Update pointer position
            if (meterPointer != null)
            {
                float meterWidth = meterBackground.rect.width;
                meterPointer.anchoredPosition = new Vector2(meterValue * meterWidth, 0);
            }
            
            yield return null;
        }
    }

    private void TakeShot()
    {
        isShooting = false;
        
        // Stop the meter coroutine
        if (meterCoroutine != null)
        {
            StopCoroutine(meterCoroutine);
            meterCoroutine = null;
        }
        
        // Check if in green zone
        if (meterValue >= greenZoneStart && meterValue <= greenZoneEnd)
        {
            // SUCCESS - prevent any more shots
            hasScored = true;
            
            // Hide meter immediately
            shotMeterUI.SetActive(false);
            
            // Lock cursor again
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            
            if (playerController != null)
            {
                playerController.SetMovementEnabled(true);
            }
            
            StartCoroutine(AnimateScore());
        }
        else
        {
            // MISS - keep meter visible, let player try again
            OnMiss();
        }
    }

    private IEnumerator AnimateScore()
    {
        // Find hoop now (at shot time, not at Start)
        Transform hoop = FindHoop();
        
        Vector3 start = transform.position;
        Vector3 end;
        
        if (hoop != null && hoop.position != Vector3.zero)
        {
            end = hoop.position;
            Debug.Log($"Animating ball from {start} to hoop at {end}");
        }
        else
        {
            // No hoop found - animate forward and up from player
            if (playerController != null)
            {
                end = playerController.transform.position + playerController.transform.forward * 5f + Vector3.up * 2f;
            }
            else
            {
                end = start + new Vector3(5f, 2f, 5f);
            }
            Debug.LogWarning($"No valid hoop target! Animating to {end}");
        }
        
        float elapsed = 0f;
        
        // Make sure ball is visible
        gameObject.SetActive(true);
        
        while (elapsed < shotDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / shotDuration;
            
            // Lerp position with arc
            Vector3 currentPos = Vector3.Lerp(start, end, t);
            
            // Add arc (parabola)
            float arc = arcHeight * 4f * t * (1f - t);
            currentPos.y += arc;
            
            transform.position = currentPos;
            
            // Spin the ball
            transform.Rotate(Vector3.right * 360f * Time.deltaTime);
            
            yield return null;
        }
        
        // Ensure ball ends at hoop
        transform.position = end;
        
        OnScore();
    }

    private void OnScore()
    {
        hasScored = true;
        
        if (scoreSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(scoreSound);
        }
        
        Debug.Log("SCORE! Perfect shot!");
        
        if (gymTeacher != null)
        {
            gymTeacher.OnBasketballScore();
        }
    }

    private void OnMiss()
    {
        if (missSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(missSound);
        }
        
        Debug.Log("Missed! Try again.");
        
        // Restart the meter at same speed
        isShooting = true;
        meterValue = 0f;
        meterGoingUp = true;
        meterCoroutine = StartCoroutine(UpdateMeter());
    }

    public void ResetBall()
    {
        hasScored = false;
        isShooting = false;
        meterValue = 0f;
        transform.position = startPosition;
        
        if (shotMeterUI != null)
        {
            shotMeterUI.SetActive(false);
        }
    }

    public bool HasScored => hasScored;
    public bool CanInteract => !hasScored;

    private void OnDestroy()
    {
        if (shotMeterUI != null)
        {
            Destroy(shotMeterUI);
        }
    }
}