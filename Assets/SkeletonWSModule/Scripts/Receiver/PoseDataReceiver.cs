using PoseSocket;
using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class PoseDataReceiver : MonoBehaviour
{
    public Text debug;
    /// <summary>
    /// 最新解析後的骨架資料
    /// </summary>
    public SkeletonFrame LatestFrame { get; private set; }

    /// <summary>
    /// 當成功解析骨架資料時觸發
    /// </summary>
    public event Action<SkeletonFrame> OnSkeletonFrame;

    /// <summary>
    /// 給 WebSocketMessageReceiverAsync 的 OnRawJsonReceived( string )
    /// 用於接收 raw JSON 並解析成 SkeletonFrame
    /// </summary>
    public void ReceiveRawJson(string json)
    {
        //Debug.Log($"[PoseDataReceiver] 收到原始 JSON:\n{json}");
        //debug.text = $"[PoseDataReceiver] 收到原始 JSON:\n{json}";

        SkeletonFrame frame = null;

        try
        {
            frame = SkeletonParser.Parse(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PoseDataReceiver] 解析失敗: {e.Message}");
            //debug.text = $"[PoseDataReceiver] 解析失敗: {e.Message}";
            return;
        }

        string percentList = string.Join(", ", frame.skeletonPercent);
        string angleList = string.Join(", ", frame.angles);

        debug.text =
            $"skeleton_percentage: [{percentList}]\n" +
            $"angle: [{angleList}]";


        LatestFrame = frame;
        OnSkeletonFrame?.Invoke(frame);
        PrintSkeletonFrame(frame);
    }
    /// <summary>
    /// 印出骨架資料（for debugging）
    /// </summary>
    private void PrintSkeletonFrame(SkeletonFrame frame)
    {
        if (frame == null)
        {
            Debug.LogWarning("[PoseDataReceiver] frame is null, 無法列印");
            return;
        }

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("========== SkeletonFrame ==========");
        sb.AppendLine($"Frame Index: {frame.frameIndex}");
        sb.AppendLine($"Person Count: {frame.persons.Count}");

        for (int p = 0; p < frame.persons.Count; p++)
        {
            var person = frame.persons[p];
            sb.AppendLine($"-- Person {p} --");

            for (int j = 0; j < person.joints.Length; j++)
            {
                var joint = person.joints[j];
                sb.AppendLine(
                    $"Joint {j}: (x:{joint.x:F3}, y:{joint.y:F3}, z:{joint.z:F3}, conf:{joint.conf:F3})"
                );
            }
        }

        sb.AppendLine("===================================");

        //Debug.Log(sb.ToString());
    }

}
