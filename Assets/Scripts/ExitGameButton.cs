using UnityEngine;

public class ExitGameButton : MonoBehaviour
{
    public void ExitGame()
    {
#if UNITY_EDITOR
        // Editor 模式下停止播放
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // Build 後真正關閉應用程式
        Application.Quit();
#endif
    }
}
