using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class LandingMenu : MonoBehaviour
{
    public AudioSource audioSource;
    private AudioClip audioClip;
    private float sampleRate;

    public Text audioLengthText;
    public Text audioNameText;
    public Slider audioAdvancementSlider;

    private int saveTime;

    public InputField audioMinRange;
    public InputField audioMaxRange;
    public Slider audioMinRangeSlider;
    public Slider audioMaxRangeSlider;

    // Section naming
    public InputField sectionNameInput; // Assign in Inspector

    // Section list UI
    public Transform sectionListContent; // Assign ScrollView Content in Inspector
    public GameObject sectionButtonPrefab; // Assign Button prefab in Inspector

    public Button NextButton;

    // List of ranges and names: [startSample, endSample], name
    public List<(int[] range, string name)> audioRangeList = new List<(int[], string)>();
    private int RangeIndex = 0;

    void Start()
    {
        InitializeAudio();

        audioMinRangeSlider.onValueChanged.AddListener(delegate { OnMinSliderChanged(); });
        audioMaxRangeSlider.onValueChanged.AddListener(delegate { OnMaxSliderChanged(); });

        audioMinRange.onEndEdit.AddListener(delegate { OnMinInputChanged(); });
        audioMaxRange.onEndEdit.AddListener(delegate { OnMaxInputChanged(); });

        // Initialize min/max fields for first section
        SetSectionFields(0, GetAudioLengthSamples());
    }

    void Update()
    {
        if (audioSource != null && audioSource.clip != null)
        {
            AudioLength();
            RangeSet();
        }
    UpdateNextButtonStateByTime();
    }


    private void UpdateNextButtonStateByTime()
    {
    if (NextButton == null || audioMinRange == null || audioLengthText == null) return;

    // Get the last MM:SS from audioLengthText.text
    string[] parts = audioLengthText.text.Split('-');

    string totalTimeStr = parts[1].Trim(); // e.g. "03:00"
    // Compare only the MM:SS part

    bool finished = audioMinRange.text.Trim() == totalTimeStr;
    NextButton.interactable = finished;
    }


    // Always keep UI in sync and clamp values
    public void RangeSet()
    {
        if (audioSource == null || audioSource.clip == null)
            return;

        int audioLengthSamples = GetAudioLengthSamples();

        // Determine min sample for this section
        int minSample = 0;
        if (audioRangeList.Count > 0)
            minSample = audioRangeList[audioRangeList.Count - 1].range[1];

        // Parse input fields (in MM:SS)
        int parsedMin = ParseTime(audioMinRange.text);
        int parsedMax = ParseTime(audioMaxRange.text);

        // Convert to samples
        int minValue = Mathf.Clamp(TimeToSamples(parsedMin), minSample, audioLengthSamples);
        int maxValue = Mathf.Clamp(TimeToSamples(parsedMax), minValue, audioLengthSamples);

        // Update UI fields
        audioMinRange.text = FormatTime(SamplesToTime(minValue));
        audioMaxRange.text = FormatTime(SamplesToTime(maxValue));

        // Set slider bounds and values
        audioMinRangeSlider.minValue = minSample;
        audioMinRangeSlider.maxValue = audioLengthSamples;
        audioMaxRangeSlider.minValue = minSample;
        audioMaxRangeSlider.maxValue = audioLengthSamples;

        audioMinRangeSlider.value = minValue;
        audioMaxRangeSlider.value = maxValue;
    }

    // Helper: parse "MM:SS" to seconds
    private int ParseTime(string time)
    {
        var parts = time.Split(':');
        if (parts.Length != 2) return 0;
        int min = int.TryParse(parts[0], out min) ? min : 0;
        int sec = int.TryParse(parts[1], out sec) ? sec : 0;
        return min * 60 + sec;
    }

    // Helper: format seconds to "MM:SS"
    private string FormatTime(int seconds)
    {
        int min = seconds / 60;
        int sec = seconds % 60;
        return string.Format("{0:D2}:{1:D2}", min, sec);
    }

    // Helper: get audio length in samples
    private int GetAudioLengthSamples()
    {
        if (audioClip == null) return 0;
        return audioClip.samples;
    }

    // Helper: convert seconds to samples
    private int TimeToSamples(int seconds)
    {
        return Mathf.FloorToInt(seconds * sampleRate);
    }

    // Helper: convert samples to seconds
    private int SamplesToTime(int samples)
    {
        return Mathf.FloorToInt(samples / sampleRate);
    }

    // Set UI fields for a new section
    private void SetSectionFields(int minSample, int maxSample)
    {
        audioMinRange.text = FormatTime(SamplesToTime(minSample));
        audioMaxRange.text = FormatTime(SamplesToTime(maxSample));
        audioMinRangeSlider.value = minSample;
        audioMaxRangeSlider.value = maxSample;
    }

    // Call this from your button to add a section
    public void AddRange()
    {
        int minSample = (int)audioMinRangeSlider.value;
        int maxSample = (int)audioMaxRangeSlider.value;
        int audioLengthSamples = GetAudioLengthSamples();
        string sectionName = sectionNameInput != null ? sectionNameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(sectionName)) sectionName = $"Section {audioRangeList.Count + 1}";

        // Only add if valid and not exceeding audio length
        if (minSample < maxSample && maxSample <= audioLengthSamples)
        {
            audioRangeList.Add((new int[] { minSample, maxSample }, sectionName));
            RangeIndex = audioRangeList.Count - 1;

            // Instantiate button in scroll view
            if (sectionButtonPrefab != null && sectionListContent != null)
            {
                GameObject btnObj = Instantiate(sectionButtonPrefab, sectionListContent);
                btnObj.SetActive(true); // Ensure the button is active
                Text btnText = btnObj.GetComponentInChildren<Text>();
                if (btnText != null)
                    btnText.text = sectionName;

                // Add click listener to select this section and preview its info
                int thisIndex = RangeIndex;
                Button btn = btnObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.AddListener(() => SelectSection(thisIndex));
                }
            }

            // Prepare next section: min = last max, max = end of audio
            SetSectionFields(maxSample, maxSample);

            // Optionally clear the section name input
            if (sectionNameInput != null)
                sectionNameInput.text = "";
        }
    }

    // Select a section by index and preview its info in the UI
    public void SelectSection(int index)
    {
        if (index >= 0 && index < audioRangeList.Count)
        {
            var range = audioRangeList[index].range;
            var name = audioRangeList[index].name;
            SetSectionFields(range[0], range[1]);
            RangeIndex = index;
            if (sectionNameInput != null)
                sectionNameInput.text = name;
        }
    }

    // Expose the list of sections for other scripts
    public List<(int[] range, string name)> GetSections()
    {
        return new List<(int[] range, string name)>(audioRangeList);
    }

    // Slider listeners
    public void OnMinSliderChanged()
    {
        int minValue = (int)audioMinRangeSlider.value;
        int maxValue = (int)audioMaxRangeSlider.value;
        if (minValue > maxValue)
        {
            minValue = maxValue;
            audioMinRangeSlider.value = minValue;
        }
        audioMinRange.text = FormatTime(SamplesToTime(minValue));
    }

    public void OnMaxSliderChanged()
    {
        int maxValue = (int)audioMaxRangeSlider.value;
        int minValue = (int)audioMinRangeSlider.value;
        if (maxValue < minValue)
        {
            maxValue = minValue;
            audioMaxRangeSlider.value = maxValue;
        }
        audioMaxRange.text = FormatTime(SamplesToTime(maxValue));
    }

    // InputField listeners
    public void OnMinInputChanged()
    {
        int minSeconds = ParseTime(audioMinRange.text);
        int minSample = TimeToSamples(minSeconds);
        int maxSample = (int)audioMaxRangeSlider.value;
        int audioLengthSamples = GetAudioLengthSamples();

        // Clamp
        minSample = Mathf.Clamp(minSample, 0, audioLengthSamples);
        if (minSample > maxSample)
            minSample = maxSample;

        audioMinRange.text = FormatTime(SamplesToTime(minSample));
        audioMinRangeSlider.value = minSample;
    }

    public void OnMaxInputChanged()
    {
        int maxSeconds = ParseTime(audioMaxRange.text);
        int maxSample = TimeToSamples(maxSeconds);
        int minSample = (int)audioMinRangeSlider.value;
        int audioLengthSamples = GetAudioLengthSamples();

        // Clamp
        maxSample = Mathf.Clamp(maxSample, minSample, audioLengthSamples);

        audioMaxRange.text = FormatTime(SamplesToTime(maxSample));
        audioMaxRangeSlider.value = maxSample;
    }

    // Initializes the AudioSource and AudioClip
    public void InitializeAudio()
    {
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioClip = audioSource.clip;
        if (audioClip != null)
        {
            sampleRate = audioClip.frequency;
            audioNameText.text = audioClip.name;
            Debug.Log("Audio initialized with sample rate: " + sampleRate);
        }
    }

    public void AudioLength()
    {
        if (audioSource != null && audioSource.clip != null)
        {
            audioAdvancementSlider.minValue = 0;
            audioAdvancementSlider.maxValue = audioClip.samples;
            audioMaxRangeSlider.maxValue = audioClip.samples;
            if (audioSource.isPlaying)
            {
                audioAdvancementSlider.value = audioSource.timeSamples;
            }
            int totalSeconds = Mathf.FloorToInt(audioClip.samples / sampleRate);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            int elapsedTime = Mathf.FloorToInt(audioSource.time);
            int elapsedMinutes = elapsedTime / 60;
            int elapsedSeconds = elapsedTime % 60;
            if (audioSource.isPlaying)
            {
                audioLengthText.text = string.Format("{0:D2}:{1:D2} - {2:D2}:{3:D2}", elapsedMinutes, elapsedSeconds, minutes, seconds);
            }
            else
            {
                int totaleelasped = Mathf.FloorToInt(saveTime / sampleRate);
                int saveMinutes = totaleelasped / 60;
                int saveSeconds = totaleelasped % 60;
                audioLengthText.text = string.Format("{0:D2}:{1:D2} - {2:D2}:{3:D2}", saveMinutes, saveSeconds, minutes, seconds);
            }
        }
        else
        {
            Debug.LogWarning("AudioSource or AudioClip is not assigned.");
        }
    }

    public void StopAudio()
    {
        saveTime = audioSource.timeSamples;
        audioSource.Stop();
    }
    public void PlayAudio()
    {
        audioSource.Play();
        audioSource.timeSamples = saveTime;
    }

    public void OnDragStart()
    {
        saveTime = audioSource.timeSamples;
        audioAdvancementSlider.value = saveTime;
        audioSource.Pause();
    }
    public void OnSeek()
    {
        saveTime = (int)audioAdvancementSlider.value;
        audioSource.timeSamples = saveTime;
    }
    public void OnDragEnd()
    {
        audioSource.Play();
    }
}