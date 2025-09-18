using UnityEngine;
using UnityEngine.Events;

public class DeactivateTimer : MonoBehaviour
{
    [Header("Timer Settings")]
    [Tooltip("Time in seconds before timer expires")]
    public float timerDurationSeconds = 5f;

    [Tooltip("Start timer automatically when object becomes active")]
    public bool startOnEnable = true;

    [Header("Legacy Behavior")]
    [Tooltip("Deactivate THIS object when timer expires")]
    public bool deactivateSelfOnExpire = false;

    [Tooltip("Destroy THIS object instead of deactivating")]
    public bool destroySelfInsteadOfDeactivate = false;

    [Header("Events")]
    [Tooltip("Called when timer starts")]
    public UnityEvent OnTimerStarted;

    [Tooltip("Called when timer expires")]
    public UnityEvent OnTimerExpired;

    [Tooltip("Called when timer is manually stopped")]
    public UnityEvent OnTimerStopped;

    [Header("Quick Object Controls")]
    [Tooltip("Objects to ACTIVATE when timer expires")]
    public GameObject[] objectsToActivate;

    [Tooltip("Objects to DEACTIVATE when timer expires")]
    public GameObject[] objectsToDeactivate;

    [Header("Debug Info")]
    [SerializeField] private bool timerRunning = false;
    [SerializeField] private float timeRemaining = 0f;

    private float startTime;
    private bool timerStarted = false;

    void OnEnable()
    {
        if (startOnEnable)
        {
            StartTimer();
        }
    }

    void Start()
    {
        if (startOnEnable && !timerStarted)
        {
            StartTimer();
        }
    }

    void Update()
    {
        if (!timerRunning) return;

        // Calculate time elapsed using real-world time (unaffected by Time.timeScale)
        float elapsed = Time.realtimeSinceStartup - startTime;
        timeRemaining = timerDurationSeconds - elapsed;

        // Check if timer has expired
        if (elapsed >= timerDurationSeconds)
        {
            TimerExpired();
        }
    }

    /// <summary>
    /// Start the timer
    /// </summary>
    public void StartTimer()
    {
        startTime = Time.realtimeSinceStartup;
        timerRunning = true;
        timerStarted = true;
        timeRemaining = timerDurationSeconds;

        // Trigger start event
        OnTimerStarted?.Invoke();
    }

    /// <summary>
    /// Stop the timer without triggering expiration
    /// </summary>
    public void StopTimer()
    {
        if (!timerRunning) return;

        timerRunning = false;
        timeRemaining = 0f;

        // Trigger stop event
        OnTimerStopped?.Invoke();
    }

    /// <summary>
    /// Reset and restart the timer
    /// </summary>
    public void RestartTimer()
    {
        StartTimer();
    }

    /// <summary>
    /// Add time to the current timer
    /// </summary>
    /// <param name="additionalSeconds">Seconds to add</param>
    public void AddTime(float additionalSeconds)
    {
        if (timerRunning)
        {
            timerDurationSeconds += additionalSeconds;
        }
    }

    /// <summary>
    /// Set a new timer duration (affects current timer if running)
    /// </summary>
    /// <param name="newDuration">New duration in seconds</param>
    public void SetTimerDuration(float newDuration)
    {
        timerDurationSeconds = newDuration;
    }

    private void TimerExpired()
    {
        timerRunning = false;

        // Handle quick object controls
        ActivateObjects();
        DeactivateObjects();

        // Trigger expiration event (for custom behaviors)
        OnTimerExpired?.Invoke();

        // Handle legacy self-deactivation
        HandleSelfDeactivation();
    }

    private void ActivateObjects()
    {
        foreach (GameObject obj in objectsToActivate)
        {
            if (obj != null)
            {
                obj.SetActive(true);
            }
        }
    }

    private void DeactivateObjects()
    {
        foreach (GameObject obj in objectsToDeactivate)
        {
            if (obj != null)
            {
                obj.SetActive(false);
            }
        }
    }

    private void HandleSelfDeactivation()
    {
        if (deactivateSelfOnExpire)
        {
            if (destroySelfInsteadOfDeactivate)
            {
                Destroy(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Get remaining time in seconds
    /// </summary>
    /// <returns>Time remaining</returns>
    public float GetTimeRemaining()
    {
        if (!timerRunning) return 0f;

        float elapsed = Time.realtimeSinceStartup - startTime;
        return Mathf.Max(0f, timerDurationSeconds - elapsed);
    }

    /// <summary>
    /// Check if timer is currently running
    /// </summary>
    /// <returns>True if timer is active</returns>
    public bool IsTimerRunning()
    {
        return timerRunning;
    }

    /// <summary>
    /// Get timer progress as percentage (0-1)
    /// </summary>
    /// <returns>Progress from 0 (start) to 1 (complete)</returns>
    public float GetProgress()
    {
        if (!timerRunning) return timerStarted ? 1f : 0f;

        float elapsed = Time.realtimeSinceStartup - startTime;
        return Mathf.Clamp01(elapsed / timerDurationSeconds);
    }
}