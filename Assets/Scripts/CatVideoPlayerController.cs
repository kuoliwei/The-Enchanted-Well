using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;

public class CatVideoPlayerController : MonoBehaviour
{
    [Header("�v���C���]�|�̧ǽ����^")]
    public VideoClip[] videoClips;

    [Header("�]�w")]
    public bool playOnStart = true;
    public bool loopPlaylist = true;

    [Header("影格序列播放（已完全取代 VideoPlayer，舊路徑已封死無法再被啟用）")]
    private const bool useFrameSequenceMode = true;   // 鎖死為 true：不再是 Inspector 可調欄位，場景/Prefab 裡殘留的舊序列化值會被忽略
    public string[] frameSequenceNames;                            // 與 videoClips 平行的播放清單，存放 PNG 序列的資料夾名稱

    [Header("���w����]�C���߷|�ƻs�@���^")]
    [SerializeField] private Material baseMaterial;

    [Header("RenderTexture �ѪR�׳]�w")]
    private int rtWidth = 1920;
    private int rtHeight = 1080;

    private VideoPlayer vp;
    private RawImage rawImg;
    private RenderTexture rt;
    private RenderTexture snapshotRt;   // 換片瞬間用來定格最後一幀，遮住 rt 被清黑的過程
    private int currentIndex = 0;
    private bool _pendingPlay = false;
    private bool _preloading = false;
    private float _preloadTimer = 0f;
    private const float PreloadTimeout = 2f;   // 保險：準備超過 2 秒就直接播

    private CatFrameSequencePlayer frameSequencePlayer;
    private int frameSeqIndex = 0;

    void Awake()
    {
        vp = GetComponent<VideoPlayer>();
        rawImg = GetComponent<RawImage>();

        vp.playOnAwake = false;
        vp.isLooping = false;

        vp.loopPointReached += OnVideoFinished;

        // frameReady 用來得知「新影片的第一幀已寫入 rt」，此時才把畫面從定格切回 rt
        vp.sendFrameReadyEvents = true;
        vp.frameReady += OnFrameReady;
    }

    void Start()
    {
        AssignMaterialInstance();   // 兩種模式都需要：把貓咪去背/波紋材質裝到 RawImage 上

        if (useFrameSequenceMode)
        {
            Debug.Log($"[CatVideoPlayerController] ★★★ 使用新模式 (Frame Sequence) ★★★  gameObject={gameObject.name}");
            SetupFrameSequenceMode();
            return;
        }

        Debug.Log($"[CatVideoPlayerController] ▲▲▲ 使用舊模式 (VideoPlayer) ▲▲▲  gameObject={gameObject.name}");
        CreateNewRenderTexture();

        if (playOnStart)
            PlayFirstClip();
    }

    private void SetupFrameSequenceMode()
    {
        frameSequencePlayer = GetComponent<CatFrameSequencePlayer>();
        if (frameSequencePlayer == null)
        {
            Debug.LogError("[CatVideoPlayerController] useFrameSequenceMode 已啟用，但找不到 CatFrameSequencePlayer 元件");
            return;
        }

        frameSequencePlayer.OnClipFinished += OnFrameSequenceFinished;

        // 一次把整份播放列表都載入快取，避免播放中途換片時才觸發同步載入卡頓
        // （同步載入 200+ 張 PNG 可能卡住主執行緒到看得出來黑一下，跟舊版 VideoPlayer
        // 的黑屏成因完全不同，但視覺上幾乎一樣）
        frameSequencePlayer.PreloadSequences(frameSequenceNames);

        if (playOnStart)
            PlayFirstFrameSequence();
    }

    private void PlayFirstFrameSequence()
    {
        if (frameSequenceNames == null || frameSequenceNames.Length == 0)
        {
            Debug.LogWarning($"[CatVideoPlayerController] frameSequenceNames 是空的（CatManager 可能沒有指派，或 useFrameSequenceMode 沒開）gameObject={gameObject.name}");
            return;
        }

        frameSeqIndex = 0;
        Debug.Log($"[CatVideoPlayerController] Frame Sequence 播放第一段: {frameSequenceNames[frameSeqIndex]}");
        frameSequencePlayer.Play(frameSequenceNames[frameSeqIndex]);
    }

    private void OnFrameSequenceFinished()
    {
        frameSeqIndex++;

        if (frameSeqIndex >= frameSequenceNames.Length)
        {
            if (loopPlaylist)
                frameSeqIndex = 0;
            else
                return;
        }

        Debug.Log($"[CatVideoPlayerController] Frame Sequence 換下一段: {frameSequenceNames[frameSeqIndex]}");
        frameSequencePlayer.Play(frameSequenceNames[frameSeqIndex]);
    }

    private float _watchdogTimer = 0f;
    private const float WatchdogInterval = 3f;   // 每 3 秒檢查一次

