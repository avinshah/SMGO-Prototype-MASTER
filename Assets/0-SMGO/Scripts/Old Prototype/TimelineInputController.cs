using UnityEngine;

public class TimelineInputController : MonoBehaviour
{
    public TimelineController timelineController;

    // Define public keycodes for testing
    public KeyCode playPauseKey = KeyCode.Space; // Toggle play/pause
    public KeyCode rewindKey = KeyCode.LeftArrow; // Rewind when held
    public KeyCode forwardKey = KeyCode.RightArrow; // Fast forward when held

    private bool isPlaying = false; // Track play/pause state
    private bool isStarted = false; // Track if the timeline has started

    void Update()
    {
        // Start or toggle play/pause with the defined key
        if (Input.GetKeyDown(playPauseKey))
        {
            if (!isStarted)
            {
                // Start the timeline for the first time
                timelineController.PlayTimeline();
                isStarted = true;
                isPlaying = true;
            }
            else if (isPlaying)
            {
                // Pause if already playing
                timelineController.PauseTimeline();
                isPlaying = false;
            }
            else
            {
                // Resume playing if paused
                timelineController.PlayTimeline();
                isPlaying = true;
            }
        }

        // Handle rewind and fast forward only if paused and the timeline has started
        if (isStarted && !isPlaying)
        {
            if (Input.GetKey(rewindKey))
            {
                timelineController.SetTimelineTime(timelineController.playableDirector.time - Time.deltaTime); // Rewind smoothly
            }
            else if (Input.GetKey(forwardKey))
            {
                timelineController.SetTimelineTime(timelineController.playableDirector.time + Time.deltaTime); // Forward smoothly
            }
        }
    }
}
