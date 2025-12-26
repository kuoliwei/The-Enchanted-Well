using UnityEngine;

public class PoseAppInitializer : MonoBehaviour
{
    [SerializeField] private WebSocketMessageReceiverAsync receiver;
    [SerializeField] private PoseDataReceiver poseReceiver;

    private void Start()
    {
        // ★★★ 在這裡綁定事件（最正確的位置） ★★★
        receiver.OnRawJsonReceived += poseReceiver.ReceiveRawJson;

        Debug.Log("[PoseAppInitializer] 已綁定 RawJson → PoseDataReceiver");
    }

    private void OnDestroy()
    {
        // ★ 良好習慣：解除訂閱（避免殘留）
        receiver.OnRawJsonReceived -= poseReceiver.ReceiveRawJson;
    }
}
