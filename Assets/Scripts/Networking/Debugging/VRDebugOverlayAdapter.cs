using UnityEngine;

public class VRDebugOverlayAdapter : MonoBehaviour
{
    public TMPro.TextMeshProUGUI text; // or your overlay API
    public void SetText(string s) { if (text) text.text = s; }
}
