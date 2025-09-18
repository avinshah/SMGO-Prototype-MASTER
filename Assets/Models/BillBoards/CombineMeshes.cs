using UnityEngine;

public class MeshCombiner : MonoBehaviour
{
    [Header("Combine Settings")]
    public GameObject[] objectsToCombine;
    public bool createNewGameObject = true;
    public string combinedMeshName = "CombinedMesh";

    [ContextMenu("Combine Meshes")]
    public void CombineMeshes()
    {
        // Get all mesh filters
        MeshFilter[] meshFilters = new MeshFilter[objectsToCombine.Length];
        for (int i = 0; i < objectsToCombine.Length; i++)
        {
            meshFilters[i] = objectsToCombine[i].GetComponent<MeshFilter>();
        }

        // Create combine instances
        CombineInstance[] combine = new CombineInstance[meshFilters.Length];

        for (int i = 0; i < meshFilters.Length; i++)
        {
            combine[i].mesh = meshFilters[i].sharedMesh;
            combine[i].transform = meshFilters[i].transform.localToWorldMatrix;
        }

        // Create new mesh
        Mesh combinedMesh = new Mesh();
        combinedMesh.CombineMeshes(combine);

        if (createNewGameObject)
        {
            // Create new GameObject with combined mesh
            GameObject combinedObject = new GameObject(combinedMeshName);
            combinedObject.AddComponent<MeshFilter>().mesh = combinedMesh;
            combinedObject.AddComponent<MeshRenderer>().material = objectsToCombine[0].GetComponent<MeshRenderer>().material;

            // Disable original objects
            foreach (GameObject obj in objectsToCombine)
            {
                obj.SetActive(false);
            }
        }
    }
}