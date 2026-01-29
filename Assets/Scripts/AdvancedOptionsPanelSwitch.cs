using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AdvancedOptionsPanelSwitch : MonoBehaviour
{
    public GameObject AdvancedOptionsPanel;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void SwitchPanel()
    {
        AdvancedOptionsPanel.SetActive(!AdvancedOptionsPanel.activeSelf);
    }
}
