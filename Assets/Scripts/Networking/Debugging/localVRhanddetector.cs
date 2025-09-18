using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Detects LOCAL VR hand collisions and reports them to GameRecorder.
/// This works with hands that DON'T have NetworkObject components.
/// Place this on your interactable cubes.
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(NetworkObject))]
public class LocalVRHandReporter : NetworkBehaviour
{
    [Header("MAIN category (goes to GameRecorder)")]
    public Category MainCategory = Category.Art;
    public int MainDelta = 1;

    [Header("SUB category")]
    public string SubCategoryName = "Blocks";
    public int SubCategoryId = 0;
    public int SubDelta = 1;

    [Header("Detection")]
    [Tooltip("Tag or layer name to identify VR hands. Leave empty to accept any collision.")]
    public string handTag = "Hand";  // Set your hand objects to this tag
    public LayerMask handLayers = -1;  // Or use layers
    public float cooldownTime = 0.5f;

    [Header("Visual Feedback")]
    public bool changeColorOnTouch = true;
    public Color touchColor = Color.green;
    public float colorResetTime = 0.5f;

    [Header("Threshold")]
    public int TriggerThreshold = 3;
    public bool FireOnce = true;
    public UnityEvent OnThresholdReached;

    // Tracking
    private float _lastTouchTime;
    [Networked] public int TriggerCount { get; private set; }
    [Networked] public bool ThresholdFired { get; private set; }

    private Renderer _renderer;
    private Color _originalColor;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"{name}: Setting collider to trigger");
            col.isTrigger = true;
        }

        _renderer = GetComponent<Renderer>();
        if (_renderer != null)
            _originalColor = _renderer.material.color;
    }

    private void OnTriggerEnter(Collider other)
    {
        // This runs locally on each client

        // Check if this is a hand (by tag or layer)
        if (!IsHand(other))
            return;

        // Check cooldown (local)
        if (Time.time - _lastTouchTime < cooldownTime)
            return;

        _lastTouchTime = Time.time;

        // Visual feedback (local)
        if (changeColorOnTouch)
            ShowTouchFeedback();

        // Only the local player reports their own touches
        if (Runner != null && Runner.LocalPlayer != PlayerRef.None)
        {
            ReportTouch();
        }
    }

    private bool IsHand(Collider other)
    {
        // Check by tag first
        if (!string.IsNullOrEmpty(handTag) && other.CompareTag(handTag))
            return true;

        // Check by layer
        if ((handLayers.value & (1 << other.gameObject.layer)) != 0)
            return true;

        // If no specific tag/layer set, check common OVR hand names
        string name = other.name.ToLower();
        return name.Contains("hand") || name.Contains("capsule") || name.Contains("bone");
    }

    private void ReportTouch()
    {
        if (GameRecorder.Instance == null)
        {
            Debug.LogWarning("No GameRecorder found");
            return;
        }

        // Build sub-key
        string subKey = !string.IsNullOrEmpty(SubCategoryName)
            ? SubCategoryName
            : $"ID_{SubCategoryId}";

        // Report to GameRecorder (as the local player)
        GameRecorder.Instance.RPC_ReportInteractionEx(
            Runner.LocalPlayer,
            gameObject.name,
            MainCategory,
            MainDelta,
            subKey,
            SubDelta
        );

        // Also increment local counter on authority
        if (Object.HasStateAuthority)
        {
            IncrementTriggerCount();
        }
        else
        {
            RPC_IncrementTriggerCount();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_IncrementTriggerCount()
    {
        IncrementTriggerCount();
    }

    private void IncrementTriggerCount()
    {
        if (!Object.HasStateAuthority)
            return;

        TriggerCount++;

        if (TriggerThreshold > 0 && TriggerCount >= TriggerThreshold && (!FireOnce || !ThresholdFired))
        {
            if (FireOnce)
                ThresholdFired = true;

            OnThresholdReached?.Invoke();

            // Notify all clients to show visual feedback
            RPC_ShowThresholdReached();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowThresholdReached()
    {
        // Visual celebration - pulse the object, change color permanently, etc.
        if (_renderer != null)
        {
            _renderer.material.color = Color.yellow;
        }
    }

    private void ShowTouchFeedback()
    {
        if (_renderer != null && changeColorOnTouch)
        {
            _renderer.material.color = touchColor;
            Invoke(nameof(ResetColor), colorResetTime);
        }
    }

    private void ResetColor()
    {
        if (_renderer != null && !ThresholdFired)
        {
            _renderer.material.color = _originalColor;
        }
    }
}