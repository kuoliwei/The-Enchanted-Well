using UnityEngine;

public class FullDisplayActivator : MonoBehaviour
{
    void Start()
    {
        // 啟動所有可用顯示器
        for (int i = 0; i < Display.displays.Length; i++)
        {
            Debug.Log($"Activating Display {i + 1}");
            Display.displays[i].Activate();
        }
    }
}
