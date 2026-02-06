public class SystemConfigData
{
    public WellCalibrationConfig wellCalibration;
    public WebSocketConnectionConfig webSocketConnection;
    public CatAppearanceConfig catAppearance;
    public ScreenSettingsConfig screenSettings;
}
public class WellCalibrationConfig
{
    /// <summary>
    /// 用來補償實體井裝設角度誤差（單位：度）
    /// </summary>
    public float rotationOffsetDegrees;

    /// <summary>
    /// 井在畫面中的 XY 偏移補償
    /// </summary>
    public PositionOffset positionOffset;

    /// <summary>
    /// 井整體縮放倍率（1.0 = 原始尺寸）
    /// </summary>
    public float scale;
}
public class PositionOffset
{
    public float x;
    public float y;
}
public class WebSocketConnectionConfig
{
    /// <summary>
    /// WebSocket 伺服器 IP
    /// </summary>
    public string ip;

    /// <summary>
    /// WebSocket 連線 Port
    /// </summary>
    public int port;
}
public class CatAppearanceConfig
{
    /// <summary>
    /// 貓咪探頭出現後，多久顯示完整身體（秒）
    /// </summary>
    public float secondsToRevealFullBody;
    /// <summary>
    /// 人離開多久，貓咪會縮頭（秒）
    /// </summary>
    public float SecondsPersonLeavesTemporarily;
    /// <summary>
    /// 人離開多久，會把貓咪刪除（秒）
    /// </summary>
    public float SecondsPersonLeavesPermanently;
}
public class ScreenSettingsConfig
{
    /// <summary>
    /// 螢幕寬度比例（例如 16:9）
    /// </summary>
    public int widthRatio;

    /// <summary>
    /// 螢幕高度比例
    /// </summary>
    public int heightRatio;
}
