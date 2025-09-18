using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ToggleMirrorsPortals : MonoBehaviour
{
    
    public bool isToggled = false;

    public GameObject mirror1;
    public GameObject mirror2;
    
    public GameObject portal1;
    public GameObject portal2;



    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    public void ToggleMirror()
    {
        isToggled = !isToggled;

        mirror1.SetActive(!isToggled);
        mirror2.SetActive(!isToggled);
        portal1.SetActive(isToggled);
        portal2.SetActive(isToggled);
    }
}
