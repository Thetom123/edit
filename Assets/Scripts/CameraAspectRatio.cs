using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraAspectRatio : MonoBehaviour
{
    [Header("目標螢幕比例 (預設 16:9)")]
    public float targetAspectX = 16.0f;
    public float targetAspectY = 9.0f;

    private float currentWindowAspect = 0f;
    private Camera backgroundCam;

    void Start()
    {
        CreateBackgroundCamera();
        UpdateAspectRatio();
    }

    void CreateBackgroundCamera()
    {
        // 創建一個只渲染純黑背景的副攝影機
        GameObject bgCamObj = new GameObject("BackgroundCamera");
        backgroundCam = bgCamObj.AddComponent<Camera>();
        backgroundCam.depth = -100; // 確保在主攝影機後面
        backgroundCam.clearFlags = CameraClearFlags.SolidColor;
        backgroundCam.backgroundColor = Color.black;
        backgroundCam.cullingMask = 0; // 不渲染任何物件
        backgroundCam.useOcclusionCulling = false;
    }

    void Update()
    {
        // 如果在 PC 端允許玩家動態拉扯視窗大小，持續偵測並更新
        float windowAspect = (float)Screen.width / (float)Screen.height;
        if (Mathf.Abs(windowAspect - currentWindowAspect) > 0.01f)
        {
            UpdateAspectRatio();
        }
    }

    void UpdateAspectRatio()
    {
        float targetAspect = targetAspectX / targetAspectY;
        currentWindowAspect = (float)Screen.width / (float)Screen.height;

        // 計算縮放高度比例
        float scaleHeight = currentWindowAspect / targetAspect;

        Camera camera = GetComponent<Camera>();

        // 如果當前螢幕比預設比例更寬 (例如 21:9 螢幕配上 16:9 遊戲) -> 上下加黑邊 (Pillarbox/Letterbox)
        if (scaleHeight < 1.0f)
        {
            Rect rect = camera.rect;
            rect.width = 1.0f;
            rect.height = scaleHeight;
            rect.x = 0;
            rect.y = (1.0f - scaleHeight) / 2.0f;
            camera.rect = rect;
        }
        // 如果當前螢幕比預設比例更窄 (例如 4:3 或 iPad 螢幕配上 16:9 遊戲) -> 左右加黑邊
        else
        {
            float scaleWidth = 1.0f / scaleHeight;
            Rect rect = camera.rect;
            rect.width = scaleWidth;
            rect.height = 1.0f;
            rect.x = (1.0f - scaleWidth) / 2.0f;
            rect.y = 0;
            camera.rect = rect;
        }
    }
}
