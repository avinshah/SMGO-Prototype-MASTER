using UnityEngine;

public class IEnumeratorSanityCheck : MonoBehaviour
{
    private void Start()
    {
        StartCoroutine(Flash());
    }

    // Note the global:: prefix — this ignores any user-defined System types.
    private global::System.Collections.IEnumerator Flash()
    {
        yield return null;
    }
}
