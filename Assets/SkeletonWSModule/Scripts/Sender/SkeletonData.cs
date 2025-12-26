using System;
using System.Collections.Generic;
using UnityEngine;

namespace PoseSocket
{
    public enum JointId
    {
        Nose = 0,
        LeftEye = 1,
        RightEye = 2,
        LeftEar = 3,
        RightEar = 4,
        LeftShoulder = 5,
        RightShoulder = 6,
        LeftElbow = 7,
        RightElbow = 8,
        LeftWrist = 9,
        RightWrist = 10,
        LeftHip = 11,
        RightHip = 12,
        LeftKnee = 13,
        RightKnee = 14,
        LeftAnkle = 15,
        RightAnkle = 16,
    }

    public static class PoseSchema
    {
        public const int JointCount = 17;
    }

    [Serializable]
    public struct Joint
    {
        public float x;
        public float y;
        public float z;
        public float conf;

        public Joint(float x, float y, float z, float conf = 1f)
        {
            this.x = x; this.y = y; this.z = z; this.conf = conf;
        }

        public bool IsValid => conf > 0f;
        public Vector3 XYZ => new Vector3(x, y, z);
    }

    [Serializable]
    public class PersonSkeleton
    {
        public Joint[] joints = new Joint[PoseSchema.JointCount];

        public Joint this[JointId id]
        {
            get => joints[(int)id];
            set => joints[(int)id] = value;
        }
    }

    [Serializable]
    public class SkeletonFrame
    {
        public int frameIndex;
        public List<PersonSkeleton> persons = new List<PersonSkeleton>();
        public List<float> skeletonPercent = new List<float>();
        public List<float> angles = new List<float>();
        public float recvTime;
    }
}
