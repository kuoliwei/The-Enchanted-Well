using System.Collections.Generic;
using UnityEngine;
using PoseSocket;

public class SkeletonVisualizer : MonoBehaviour
{
    [Header("Prefabs & Materials")]
    [SerializeField] private GameObject jointPrefab;
    [SerializeField] private Material boneMaterial;

    [Header("Joint Visual Settings")]
    [SerializeField] private Vector3 jointScale = Vector3.one * 0.03f;
    [SerializeField] private float boneWidth = 0.02f;

    [Header("Parent Container")]
    [SerializeField] private Transform skeletonParent;

    /* ------------------------------------------------------
     * ★★★ Pixel → Unity World 映射設定 ★★★
     * ------------------------------------------------------*/
    [Header("Image Source Resolution (解析度)")]
    public float imageWidth = 1920f;
    public float imageHeight = 1080f;

    [Header("Unity 映射範圍 (World Space)")]
    public float worldWidth = 10f;
    public float worldHeight = 6f;

    private class PersonVisual
    {
        public Transform root;
        public Transform[] joints;
        public Renderer[] renderers;
        public LineRenderer[] bones;
    }

    private Dictionary<int, PersonVisual> visuals = new Dictionary<int, PersonVisual>();

    private static readonly (JointId a, JointId b)[] bonePairs =
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
        (JointId.LeftHip, JointId.LeftKnee),
        (JointId.LeftKnee, JointId.LeftAnkle),
        (JointId.RightHip, JointId.RightKnee),
        (JointId.RightKnee, JointId.RightAnkle)
    };

    /* ------------------------------------------------------
     * ★ Pixel → World 映射函式（最重要）
     * ------------------------------------------------------*/
    private Vector3 PixelToWorld(float px, float py)
    {
        // ★★ 1. 翻轉 Y：影像是上到下，Unity 是下到上
        float flippedY = imageHeight - py;

        // ★★ 2. Pixel → 0~1
        float nx = px / imageWidth;
        float ny = flippedY / imageHeight;

        // ★★ 3. 0~1 → 指定 worldWidth/worldHeight 的平面
        float wx = nx * worldWidth - worldWidth * 0.5f;
        float wy = ny * worldHeight - worldHeight * 0.5f;

        return new Vector3(wx, wy, 0f); // Z 先固定 0
    }

    /* ------------------------------------------------------
     * 外部呼叫入口：PoseDataReceiver 觸發
     * ------------------------------------------------------*/
    public void UpdateSkeletons(SkeletonFrame frame)
    {
        if (frame == null) return;

        //Debug.Log($"[Visualizer] UpdateSkeletons persons = {frame.persons.Count}");

        int count = frame.persons.Count;

        // 如果人數變少 → 刪除多餘的人
        List<int> toRemove = new List<int>();
        foreach (var kvp in visuals)
        {
            if (kvp.Key >= count)
                toRemove.Add(kvp.Key);
        }
        foreach (int pid in toRemove)
        {
            Destroy(visuals[pid].root.gameObject);
            visuals.Remove(pid);
        }

        // 更新或建立人
        for (int pid = 0; pid < count; pid++)
        {
            PersonSkeleton person = frame.persons[pid];

            if (!visuals.TryGetValue(pid, out PersonVisual visual))
            {
                visual = CreatePersonVisual(pid, person);
                visuals.Add(pid, visual);
            }

            UpdatePersonVisual(visual, person);
        }
    }

    /* ------------------------------------------------------
     * 建立一個人的可視化
     * ------------------------------------------------------*/
    private PersonVisual CreatePersonVisual(int personId, PersonSkeleton data)
    {
        var pv = new PersonVisual();

        pv.root = new GameObject($"Skeleton_{personId}").transform;
        pv.root.SetParent(skeletonParent ? skeletonParent : transform, false);

        int jointCount = data.joints.Length;
        pv.joints = new Transform[jointCount];
        pv.renderers = new Renderer[jointCount];

        for (int i = 0; i < jointCount; i++)
        {
            GameObject j = Instantiate(jointPrefab, pv.root);
            j.name = ((JointId)i).ToString();
            j.transform.localScale = jointScale;

            pv.joints[i] = j.transform;
            pv.renderers[i] = j.GetComponent<Renderer>();
        }

        pv.bones = new LineRenderer[bonePairs.Length];

        for (int i = 0; i < bonePairs.Length; i++)
        {
            GameObject b = new GameObject($"Bone_{i}");
            b.transform.SetParent(pv.root, false);

            LineRenderer lr = b.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.startWidth = boneWidth;
            lr.endWidth = boneWidth;

            lr.material = boneMaterial != null ?
                boneMaterial :
                new Material(Shader.Find("Sprites/Default"));

            lr.startColor = Color.green;
            lr.endColor = Color.green;
            lr.useWorldSpace = true;

            pv.bones[i] = lr;
        }

        return pv;
    }

    /* ------------------------------------------------------
     * 更新 joints + bones
     * ------------------------------------------------------*/
    private void UpdatePersonVisual(PersonVisual pv, PersonSkeleton data)
    {
        // joints
        for (int i = 0; i < data.joints.Length; i++)
        {
            PoseSocket.Joint j = data.joints[i];

            // ★ Pixel → Unity World Mapping
            pv.joints[i].position = PixelToWorld(j.x, j.y);
        }

        // bones
        for (int i = 0; i < bonePairs.Length; i++)
        {
            var (a, b) = bonePairs[i];

            PoseSocket.Joint ja = data[(JointId)a];
            PoseSocket.Joint jb = data[(JointId)b];

            Vector3 posA = PixelToWorld(ja.x, ja.y);
            Vector3 posB = PixelToWorld(jb.x, jb.y);

            pv.bones[i].SetPosition(0, posA);
            pv.bones[i].SetPosition(1, posB);
        }
    }
}
