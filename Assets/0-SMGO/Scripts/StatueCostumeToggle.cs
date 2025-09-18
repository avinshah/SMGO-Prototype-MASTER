using UnityEngine;
using UnityEngine.Events;

public class StatueCostumeToggle : MonoBehaviour
{
    [Header("Renderer References")]
    public Renderer bodyRenderer;
    public Renderer costumeRenderer; // optional

    [Header("Materials")]
    public Material bodyDarkMaterial;
    public Material bodyLitMaterial;

    public Material costumeDarkMaterial; // optional
    public Material costumeLitMaterial;  // optional

    public UnityEvent onRevealed;
    public UnityEvent onHidden;

    private bool revealed = false;

    public KeyCode statueKey;

    //for quick testing without VR camera

    private void Update()
    {
        if (Input.GetKeyDown(statueKey))
        {
            ToggleReveal();
        }
    }


    // Call this from button/UnityEvent
    public void ToggleReveal()
    {
        revealed = !revealed;

        // Handle body material
        if (bodyRenderer != null)
        {
            if (revealed)
            {
                bodyRenderer.material = bodyLitMaterial;
            }
            else
            {
                bodyRenderer.material = bodyDarkMaterial;
            }

            //bodyRenderer.material = revealed ? bodyLitMaterial : bodyDarkMaterial;
        }

        // Handle costume material (optional)
        if (costumeRenderer != null)
        {
            if (revealed)
            {
                costumeRenderer.material = costumeLitMaterial;
            }
            else
            {
                costumeRenderer.material = costumeDarkMaterial;
            }

            //costumeRenderer.material = revealed ? costumeLitMaterial : costumeDarkMaterial;
        }

        // Invoke UnityEvents so external things (like lights) can hook in
        if (revealed)
        {
            onRevealed.Invoke();
        }
        else
        {
            onHidden.Invoke();
        }
    }
}
