using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class Execution : MonoBehaviour
{
    [Header("UI References")]
    public Image analysisPanel;
    public Canvas analsisCanvas;
    public Button finishButton;
    public Text statusText;

    [Header("Audio")]
    public AudioSource audioSource;

    [Header("Results")]
    public int detectedTempo = -1;
    private float[] energyArray;

    [Header("Analysis Settings")]
    public int blockSize = 1024;
    [Range(40, 240)] public int minTempo = 60;
    [Range(40, 240)] public int maxTempo = 200;

    private bool analysisStarted = false;

    [Header("Imported Data")]
    public EditorManger editorManager;
    public LandingMenu landingMenu;

    private List<EditorManger.Section> importedSections;
    private List<(int[] range, string name)> importedAudioRanges;

    [Header("Light Arrays")]
    public Light[] sceneLights = new Light[11];
    public Light[] ceilingLights = new Light[8];
    public Light[] secondFloorLights = new Light[12];

    private int currentSectionIndex = 0;
    private float nextBeatTime = 0f;
    private float beatInterval = 0f;
    private float sectionStartTime = 0f;
    private float sectionEndTime = 0f;
    private bool isPlayingVisuals = false;
    private int currentBeat = 0;
    private float lastEnergy = 1f;

    void Start()
    {
        if (finishButton != null)
            finishButton.onClick.AddListener(OnFinishButtonClicked);
    }

    void Update()
    {
        if (analysisPanel != null && analysisPanel.gameObject.activeSelf)
        {
            if (!analysisStarted)
            {
                analysisStarted = true;
                ImportExternalData();
                StartCoroutine(AnalyzeSong());
            }
        }
        else
        {
            analysisStarted = false;
        }

        if (isPlayingVisuals && audioSource != null && audioSource.isPlaying)
        {
            AdvanceSectionIfNeeded();
            AnimateLights();
        }
    }

    private void SetLightActive(Light light, bool active)
    {
        if (light != null)
            light.gameObject.SetActive(active);
    }

    private void ImportExternalData()
    {
        if (editorManager != null)
        {
            importedSections = editorManager.Sections;
            Debug.Log($"Imported {importedSections.Count} sections from EditorManger.");
        }

        if (landingMenu != null)
        {
            importedAudioRanges = landingMenu.audioRangeList;
            Debug.Log($"Imported {importedAudioRanges.Count} audio ranges from LandingMenu.");
        }
    }

    private System.Collections.IEnumerator AnalyzeSong()
    {
        if (analysisPanel == null || !analysisPanel.gameObject.activeSelf)
            yield break;

        if (finishButton != null) finishButton.interactable = false;
        if (statusText != null) statusText.text = "Analyzing...";

        yield return null;

        if (audioSource == null || audioSource.clip == null)
        {
            if (statusText != null) statusText.text = "No Audio!";
            yield break;
        }

        AudioClip audioClip = audioSource.clip;
        int sampleCount = audioClip.samples;
        int channels = audioClip.channels;
        int blockCount = Mathf.CeilToInt((float)sampleCount / blockSize);

        float[] allSamples = new float[sampleCount * channels];
        audioClip.GetData(allSamples, 0);

        float[] rightChannel = new float[sampleCount];
        for (int i = 0, j = 1; j < allSamples.Length && i < sampleCount; i++, j += 2)
            rightChannel[i] = allSamples[j];

        energyArray = new float[blockCount];
        for (int block = 0; block < blockCount; block++)
        {
            int startSample = block * blockSize;
            int endSample = Mathf.Min(startSample + blockSize, sampleCount);
            float sum = 0f;
            for (int i = startSample; i < endSample; i++)
                sum += Mathf.Abs(rightChannel[i]);
            energyArray[block] = sum / (endSample - startSample);
        }

        float[] rmsDbValues = new float[blockCount];
        for (int block = 0; block < blockCount; block++)
        {
            int startSample = block * blockSize;
            int endSample = Mathf.Min(startSample + blockSize, sampleCount);
            float sumSq = 0f;
            for (int i = startSample; i < endSample; i++)
                sumSq += rightChannel[i] * rightChannel[i];
            float rms = Mathf.Sqrt(sumSq / (endSample - startSample));
            rmsDbValues[block] = 20f * Mathf.Log10(Mathf.Max(rms, 1e-6f));
        }

        List<int> beatBlocks = FindPotentialBeats(rmsDbValues, audioClip.frequency, blockSize);

        detectedTempo = -1;
        if (beatBlocks.Count >= 2)
        {
            Dictionary<int, float> tempoScores = new Dictionary<int, float>();
            for (int i = 1; i < beatBlocks.Count; i++)
            {
                float interval = (beatBlocks[i] - beatBlocks[i - 1]) * blockSize / (float)audioClip.frequency;
                int tempo = Mathf.RoundToInt(60f / interval);
                tempo = Mathf.Clamp(tempo, minTempo, maxTempo);

                float score = 1f - Mathf.Abs(rmsDbValues[beatBlocks[i]] - rmsDbValues[beatBlocks[i - 1]]) / 40f;

                if (tempoScores.ContainsKey(tempo))
                    tempoScores[tempo] += score;
                else
                    tempoScores[tempo] = score;
            }

            int candidateTempo = -1;
            float bestScore = 0f;
            foreach (var pair in tempoScores)
            {
                if (pair.Value > bestScore)
                {
                    bestScore = pair.Value;
                    candidateTempo = pair.Key;
                }
            }
            detectedTempo = candidateTempo;
        }

        if (finishButton != null) finishButton.interactable = true;
        if (statusText != null) statusText.text = "Finished";
    }

    private List<int> FindPotentialBeats(float[] rmsDbValues, int frequency, int blockSize)
    {
        List<int> beats = new List<int>();
        int windowBlocks = Mathf.Max(1, Mathf.RoundToInt(0.3f * frequency / blockSize));

        for (int i = 0; i < rmsDbValues.Length; i++)
        {
            if (rmsDbValues[i] <= -40f) continue;

            bool isPeak = true;
            int start = Mathf.Max(0, i - windowBlocks);
            int end = Mathf.Min(rmsDbValues.Length - 1, i + windowBlocks);

            for (int j = start; j <= end; j++)
            {
                if (j == i) continue;
                if (rmsDbValues[j] > rmsDbValues[i])
                {
                    isPeak = false;
                    break;
                }
            }

            if (isPeak)
            {
                beats.Add(i);
                i += windowBlocks;
            }
        }
        return beats;
    }

    private void OnFinishButtonClicked()
    {
        if (analysisPanel != null)
        {
            analysisPanel.gameObject.SetActive(false);
            analsisCanvas.gameObject.SetActive(false);
        }
        if (audioSource != null)
        {
            audioSource.timeSamples = 0;
            audioSource.Play();
            StartSection(0);
            isPlayingVisuals = true;
        }
    }

    private void StartSection(int sectionIdx)
    {
        if (importedAudioRanges == null || sectionIdx >= importedAudioRanges.Count)
            return;

        currentSectionIndex = sectionIdx;
        var range = importedAudioRanges[sectionIdx].range;
        sectionStartTime = range[0] / (float)audioSource.clip.frequency;
        sectionEndTime = range[1] / (float)audioSource.clip.frequency;

        beatInterval = detectedTempo > 0 ? 60f / detectedTempo : 0.5f;
        nextBeatTime = sectionStartTime;
        currentBeat = 0;
    }

    private void AdvanceSectionIfNeeded()
    {
        if (audioSource.time >= sectionEndTime)
        {
            int nextSection = currentSectionIndex + 1;
            if (nextSection < importedAudioRanges.Count)
            {
                StartSection(nextSection);
            }
            else
            {
                isPlayingVisuals = false;
            }
        }
    }

    private void AnimateLights()
    {
        float t = audioSource.time;

        if (t >= nextBeatTime)
        {
            currentBeat = (currentBeat + 1) % 4;
            nextBeatTime += beatInterval;
        }

        if (importedSections == null || currentSectionIndex >= importedSections.Count)
            return;

        var section = importedSections[currentSectionIndex];
        AnimateSceneLights(sceneLights, section.SceneLights, currentBeat, beatInterval, t - sectionStartTime);
        AnimateCeilingLights(ceilingLights, section.CeilingLights, currentBeat, beatInterval, t - sectionStartTime);
        AnimateSecondFloorLights(secondFloorLights, section.SecondFloorLights, currentBeat, beatInterval, t - sectionStartTime);
    }

    private void AnimateSceneLights(Light[] lights, EditorManger.SceneLightsData lightData, int beat, float beatInterval, float sectionTime)
    {
        if (lightData.Pulsing)
        {
            for (int i = 0; i < lights.Length; i++)
            {
                bool on = lightData.PulsingSequence[beat][i];
                if (lightData.Delayed != null && lightData.Delayed.Length > 0 && lightData.Delayed[0])
                {
                    float lightBeatTime = beatInterval * i / lights.Length;
                    on = ((sectionTime % (beatInterval * 4)) >= lightBeatTime) && on;
                }
                SetLightActive(lights[i], on);
            }
        }

        if (lightData.Rotation)
        {
            for (int i = 0; i < lights.Length; i++)
            {
                bool rotate = lightData.RotationSequence[i];
                float[] startAngles = rotate ? lightData.OnStartingAngle : lightData.OffStartingAngle;
                float[] angles = rotate ? lightData.OnAngle : lightData.OffAngle;
                float phase = Mathf.Sin(sectionTime * 2f * Mathf.PI / beatInterval);

                float angle = Mathf.Lerp(startAngles[0], angles[0], (phase + 1f) / 2f);
                lights[i].transform.localRotation = Quaternion.Euler(angle, 0, 0);
            }
        }

        if (!string.IsNullOrEmpty(lightData.Colour))
        {
            if (ColorUtility.TryParseHtmlString(lightData.Colour, out Color c))
            {
                foreach (var l in lights)
                    if (l != null) l.color = c;
            }
        }
    }

    private void AnimateCeilingLights(Light[] lights, EditorManger.CeilingLightsData lightData, int beat, float beatInterval, float sectionTime)
    {
        if (lightData.Pulsing)
        {
            for (int i = 0; i < lights.Length; i++)
            {
                bool on = lightData.PulsingSequence[beat][i];
                if (lightData.Delayed != null && lightData.Delayed.Length > 0 && lightData.Delayed[0])
                {
                    float lightBeatTime = beatInterval * i / lights.Length;
                    on = ((sectionTime % (beatInterval * 4)) >= lightBeatTime) && on;
                }
                SetLightActive(lights[i], on);
            }
        }

        if (lightData.Rotation)
        {
            for (int i = 0; i < lights.Length; i++)
            {
                bool rotate = lightData.RotationSequence[i];
                float[] startAngles = rotate ? lightData.OnStartingAngle : lightData.OffStartingAngle;
                float[] angles = rotate ? lightData.OnAngle : lightData.OffAngle;
                float phase = Mathf.Sin(sectionTime * 2f * Mathf.PI / beatInterval);

                float angle = Mathf.Lerp(startAngles[0], angles[0], (phase + 1f) / 2f);
                lights[i].transform.localRotation = Quaternion.Euler(0, angle, 0);
            }
        }

        if (!string.IsNullOrEmpty(lightData.Colour))
        {
            if (ColorUtility.TryParseHtmlString(lightData.Colour, out Color c))
            {
                foreach (var l in lights)
                    if (l != null) l.color = c;
            }
        }
    }

    private void AnimateSecondFloorLights(Light[] lights, EditorManger.SecondFloorLightsData lightData, int beat, float beatInterval, float sectionTime)
    {
        if (lightData.Pulsing)
        {
            for (int i = 0; i < lights.Length; i++)
            {
                bool on = lightData.PulsingSequence[beat][i];
                if (lightData.Delayed != null && lightData.Delayed.Length > 0 && lightData.Delayed[0])
                {
                    float lightBeatTime = beatInterval * i / lights.Length;
                    on = ((sectionTime % (beatInterval * 4)) >= lightBeatTime) && on;
                }
                SetLightActive(lights[i], on);
            }
        }

        if (lightData.Rotation)
        {
            for (int i = 0; i < lights.Length; i++)
            {
                bool rotate = lightData.RotationSequence[i];
                float[] startAngles = rotate ? lightData.OnStartingAngle : lightData.OffStartingAngle;
                float[] angles = rotate ? lightData.OnAngle : lightData.OffAngle;
                float phase = Mathf.Sin(sectionTime * 2f * Mathf.PI / beatInterval);

                float angle = Mathf.Lerp(startAngles[0], angles[0], (phase + 1f) / 2f);
                lights[i].transform.localRotation = Quaternion.Euler(0, 0, angle);
            }
        }

        if (!string.IsNullOrEmpty(lightData.Colour))
        {
            if (ColorUtility.TryParseHtmlString(lightData.Colour, out Color c))
            {
                foreach (var l in lights)
                    if (l != null) l.color = c;
            }
        }
    }
}