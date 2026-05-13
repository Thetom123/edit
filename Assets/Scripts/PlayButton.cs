using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayButton : MonoBehaviour
{

    private static string CurrentSong;
    private static int CurrentEventStatus;

    public GameObject LoadingScreen, SongName, SongJacket;
    private Animator loadingScreenAnimator;

    // Start is called before the first frame update
    void Start()
    {
        if (LoadingScreen != null)
        {
            loadingScreenAnimator = LoadingScreen.GetComponent<Animator>();
        }
        else
        {
            Debug.LogError("LoadingScreen is not assigned in the inspector.");
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void PlaySong(){
        Debug.Log(CurrentSong);
        
        string eventFilePath = System.IO.Path.Combine(Application.persistentDataPath, $"EventStatus_{CurrentSong}.txt");
        bool hasPlayedEvent = System.IO.File.Exists(eventFilePath);

        if (CurrentEventStatus == 1 && !hasPlayedEvent)
        {
            // Do custom event video playing
            StartCoroutine(PlayEventVideoAndLoad(eventFilePath));
        }
        else
        {
            // Normal loading
            LoadingScreen.SetActive(true);
            loadingScreenAnimator.SetTrigger("SlideUp");

            SongName.GetComponent<TextMeshProUGUI>().text = CurrentSong;
            var _jacket = Resources.Load<Sprite>("Songs/"+CurrentSong+"/Jacket");
            SongJacket.GetComponent<Image>().sprite = _jacket;

            //switch scene to SongPlaying after 3 seconds
            StartCoroutine(LoadScene());
        }
    }

    IEnumerator PlayEventVideoAndLoad(string filePath)
    {
        // 1. Create a full screen black UI
        GameObject canvasObj = new GameObject("EventCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // Make sure it is on top
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject blackScreenObj = new GameObject("BlackScreen");
        blackScreenObj.transform.SetParent(canvasObj.transform, false);
        Image blackScreen = blackScreenObj.AddComponent<Image>();
        blackScreen.color = new Color(0, 0, 0, 0);
        RectTransform rt = blackScreen.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Fade out to black
        float fadeDuration = 1.0f;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            blackScreen.color = new Color(0, 0, 0, elapsed / fadeDuration);
            yield return null;
        }
        blackScreen.color = new Color(0, 0, 0, 1);

        // 2. Play Video
        UnityEngine.Video.VideoClip clip = Resources.Load<UnityEngine.Video.VideoClip>("Songs/" + CurrentSong + "/EventVideo");
        if (clip != null)
        {
            // Create RenderTexture
            RenderTexture renderTexture = new RenderTexture(1920, 1080, 24);
            
            // Create RawImage to display the video
            GameObject videoImageObj = new GameObject("VideoRawImage");
            videoImageObj.transform.SetParent(canvasObj.transform, false);
            RawImage videoImage = videoImageObj.AddComponent<RawImage>();
            videoImage.texture = renderTexture;
            RectTransform vrt = videoImage.rectTransform;
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = Vector2.zero;
            vrt.offsetMax = Vector2.zero;

            GameObject videoPlayerObj = new GameObject("EventVideoPlayer");
            videoPlayerObj.transform.SetParent(canvasObj.transform, false);

            UnityEngine.Video.VideoPlayer videoPlayer = videoPlayerObj.AddComponent<UnityEngine.Video.VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.renderMode = UnityEngine.Video.VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = renderTexture;
            videoPlayer.clip = clip;
            
            // Fix for frozen video / fast audio
            videoPlayer.skipOnDrop = false;
            videoPlayer.playbackSpeed = 1f;
            videoPlayer.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.Direct;

            // wait for preparation
            videoPlayer.Prepare();
            while (!videoPlayer.isPrepared)
            {
                yield return null;
            }

            videoPlayer.frame = 0;
            
            bool isDone = false;
            videoPlayer.loopPointReached += (vp) => { isDone = true; };

            // Wait for 1 second before playing
            yield return new WaitForSeconds(1f);

            videoPlayer.Play();
            
            // Wait for video to finish
            while (!isDone)
            {
                yield return null;
            }

            renderTexture.Release();
        }
        else
        {
            Debug.LogWarning("EventVideo not found for song: " + CurrentSong);
            yield return new WaitForSeconds(1f); // Just wait a bit if video is missing
        }

        // 3. Write to local file
        try {
            System.IO.File.WriteAllText(filePath, "1");
        } catch(Exception e) { Debug.LogError(e); }

        // 4. Enter SongPlaying
        SceneManager.LoadScene("SongPlaying");
    }
    
    IEnumerator LoadScene(){
        yield return new WaitForSeconds(3);
        SceneManager.LoadScene("SongPlaying");
    }

    public void SetPlaySong(string SongID, int eventStatus = 0){
        CurrentSong = SongID;
        CurrentEventStatus = eventStatus;
        Debug.Log(CurrentSong + " EventStatus: " + CurrentEventStatus);
    }

    public string GetPlaySong(){
        return CurrentSong;
    }
}
