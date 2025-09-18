using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEngine;

/// <summary>
/// Relay for TouchHandGrabInteractable that polls Interactable.State each frame
/// and forwards "selected" transitions to MetaGrabRelay + MetaGrabRelayFeedback.
/// Put this on the SAME object that has TouchHandGrabInteractable.
/// </summary>
[RequireComponent(typeof(TouchHandGrabInteractable))]
public class TouchHandGrabSelectRelayPolling : MonoBehaviour
{
    [Header("Targets on the cube ROOT (auto-found in parents if left empty)")]
    public MetaGrabRelay RelayTarget;            // expects OnSelectedInteractor(GameObject)
    public MetaGrabRelayFeedback FeedbackTarget; // expects OnRelayTriggered(GameObject)

    [Header("Which transitions to forward")]
    public bool FireOnSelectStart = true;        // when entering Select
    public bool FireWhileHeld = false;       // re-fire every frame while in Select
    public bool FireOnSelectEnd = false;       // when leaving Select

    private TouchHandGrabInteractable _interactable;
    private InteractableState _prevState;

    private void Reset()
    {
        _interactable = GetComponent<TouchHandGrabInteractable>();
        if (!RelayTarget) RelayTarget = GetComponentInParent<MetaGrabRelay>();
        if (!FeedbackTarget) FeedbackTarget = GetComponentInParent<MetaGrabRelayFeedback>();
    }

    private void Awake()
    {
        _interactable = GetComponent<TouchHandGrabInteractable>();
        if (!RelayTarget) RelayTarget = GetComponentInParent<MetaGrabRelay>();
        if (!FeedbackTarget) FeedbackTarget = GetComponentInParent<MetaGrabRelayFeedback>();
        _prevState = _interactable.State;
    }

    private void Update()
    {
        var state = _interactable.State; // Oculus.Interaction.InteractableState

        bool enteredSelect = (_prevState != InteractableState.Select) && (state == InteractableState.Select);
        bool exitedSelect = (_prevState == InteractableState.Select) && (state != InteractableState.Select);
        bool isHeld = (state == InteractableState.Select);

        if (enteredSelect && FireOnSelectStart)
            Forward(interactorGO: gameObject, tag: "SelectStart"); // pass self for feedback

        if (isHeld && FireWhileHeld)
            Forward(interactorGO: gameObject, tag: "WhileHeld");

        if (exitedSelect && FireOnSelectEnd)
            Forward(interactorGO: gameObject, tag: "SelectEnd");

        _prevState = state;
    }

    private void Forward(GameObject interactorGO, string tag)
    {
        // 1) Visible feedback so you can confirm in-headset
        if (FeedbackTarget)
            FeedbackTarget.OnRelayTriggered(interactorGO);
        else
            Debug.LogWarning($"[TouchHandGrabSelectRelayPolling] No MetaGrabRelayFeedback on parent/root ({tag}).");

        // 2) Network/scoring relay (will try to resolve player via PlayerLink; fine if it can't yet)
        if (RelayTarget)
            RelayTarget.OnSelectedInteractor(interactorGO);
        else
            Debug.LogWarning($"[TouchHandGrabSelectRelayPolling] No MetaGrabRelay on parent/root ({tag}).");
    }
}
