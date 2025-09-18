using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ToggleLight : MonoBehaviour
{
    //private bool isOn;
    private new Light light;

    // Start is called before the first frame update

  

    void Awake()
    {
        light = GetComponent<Light>();
    }

    private void Start()
    {
        light.enabled = false;
    }

    // Update is called once per frame
   
    
    public void ToggleLightOnOff()
    {
        
            light.enabled = !light.enabled;
        
    }


}