    void Update()
    {
        // Frame-sequence 模式沒有解碼器延遲問題，不需要下方的 preload/watchdog 機制
        if (useFrameSequenceMode)
            return;

        // Preload state machine: wait until decoder is ready, then play
        if (_preloading && vp != null)
        {
            _preloadTimer += Time.deltaTime;

            if (vp.isPrepared)
            {
                _preloading = false;
                vp.Play();
            }
            else if (_preloadTimer >= PreloadTimeout)
            {
                // 保險：isPrepared 遲遲未就緒，直接 Play（Play 會隱式 prepare）
                Debug.LogWarning("[CatVideoPlayerController] Preload 逾時，強制播放");
                _preloading = false;
                vp.Play();
            }
        }

        // Pending play（從 callback 延遲到 Update）
        if (_pendingPlay)
        {
            _pendingPlay = false;
            PreloadNextClip();
        }

        // Watchdog：偵測影片是否意外凍結
        _watchdogTimer += Time.deltaTime;
        if (_watchdogTimer >= WatchdogInterval)
        {
            _watchdogTimer = 0f;

            if (videoClips != null && videoClips.Length > 0
                && vp != null
                && !vp.isPlaying
                && !_pendingPlay
                && !_preloading)
            {
                Debug.LogWarning("[CatVideoPlayerController] 偵測到影片停止，自動重播");
                PlayCurrentClip();
            }
        }
    }


    private void AssignMaterialInstance()
    {
        if (rawImg == null)
            return;

        if (baseMaterial == null)
        {
            Debug.LogWarning("[CatVideoPlayerController] baseMaterial �����w");
            return;
        }

        rawImg.material = Instantiate(baseMaterial);

        GetComponent<BlackKeyWaveBlurController>()?.SetMaterial(rawImg.material);
    }

    private void CreateNewRenderTexture()
    {
        if (rt != null)
        {
            rt.Release();
            DestroyImmediate(rt);
        }
        Debug.Log($"rtWidth:{rtWidth},rtHeight:{rtHeight}");
        int width = Mathf.Max(1, rtWidth);
        int height = Mathf.Max(1, rtHeight);

        Debug.Log($"width:{width},height:{height}");
        rt = new RenderTexture(width, height, 0, RenderTextureFormat.RGB565);
        rt.antiAliasing = 1;
        rt.filterMode = FilterMode.Bilinear;
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.Create();

        if (snapshotRt != null)
        {
            snapshotRt.Release();
            DestroyImmediate(snapshotRt);
        }
        snapshotRt = new RenderTexture(width, height, 0, RenderTextureFormat.RGB565);
        snapshotRt.filterMode = FilterMode.Bilinear;
        snapshotRt.wrapMode = TextureWrapMode.Clamp;
        snapshotRt.Create();

        vp.targetTexture = rt;

        if (rawImg != null)
            rawImg.texture = rt;
    }
    void PlayFirstClip()
    {
        if (videoClips == null || videoClips.Length == 0)
        {
            Debug.LogWarning("CatVideoPlayerController: videoClips 是空的");
            return;
        }

        currentIndex = 0;
        PlayCurrentClip();
    }

    void PlayCurrentClip()
    {
        if (videoClips == null || videoClips.Length == 0)
            return;

        vp.clip = videoClips[currentIndex];
        vp.time = 0;
        vp.Play();
    }

    void PreloadNextClip()
    {
        if (videoClips == null || videoClips.Length == 0)
            return;

        vp.clip = videoClips[currentIndex];
        vp.time = 0;
        vp.Prepare();          // 啟動解碼器非同步準備（先前漏了這行，導致 isPrepared 永遠不會就緒）
        _preloading = true;
        _preloadTimer = 0f;
        // 這裡刻意不 Play：畫面會停留在上一段的最後一幀（而不是黑屏），
        // 等 Update() 偵測到 isPrepared 後才 Play，把解碼延遲藏起來。
    }


    void OnVideoFinished(VideoPlayer source)
    {
        Debug.Log($"[CatVideoPlayerController] ▲▲▲ 舊模式 (VideoPlayer) loopPointReached ▲▲▲  gameObject={gameObject.name}");

        // 立刻把最後一幀定格到 snapshotRt 並顯示它。
        // 之後換 clip / Prepare 會把 rt 清成黑色，但觀眾看的是定格畫面，看不到黑屏。
        if (rt != null && snapshotRt != null && rawImg != null)
        {
            Graphics.Blit(rt, snapshotRt);
            rawImg.texture = snapshotRt;
        }

        currentIndex++;

        if (currentIndex >= videoClips.Length)
        {
            if (loopPlaylist)
                currentIndex = 0;
            else
                return;
        }

        _pendingPlay = true;
    }

    void OnFrameReady(VideoPlayer source, long frameIdx)
    {
        // 新影片的一幀已確實寫入 rt，把顯示從定格切回即時畫面
        if (rawImg != null && rawImg.texture != rt)
            rawImg.texture = rt;
    }

    public void PlayClipByIndex(int index)
    {
        if (index < 0 || index >= videoClips.Length)
            return;

        currentIndex = index;
        PlayCurrentClip();
    }
    // 處理 App 失去焦點（Alt+Tab、螢幕保護等）導致影片暫停
    void OnApplicationPause(bool pauseStatus)
    {
        if (useFrameSequenceMode)
            return;

        if (!pauseStatus && vp != null && !vp.isPlaying)
        {
            // App 恢復時，如果影片沒在播就重新播
            vp.Play();
        }
    }

}
