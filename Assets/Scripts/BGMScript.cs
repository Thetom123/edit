using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Main
{
    public class BGMScript : MonoBehaviour
    {
        private AudioSource _audioSource;
        void Awake()
        {
            GameObject[] objs = GameObject.FindGameObjectsWithTag("BGM");

            if (objs.Length > 1)
            {
                Destroy(this.gameObject);
            }

            DontDestroyOnLoad(this.gameObject);
            _audioSource = GetComponent<AudioSource>();

            // 初始化全域音量
            string savePath = System.IO.Path.Combine(Application.persistentDataPath, "audioSettings.json");
            if (System.IO.File.Exists(savePath))
            {
                try
                {
                    string json = System.IO.File.ReadAllText(savePath);
                    AudioManager.AudioSettings settings = JsonUtility.FromJson<AudioManager.AudioSettings>(json);
                    AudioListener.volume = settings.isMuted ? 0f : settings.volume;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("Failed to load initial audio settings: " + e.Message);
                }
            }
        }

        public void ChangeVolume(float volume)
        {
            _audioSource.volume = volume;
        }

        public static void DestoryBGM(){
            GameObject objs = GameObject.FindGameObjectWithTag("BGM");
            Destroy(objs);
        }
    }
}

