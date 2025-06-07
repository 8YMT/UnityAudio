using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class VolumeControl : MonoBehaviour
{
    public AudioMixer audioMixer; // Assign your AudioMixer here
    public Slider volumeSlider;   // Assign your UI Slider here
    public Text volumeDisplayText; // Assign your UI Text element here

    private void Start()
    {
        // Initialize the slider value based on the current volume
        float currentVolume;
        audioMixer.GetFloat("MasterVolume", out currentVolume);

        // Map the current dB value to the slider's range (0-100)
        volumeSlider.value = Mathf.InverseLerp(-80f, 6f, currentVolume) * 100f;

        // Update the UI text to display the current dB value
        UpdateVolumeDisplay(currentVolume);

        // Add listener to the slider
        volumeSlider.onValueChanged.AddListener(SetVolume);
    }

    public void SetVolume(float sliderValue)
    {
        // Map slider value (0-100) to dB range (-80 to +6 dB)
        float volumeDB = Mathf.Lerp(-80f, 6f, sliderValue / 100f);

        // Set the volume in the AudioMixer
        audioMixer.SetFloat("MasterVolume", volumeDB);

        // Update the UI text to display the current dB value
        UpdateVolumeDisplay(volumeDB);
    }

    private void UpdateVolumeDisplay(float volumeDB)
    {
        // Update the UI text to show the current dB value
        if (volumeDB <= -80f)
        {
            volumeDisplayText.text = "-∞ dB"; // Display -∞ for silence
        }
        else
        {
            volumeDisplayText.text = $"{volumeDB:F1} dB"; // Display dB value with 1 decimal place
        }
    }
}