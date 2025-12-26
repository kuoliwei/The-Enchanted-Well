using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;

public class CatVideoPlayerController : MonoBehaviour
{
    [Header("影片列表（會依序輪播）")]
    public VideoClip[] videoClips;

    [Header("設定")]
    public bool playOnStart = true;
    public bool loopPlaylist = true;

    [Header("指定材質（每隻貓會複製一份）")]
    [SerializeField] private Material baseMaterial;

    [Header("RenderTexture 解析度設定")]
    private int rtWidth = 1920;
    private int rtHeight = 1080;

    private VideoPlayer vp;
    private RawImage rawImg;
    private RenderTexture rt;
    private int currentIndex = 0;

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

    private void AssignMaterialInstance()
    {
        if (rawImg == null)
            return;

        if (baseMaterial == null)
        {
            Debug.LogWarning("[CatVideoPlayerController] baseMaterial 未指定");
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
            Debug.LogWarning("CatVideoPlayerController: videoClips 為空");
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

        PlayCurrentClip();
    }

    public void PlayClipByIndex(int index)
    {
        if (index < 0 || index >= videoClips.Length)
            return;

        currentIndex = index;
        PlayCurrentClip();
    }
}
