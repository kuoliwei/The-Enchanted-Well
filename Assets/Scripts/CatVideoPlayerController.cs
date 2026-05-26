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

    [Header("���w����]�C���߷|�ƻs�@���^")]
    [SerializeField] private Material baseMaterial;

    [Header("RenderTexture �ѪR�׳]�w")]
    private int rtWidth = 1920;
    private int rtHeight = 1080;

    private VideoPlayer vp;
    private RawImage rawImg;
    private RenderTexture rt;
    private int currentIndex = 0;
    private bool _pendingPlay = false;

    void Awake()
    {
        vp = GetComponent<VideoPlayer>();
        rawImg = GetComponent<RawImage>();

        vp.playOnAwake = false;
        vp.isLooping = false;

        vp.loopPointReached += OnVideoFinished;
    }

    void Start()
    {
        AssignMaterialInstance();
        CreateNewRenderTexture();

        if (playOnStart)
            PlayFirstClip();
    }
    private float _watchdogTimer = 0f;
    private const float WatchdogInterval = 3f;   // 每 3 秒檢查一次

    void Update()
    {
        // Pending play（從 callback 延遲到 Update）
        if (_pendingPlay)
        {
            _pendingPlay = false;
            PlayCurrentClip();
        }

        // Watchdog：偵測影片是否意外凍結
        _watchdogTimer += Time.deltaTime;
        if (_watchdogTimer >= WatchdogInterval)
        {
            _watchdogTimer = 0f;

            if (videoClips != null && videoClips.Length > 0
                && vp != null
                && !vp.isPlaying
                && !_pendingPlay)
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
        vp.time = 0;   // 恢復，確保每次都從頭播
        vp.Play();
    }


    void OnVideoFinished(VideoPlayer source)
    {
        currentIndex++;

        if (currentIndex >= videoClips.Length)
        {
            if (loopPlaylist)
                currentIndex = 0;
            else
                return;
        }

        _pendingPlay = true;   // 改成設 flag，不直接 Play
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
        if (!pauseStatus && vp != null && !vp.isPlaying)
        {
            // App 恢復時，如果影片沒在播就重新播
            vp.Play();
        }
    }

}
