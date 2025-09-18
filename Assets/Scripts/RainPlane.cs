using UnityEngine;

public class RainPlane : MonoBehaviour
{
    public float scrollSpeed = 5f;
    Material rainMat;

    void Start()
    {
        rainMat = GetComponent<Renderer>().material;
    }

    void Update()
    {
        Vector2 offset = rainMat.mainTextureOffset;
        offset.y -= scrollSpeed * Time.deltaTime;
        rainMat.mainTextureOffset = offset;
    }
}