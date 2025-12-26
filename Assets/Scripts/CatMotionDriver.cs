using UnityEngine;
using PoseSocket;

public class CatMotionDriver : MonoBehaviour
{
    [Header("Data Source")]
    [SerializeField] private PoseDataReceiver poseReceiver;

    [Header("Target Cat Controller")]
    [SerializeField] private CatMotionController catMotion;

    private void Start()
    {
        if (poseReceiver != null)
            poseReceiver.OnSkeletonFrame += OnSkeletonFrame;
        else
            Debug.LogError("[CatMotionDriver] poseReceiver ¥¼³]©w¡I");
    }

    private void OnSkeletonFrame(SkeletonFrame frame)
    {
        if (frame == null)
            return;

        if (frame.skeletonPercent.Count == 0 || frame.angles.Count == 0)
            return;

        float percent = frame.skeletonPercent[0];  // 0 / 50 / 100
        float angle = frame.angles[0];             // e.g. 78

        catMotion.UpdateAngle(angle);
        catMotion.UpdateHeadPosition(percent);
    }
}
