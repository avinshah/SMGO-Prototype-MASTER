using UnityEngine;

public class ElevatorSpeaker : MonoBehaviour
{
    public AudioSource speaker;       // The speaker AudioSource
    public AudioClip[] announcements; // Array of clips: 0 = first floor, 1 = second, etc.

    // Call this from a button, passing the index of the clip
    public void PlayAnnouncement(int index)
    {
        if (speaker != null && announcements != null && index >= 0 && index < announcements.Length)
        {
            speaker.clip = announcements[index];
            speaker.Play();
        }
        else
        {
            Debug.LogWarning("Invalid index or missing components.");
        }
    }
}
