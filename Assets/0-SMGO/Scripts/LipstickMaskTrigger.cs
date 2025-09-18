using UnityEngine;

public class LipstickMaskTrigger : MonoBehaviour
{
    [Header("Mask Application Settings")]
    public float requiredTime = 2f; // how long the lipstick must touch the lips
    private float timer = 0f;
    private bool isTouching = false;

    [Header("Depth Clamp Settings")]
    public Transform lipstickTip;    // assign the lipstick tip transform
    public Transform lips;           // assign the lips collider/marker transform
    public float minDistance = 0.01f; // minimum distance to stop it clipping through

    [Header("Events")]
    public UnityEngine.Events.UnityEvent onMaskApplied;

    private void Update()
    {
        // Count while lipstick tip stays in lips collider
        if (isTouching)
        {
            timer += Time.deltaTime;
            if (timer >= requiredTime)
            {
                onMaskApplied.Invoke();
                timer = 0f; // reset if you want multiple applications
            }
        }
        else
        {
            timer = 0f;
        }
    }

    private void LateUpdate()
    {
        // Clamp depth so lipstick doesn’t go "inside" the face
        if (lipstickTip != null && lips != null)
        {
            Vector3 toTip = lipstickTip.position - lips.position;
            float dist = toTip.magnitude;

            if (dist < minDistance && dist > 0.0001f)
            {
                lipstickTip.position = lips.position + toTip.normalized * minDistance;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("LipMask"))
        {
            isTouching = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("LipMask"))
        {
            isTouching = false;
            timer = 0f;
        }
    }
}
