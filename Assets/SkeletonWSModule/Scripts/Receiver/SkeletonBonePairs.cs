using PoseSocket;

public static class SkeletonBonePairs
{
    public static readonly (JointId a, JointId b)[] Pairs =
    {
        (JointId.Nose, JointId.LeftEye),
        (JointId.Nose, JointId.RightEye),
        (JointId.LeftEye, JointId.LeftEar),
        (JointId.RightEye, JointId.RightEar),

        (JointId.LeftShoulder, JointId.RightShoulder),
        (JointId.LeftShoulder, JointId.LeftElbow),
        (JointId.LeftElbow, JointId.LeftWrist),
        (JointId.RightShoulder, JointId.RightElbow),
        (JointId.RightElbow, JointId.RightWrist),

        (JointId.LeftShoulder, JointId.LeftHip),
        (JointId.RightShoulder, JointId.RightHip),
        (JointId.LeftHip, JointId.RightHip),

        (JointId.LeftHip, JointId.LeftKnee),
        (JointId.LeftKnee, JointId.LeftAnkle),
        (JointId.RightHip, JointId.RightKnee),
        (JointId.RightKnee, JointId.RightAnkle),
    };
}
