using UnityEngine;

[System.Serializable]
public class Pose
{
    public AnimationClip clip;
    public float freezeTime; // seconds into the clip
}

public class StatuePoseApplier : MonoBehaviour
{
    [Header("Avatar References")]
    public Animator[] avatars;

    [Header("Poses")]
    public Pose[] poses; // assign in Inspector

    private void Start()
    {
        // Apply the first pose (idle) if available
        if (poses.Length > 0)
        {
            ApplyPose(0);
        }
    }

    private void Update()
    {
        // Example: number keys 0,1,2... trigger poses
        for (int i = 0; i < poses.Length; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + i))
            {
                ApplyPose(i);
            }
        }
    }

    public void ApplyPose(int index)
    {
        if (index < 0 || index >= poses.Length) return;

        Pose pose = poses[index];
        if (pose.clip == null) return;

        foreach (Animator avatar in avatars)
        {
            if (avatar != null)
            {
                avatar.Play(pose.clip.name, 0, pose.freezeTime / pose.clip.length);
                avatar.speed = 0f; // freeze at that frame
            }
        }
    }
}
