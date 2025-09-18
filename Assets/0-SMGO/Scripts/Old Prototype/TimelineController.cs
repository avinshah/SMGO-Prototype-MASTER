using UnityEngine;
using UnityEngine.Playables; // Required for Timeline control

public class TimelineController : MonoBehaviour
{
    public PlayableDirector playableDirector;

    void Start()
    {
        // Ensure the PlayableDirector is assigned either through the editor or dynamically
        if (playableDirector == null)
        {
            playableDirector = GetComponent<PlayableDirector>();
        }
    }

    // Play the timeline from the current position
    public void PlayTimeline()
    {
        playableDirector.Play();
    }

    // Pause the timeline
    public void PauseTimeline()
    {
        playableDirector.Pause();
    }

    // Rewind the timeline to the start
    public void RewindTimeline()
    {
        playableDirector.time = 0;
        playableDirector.Evaluate(); // Update the timeline instantly to reflect the rewound state
        playableDirector.Pause();    // Keep it paused at the beginning
    }

    // Scrub timeline to a specific time (useful for VR or user-controlled rewinds)
    public void SetTimelineTime(double time)
    {
        playableDirector.time = time;
        playableDirector.Evaluate(); // Update the timeline instantly
    }

    // Fast forward the timeline by a given amount of seconds
    public void FastForwardTimeline(float seconds)
    {
        playableDirector.time += seconds;
        playableDirector.Evaluate(); // Update to reflect the new position
    }
}
