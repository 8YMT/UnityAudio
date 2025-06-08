using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class Correlation : MonoBehaviour
{
    [Header("Channel Configuration")]
    [Tooltip("Main audio source to split into channels")]
    public AudioSource inputAudioSource;
    private AudioClip audioClip;
    private AudioClip currentInputClip;
    
    [Header("Output Settings")]
    [SerializeField] private bool autoPlayOnStart = false;
    [SerializeField] private bool createOutputSourcesAutomatically = true;
    [SerializeField] private AudioMixerGroup analysisMixerGroup;
    
    [Header("Spectral Analysis")]
    [SerializeField] private FFTWindow fftWindow = FFTWindow.BlackmanHarris;
    [Range(64, 8192)] [SerializeField] private int fftSize = 1024;
    [Range(0, 100)] [SerializeField] private int overlapPercentage = 50;
    [Range(0.01f, 1f)] [SerializeField] private float spectrumSmoothingTime = 0.1f;
    [SerializeField] private float globalOffset = 1f;

    [Header("Peak Detection")]
    [SerializeField] private bool detectPeakFrequency = true;
    [SerializeField] private float minPeakThreshold = 0.01f;
    [SerializeField] private bool logPeaksToConsole = false;

    [Header("Correlation Analysis")]
    [SerializeField] private bool calculateCorrelation = true;
    [SerializeField] private float correlationUpdateRate = 30f;
    [Range(-1f, 1f)] [SerializeField] private float correlationValue = 0f;
    [SerializeField] private float correlationSmoothing = 0.2f;
    [SerializeField] private float phaseIssueThreshold = -0.7f;

    [Header("Visualization")]
    [SerializeField] private Image correlationMeter;
    [SerializeField] private Color positiveColor = Color.green;
    [SerializeField] private Color negativeColor = Color.red;
    [SerializeField] private Color neutralColor = Color.yellow;
    [SerializeField] private bool showBackgroundLines = true;
    [SerializeField] private Color bgLineColor = new Color(0.38f, 0f, 0f, 0.5f);

    [Header("Correction Settings")]
    [SerializeField] private float lpfCutoff = 10f; // Hz
    [SerializeField] private float lpfQ = 0.707f;

    public RectTransform image;
    private const float MinValue = -1f;
    private const float MaxValue = 1f;
    private const float MinPosition = -115f;
    private const float MaxPosition = 115f;
    
    // Add these private variables
    private float leftSquared;
    private float rightSquared;
    private float product;
    private float leftRms;
    private float rightRms;
    private float productRms;
    private float rawCorrelation;
    
    // Low-pass filter state variables
    private float lpfLeftSquared;
    private float lpfRightSquared;
    private float lpfProduct;
    private float lpfOutput;
     // Channel components
    private AudioSource leftChannelSource;
    private AudioSource rightChannelSource;
    private AudioClip leftChannelClip;
    private AudioClip rightChannelClip;

    // Audio data
    private float[] leftChannelSamples;
    private float[] rightChannelSamples;
    
    // Spectrum data
    private float[] leftSpectrum;
    private float[] rightSpectrum;
    private float[] leftSmoothedSpectrum;
    private float[] rightSmoothedSpectrum;
    private float[] leftPreviousSamples;
    private float[] rightPreviousSamples;
    private float[] averagedSpectrum;
    private float[] correlationBuffer;
    
    // Peak detection
    private float leftPeakFrequency = 0f;
    private float rightPeakFrequency = 0f;
    private float leftPeakMagnitude = 0f;
    private float rightPeakMagnitude = 0f;
    
    // Correlation data
    private float smoothedCorrelation = 0f;
    private Queue<float> correlationHistory = new Queue<float>();
    
    // Analysis state
    private int overlapSamples;
    private int samplesProcessed = 0;
    private float frequencyResolution;
    private bool isProcessing = false;
    private Coroutine spectralCoroutine;
    private Coroutine correlationCoroutine;
    private bool isDragging;
    
    #region Unity Lifecycle

     void Start()
{
    if (!ValidateInputSource()) return;
    
    currentInputClip = inputAudioSource.clip;
    InitializeAudioSystem();
    
    if (autoPlayOnStart)
    {
        PlayChannels();
    }
}

    void Update()
{
    // Check for clip change
    if (inputAudioSource.clip != currentInputClip)
    {
        HandleClipChange();
    }

    if (logPeaksToConsole && detectPeakFrequency)
    {
        // Existing peak logging code
    }
    
    // Rest of the existing Update code...
    if (inputAudioSource.isPlaying)
    {
        if (!leftChannelSource.isPlaying)
        {
            leftChannelSource.Play();
            rightChannelSource.Play();
            leftChannelSource.time = inputAudioSource.time;
            rightChannelSource.time = inputAudioSource.time;
        }
    }
    if (!inputAudioSource.isPlaying)
    {
        leftChannelSource.Stop();
        rightChannelSource.Stop();
    }
    UpdateCorrelationVisualization();

    float newX = Mathf.Lerp(MinPosition, MaxPosition, Mathf.InverseLerp(MinValue, MaxValue, correlationValue));
    image.anchoredPosition = new Vector2(newX, image.anchoredPosition.y);
}

private void HandleClipChange()
{
    // Clean up existing clips
    CleanupAudioClips();
    
    // Stop any active processing
    if (spectralCoroutine != null)
    {
        StopCoroutine(spectralCoroutine);
    }
    if (correlationCoroutine != null)
    {
        StopCoroutine(correlationCoroutine);
    }
    
    // Update the current clip reference
    currentInputClip = inputAudioSource.clip;
    
    // Reinitialize everything with the new clip
    InitializeAudioSystem();
    
    // Restart processing if needed
    if (autoPlayOnStart || inputAudioSource.isPlaying)
    {
        PlayChannels();
    }
    
    spectralCoroutine = StartCoroutine(SpectralProcessing());
    if (calculateCorrelation)
    {
        correlationCoroutine = StartCoroutine(CalculateCorrelation());
    }
}

    public void OnSliderDragStart()
    {
        leftChannelSource.Stop();
        rightChannelSource.Stop();
    }


    // Called when dragging ends
    public void OnSliderDragEnd()
    {
        leftChannelSource.Play();
        rightChannelSource.Play();

        leftChannelSource.time = inputAudioSource.time;
        rightChannelSource.time = inputAudioSource.time;

    }

    void OnDestroy()
    {
        CleanupAudioClips();
        StopAllCoroutines();
    }

    void OnValidate()
    {
        fftSize = Mathf.ClosestPowerOfTwo(fftSize);
        minPeakThreshold = Mathf.Clamp(minPeakThreshold, 0.0001f, 1f);
    }

    #endregion

    #region Initialization

    private bool ValidateInputSource()
    {
        if (inputAudioSource == null)
        {
            inputAudioSource = GetComponent<AudioSource>();
        }

        if (inputAudioSource == null || inputAudioSource.clip == null)
        {
            Debug.LogError("AudioChannelProcessor: No valid AudioSource with AudioClip found!");
            return false;
        }

        return true;
    }

    private void InitializeAudioSystem()
{
    // Only create sources if they don't exist
    if (leftChannelSource == null || rightChannelSource == null)
    {
        CreateOutputSources();
    }
    
    SplitAudioChannels();
    CreateChannelClips();
    InitializeSpectralAnalysis();
    
    spectralCoroutine = StartCoroutine(SpectralProcessing());
}

    private void CreateOutputSources()
    {
        if (createOutputSourcesAutomatically)
        {
            leftChannelSource = CreateChannelSource("LeftChannelSource");
            rightChannelSource = CreateChannelSource("RightChannelSource");

            leftChannelSource.volume = 1f;
            rightChannelSource.volume = 1f;

            if (analysisMixerGroup != null)
            {
                leftChannelSource.outputAudioMixerGroup = analysisMixerGroup;
                rightChannelSource.outputAudioMixerGroup = analysisMixerGroup;
                analysisMixerGroup.audioMixer.SetFloat("Volume", -80f);
            }
            else
            {
                Debug.LogWarning("No analysis mixer group assigned - audio will be audible!");
            }
        }
    }

    private AudioSource CreateChannelSource(string name)
    {
        var source = new GameObject(name).AddComponent<AudioSource>();
        source.transform.SetParent(transform);
        source.playOnAwake = false;
        source.spatialBlend = inputAudioSource.spatialBlend;
        source.dopplerLevel = inputAudioSource.dopplerLevel;
        source.rolloffMode = inputAudioSource.rolloffMode;
        source.minDistance = inputAudioSource.minDistance;
        source.maxDistance = inputAudioSource.maxDistance;
        return source;
    }

    #endregion

    #region Audio Processing

    private void SplitAudioChannels()
    {
        AudioClip inputClip = inputAudioSource.clip;
        int totalSamples = inputClip.samples;
        int channels = inputClip.channels;
        float[] allSamples = new float[totalSamples * channels];
        
        inputClip.GetData(allSamples, 0);

        leftChannelSamples = new float[totalSamples];
        rightChannelSamples = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            leftChannelSamples[i] = allSamples[i * channels];
            rightChannelSamples[i] = channels > 1 ? allSamples[i * channels + 1] : leftChannelSamples[i];
        }
    }

    private void CreateChannelClips()
    {
        AudioClip inputClip = inputAudioSource.clip;
        
        leftChannelClip = AudioClip.Create(
            $"{inputClip.name}_LeftChannel",
            inputClip.samples,
            1,
            inputClip.frequency,
            false
        );
        leftChannelClip.SetData(leftChannelSamples, 0);
        
        rightChannelClip = AudioClip.Create(
            $"{inputClip.name}_RightChannel",
            inputClip.samples,
            1,
            inputClip.frequency,
            false
        );
        rightChannelClip.SetData(rightChannelSamples, 0);

        leftChannelSource.clip = leftChannelClip;
        rightChannelSource.clip = rightChannelClip;
    }

    private void CleanupAudioClips()
    {
        if (leftChannelClip != null) Destroy(leftChannelClip);
        if (rightChannelClip != null) Destroy(rightChannelClip);
    }

    #endregion

    #region Spectral Analysis

    private void InitializeSpectralAnalysis()
    {
        leftSpectrum = new float[fftSize];
        rightSpectrum = new float[fftSize];
        leftSmoothedSpectrum = new float[fftSize];
        rightSmoothedSpectrum = new float[fftSize];
        averagedSpectrum = new float[fftSize];
        correlationBuffer = new float[fftSize];

        overlapSamples = Mathf.FloorToInt(fftSize * overlapPercentage / 100f);
        leftPreviousSamples = new float[overlapSamples];
        rightPreviousSamples = new float[overlapSamples];

        frequencyResolution = inputAudioSource.clip.frequency / (float)fftSize;
        isProcessing = true;

        if (calculateCorrelation)
        {
            if (correlationCoroutine != null) StopCoroutine(correlationCoroutine);
            correlationCoroutine = StartCoroutine(CalculateCorrelation());
        }
    }

    private IEnumerator SpectralProcessing()
    {
        while (isProcessing)
        {
            if (!leftChannelSource.isPlaying && !rightChannelSource.isPlaying)
            {
                yield return new WaitForSeconds(0.1f);
                continue;
            }

            float waitTime = (fftSize - overlapSamples) / (float)inputAudioSource.clip.frequency;

            if (leftChannelSource.isPlaying)
            {
                leftChannelSource.GetSpectrumData(leftSpectrum, 0, fftWindow);
                ProcessChannelSpectrum(leftSpectrum, leftSmoothedSpectrum, leftPreviousSamples);
                
                if (detectPeakFrequency)
                {
                    leftPeakFrequency = FindPeakFrequency(leftSmoothedSpectrum);
                    leftPeakMagnitude = leftSmoothedSpectrum[Mathf.FloorToInt(leftPeakFrequency / frequencyResolution)];
                }
            }

            if (rightChannelSource.isPlaying)
            {
                rightChannelSource.GetSpectrumData(rightSpectrum, 0, fftWindow);
                ProcessChannelSpectrum(rightSpectrum, rightSmoothedSpectrum, rightPreviousSamples);
                
                if (detectPeakFrequency)
                {
                    rightPeakFrequency = FindPeakFrequency(rightSmoothedSpectrum);
                    rightPeakMagnitude = rightSmoothedSpectrum[Mathf.FloorToInt(rightPeakFrequency / frequencyResolution)];
                }
            }

            samplesProcessed++;
            yield return new WaitForSeconds(waitTime);
        }
    }

    private float FindPeakFrequency(float[] spectrum)
    {
        float maxMagnitude = 0f;
        int peakBin = 0;

        for (int i = 0; i < fftSize; i++)
        {
            if (spectrum[i] > maxMagnitude)
            {
                maxMagnitude = spectrum[i];
                peakBin = i;
            }
        }

        float peakFrequency = InterpolatePeakFrequency(spectrum, peakBin);
        peakFrequency *= Mathf.Pow(2, -globalOffset);

        return peakFrequency;
    }

    private float InterpolatePeakFrequency(float[] spectrum, int peakBin)
    {
        if (peakBin <= 0 || peakBin >= fftSize - 1)
        {
            return peakBin * frequencyResolution;
        }

        float dL = spectrum[peakBin - 1];
        float dC = spectrum[peakBin];
        float dR = spectrum[peakBin + 1];

        float offset = (dL - dR) / (2 * (dL - 2 * dC + dR));
        return (peakBin + offset) * frequencyResolution;
    }

    private void ProcessChannelSpectrum(float[] spectrum, float[] smoothedSpectrum, float[] previousSamples)
    {
        if (samplesProcessed > 0)
        {
            for (int i = 0; i < overlapSamples; i++)
            {
                spectrum[i] = (spectrum[i] + previousSamples[i]) * 0.5f;
            }
        }

        System.Array.Copy(spectrum, previousSamples, overlapSamples);

        float smoothingFactor = Mathf.Clamp01(Time.deltaTime / spectrumSmoothingTime);
        for (int i = 0; i < fftSize; i++)
        {
            smoothedSpectrum[i] = Mathf.Lerp(smoothedSpectrum[i], spectrum[i], smoothingFactor);
        }
    }

    #endregion

    #region Correlation Analysis

    // Modify the CalculateCorrelation coroutine
    private IEnumerator CalculateCorrelation()
{
    float[] leftBuffer = new float[fftSize];
    float[] rightBuffer = new float[fftSize];
    
    // Calculate filter coefficients once
    float dt = 1f / correlationUpdateRate;
    float rc = 1f / (2f * Mathf.PI * lpfCutoff);
    float alpha = dt / (rc + dt);
    
    while (isProcessing)
    {
        if (!leftChannelSource.isPlaying || !rightChannelSource.isPlaying)
        {
            yield return new WaitForSeconds(1f / correlationUpdateRate);
            continue;
        }

        // 1. Get time domain samples
        leftChannelSource.GetOutputData(leftBuffer, 0);
        rightChannelSource.GetOutputData(rightBuffer, 0);
        
        // 2. Calculate instantaneous energy and product
        leftSquared = 0f;
        rightSquared = 0f;
        product = 0f;
        
        for (int i = 0; i < fftSize; i++)
        {
            leftSquared += leftBuffer[i] * leftBuffer[i];
            rightSquared += rightBuffer[i] * rightBuffer[i];
            product += leftBuffer[i] * rightBuffer[i];
        }
        
        // 3. Apply first-stage low-pass filters
        lpfLeftSquared = LowPassFilter(lpfLeftSquared, leftSquared / fftSize, alpha);
        lpfRightSquared = LowPassFilter(lpfRightSquared, rightSquared / fftSize, alpha);
        lpfProduct = LowPassFilter(lpfProduct, product / fftSize, alpha);
        
        // 4. Calculate RMS values
        leftRms = Mathf.Sqrt(lpfLeftSquared);
        rightRms = Mathf.Sqrt(lpfRightSquared);
        
        // 5. Final correlation calculation with sign preserved
        if (leftRms > 0.0001f && rightRms > 0.0001f)
        {
            rawCorrelation = lpfProduct / (leftRms * rightRms);
            rawCorrelation = Mathf.Clamp(rawCorrelation, -1f, 1f);

            // Apply final low-pass to output
            lpfOutput = LowPassFilter(lpfOutput, rawCorrelation, alpha);
            correlationValue = lpfOutput;

            // Smooth for visualization
            smoothedCorrelation = Mathf.Lerp(smoothedCorrelation, correlationValue, 
                Mathf.Clamp01(Time.deltaTime / correlationSmoothing));

            // Maintain history
            correlationHistory.Enqueue(smoothedCorrelation);
            if (correlationHistory.Count > 100) correlationHistory.Dequeue();
        }

        yield return new WaitForSeconds(1f / correlationUpdateRate);
    }
}


    private float LowPassFilter(float current, float input, float alpha)
    {
        return current + alpha * (input - current);
    }
 private void UpdateCorrelationVisualization()
    {
        if (correlationMeter != null)
        {
            // Update meter fill amount based on correlation (-1 to 1 mapped to 0-1)
            float t = (smoothedCorrelation + 1f) * 0.5f;
            correlationMeter.fillAmount = t;
            
            // Update color based on correlation value
            if (smoothedCorrelation > 0.5f) correlationMeter.color = positiveColor;
            else if (smoothedCorrelation < -0.5f) correlationMeter.color = negativeColor;
            else correlationMeter.color = neutralColor;
        }
    }

    private void DrawBackgroundLines()
    {
        if (!showBackgroundLines || correlationMeter == null) return;

        // Create a temporary texture for drawing
        var rt = RenderTexture.GetTemporary(256, 256);
        var tex = new Texture2D(256, 256);
        
        // Draw lines (simplified version of the JS implementation)
        // This would need to be implemented using Unity's GUI system or a custom shader
        // for proper visualization
        
        RenderTexture.ReleaseTemporary(rt);
    }

    #endregion

    #region Public Interface

    public void PlayChannels()
    {
        if (leftChannelSource != null) {
            leftChannelSource.Play();
            leftChannelSource.time = inputAudioSource.time;
        }
        if (rightChannelSource != null){ 
            rightChannelSource.Play();
            rightChannelSource.time = inputAudioSource.time;
        }
    }

    public void StopChannels()
    {
        if (leftChannelSource != null) leftChannelSource.Stop();
        if (rightChannelSource != null) rightChannelSource.Stop();
    }

    public void ToggleChannels()
    {
        if (leftChannelSource.isPlaying || rightChannelSource.isPlaying)
        {
            StopChannels();
        }
        else
        {
            PlayChannels();
        }
    }

    public float[] GetLeftSpectrum(bool smoothed = true) => smoothed ? leftSmoothedSpectrum : leftSpectrum;
    public float[] GetRightSpectrum(bool smoothed = true) => smoothed ? rightSmoothedSpectrum : rightSpectrum;
    public float GetLeftPeakFrequency() => leftPeakFrequency;
    public float GetRightPeakFrequency() => rightPeakFrequency;
    public float GetLeftPeakMagnitude() => leftPeakMagnitude;
    public float GetRightPeakMagnitude() => rightPeakMagnitude;
    public float GetFrequencyResolution() => frequencyResolution;
    public float GetCorrelation(bool smoothed = true) => smoothed ? smoothedCorrelation : correlationValue;
    public float[] GetCorrelationHistory() => correlationHistory.ToArray();
    public AudioClip GetLeftChannelClip() => leftChannelClip;
    public AudioClip GetRightChannelClip() => rightChannelClip;
    public AudioSource GetLeftChannelSource() => leftChannelSource;
    public AudioSource GetRightChannelSource() => rightChannelSource;

    #endregion
}