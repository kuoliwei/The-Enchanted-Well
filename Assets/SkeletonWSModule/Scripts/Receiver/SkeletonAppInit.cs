using UnityEngine;

public class SkeletonAppInit : MonoBehaviour
{
    public PoseDataReceiver receiver;
    public SkeletonVisualizer visualizer;

    void Start()
    {
        receiver.OnSkeletonFrame += visualizer.UpdateSkeletons;
    }
}
