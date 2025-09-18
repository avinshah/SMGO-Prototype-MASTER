using UnityEngine;
using UnityEngine.Events;

public class TouchDeactivator : MonoBehaviour
{
    [Header("Trigger Settings")]
    [Tooltip("What can trigger this? Leave empty for anything")]
    public string[] allowedTags = { "Player" };

    [Tooltip("Use OnTriggerEnter instead of OnCollisionEnter")]
    public bool useTrigger = true;

    [Tooltip("Only trigger once, then ignore further touches")]
    public bool triggerOnce = true;

    [Header("Self Behavior")]
    [Tooltip("Deactivate THIS object when touched")]
    public bool deactivateSelfOnTouch = true;

    [Tooltip("Destroy THIS object instead of deactivating")]
    public bool destroySelfInsteadOfDeactivate = false;

    [Tooltip("Delay before deactivating self (seconds)")]
    public float selfDeactivateDelay = 0f;

    [Header("Events")]
    [Tooltip("Called when object is touched")]
    public UnityEvent OnTouch;

    [Tooltip("Called before this object deactivates itself")]
    public UnityEvent OnBeforeDeactivate;

    [Header("Quick Object Controls")]
    [Tooltip("Objects to ACTIVATE when touched")]
    public GameObject[] objectsToActivate;

    [Tooltip("Objects to DEACTIVATE when touched")]
    public GameObject[] objectsToDeactivate;

    [Header("Choice System")]
    [Tooltip("Other TouchDeactivators to disable when this is touched (for A or B choices)")]
    public TouchDeactivator[] otherChoicesToDisable;

    [Header("Debug")]
    [SerializeField] private bool hasBeenTriggered = false;
    [SerializeField] private bool isWaitingToDeactivate = false;

    private void Start()
    {
        // Ensure we have a collider
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogWarning($"TouchDeactivator on {gameObject.name} needs a Collider component!");
        }
        else if (useTrigger && !col.isTrigger)
        {
            Debug.LogWarning($"TouchDeactivator on {gameObject.name} is set to use triggers but collider is not a trigger!");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (useTrigger)
        {
            HandleTouch(other.gameObject);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!useTrigger)
        {
            HandleTouch(collision.gameObject);
        }
    }

    private void HandleTouch(GameObject toucher)
    {
        // Check if already triggered and set to trigger once
        if (triggerOnce && hasBeenTriggered) return;

        // Check if the toucher is allowed
        if (!IsToucherAllowed(toucher)) return;

        // Mark as triggered
        hasBeenTriggered = true;

        // Execute touch actions
        ExecuteTouchActions(toucher);

        // Handle self deactivation
        if (deactivateSelfOnTouch)
        {
            if (selfDeactivateDelay > 0f)
            {
                StartCoroutine(DelayedSelfDeactivate());
            }
            else
            {
                DeactivateSelf();
            }
        }
    }

    private bool IsToucherAllowed(GameObject toucher)
    {
        // If no tags specified, allow anything
        if (allowedTags == null || allowedTags.Length == 0) return true;

        // Check if toucher has any of the allowed tags
        foreach (string allowedTag in allowedTags)
        {
            if (toucher.CompareTag(allowedTag))
            {
                return true;
            }
        }

        return false;
    }

    private void ExecuteTouchActions(GameObject toucher)
    {
        // Trigger touch event
        OnTouch?.Invoke();

        // Handle quick object controls
        ActivateObjects();
        DeactivateObjects();

        // Disable other choices (for choice systems)
        DisableOtherChoices();

        // Optional: Pass toucher info to events
        Debug.Log($"{gameObject.name} was touched by {toucher.name}");
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

    private void DisableOtherChoices()
    {
        foreach (TouchDeactivator otherChoice in otherChoicesToDisable)
        {
            if (otherChoice != null && otherChoice != this)
            {
                otherChoice.DisableChoice();
            }
        }
    }

    private System.Collections.IEnumerator DelayedSelfDeactivate()
    {
        isWaitingToDeactivate = true;
        yield return new WaitForSeconds(selfDeactivateDelay);
        DeactivateSelf();
    }

    private void DeactivateSelf()
    {
        // Trigger before deactivate event
        OnBeforeDeactivate?.Invoke();

        if (destroySelfInsteadOfDeactivate)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Manually trigger this choice (useful for UI buttons etc)
    /// </summary>
    public void TriggerChoice()
    {
        if (triggerOnce && hasBeenTriggered) return;

        HandleTouch(gameObject); // Use self as toucher
    }

    /// <summary>
    /// Disable this choice (called by other choices)
    /// </summary>
    public void DisableChoice()
    {
        hasBeenTriggered = true; // Prevent further triggering

        if (deactivateSelfOnTouch)
        {
            DeactivateSelf();
        }
    }

    /// <summary>
    /// Reset this choice to be triggerable again
    /// </summary>
    public void ResetChoice()
    {
        hasBeenTriggered = false;
        isWaitingToDeactivate = false;
    }

    /// <summary>
    /// Check if this choice has been triggered
    /// </summary>
    public bool HasBeenTriggered()
    {
        return hasBeenTriggered;
    }

    /// <summary>
    /// Enable/disable this TouchDeactivator
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        this.enabled = enabled;
    }

    // TouchDeactivator.cs
    // Add near the bottom
    public void RunAsIfTouched()
    {
        // Re-use the same logic as a real touch
        ExecuteTouchActions(gameObject);
        if (deactivateSelfOnTouch)
        {
            if (selfDeactivateDelay > 0f) StartCoroutine(DelayedSelfDeactivate());
            else DeactivateSelf();
        }
    }

}