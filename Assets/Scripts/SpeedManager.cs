using UnityEngine;
using UnityEngine.UI;
using System.IO;
using TMPro;

public class SpeedManager : MonoBehaviour
{
    public Slider speedSlider;
    public TextMeshProUGUI speedText;
    private float fallSpeed = 2.0f;
    private string savePath;

    [System.Serializable]
    public class SpeedSettings
    {
        public float speed;
    }

    void Start()
    {
        savePath = Path.Combine(Application.persistentDataPath, "speedSettings.json");
        LoadSpeedSettings();
        
        if (speedSlider != null)
        {
            speedSlider.minValue = 1.0f;
            speedSlider.maxValue = 6.5f;
            speedSlider.value = fallSpeed;
            speedSlider.onValueChanged.AddListener(delegate { OnSpeedChange(); });
        }
        UpdateSpeedText();
    }

    void OnSpeedChange()
    {
        if (speedSlider != null)
        {
            fallSpeed = speedSlider.value;
            UpdateSpeedText();
            SaveSpeedSettings();
        }
    }

    void UpdateSpeedText()
    {
        if (speedText != null)
        {
            speedText.text = fallSpeed.ToString("F1");
        }
    }

    void SaveSpeedSettings()
    {
        SpeedSettings settings = new SpeedSettings();
        settings.speed = fallSpeed;
        string json = JsonUtility.ToJson(settings);
        File.WriteAllText(savePath, json);
    }

    void LoadSpeedSettings()
    {
        if (File.Exists(savePath))
        {
            try
            {
                string json = File.ReadAllText(savePath);
                SpeedSettings settings = JsonUtility.FromJson<SpeedSettings>(json);
                if (settings.speed >= 1.0f && settings.speed <= 6.5f)
                {
                    fallSpeed = settings.speed;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Failed to load speed settings: " + e.Message);
            }
        }
    }
}
