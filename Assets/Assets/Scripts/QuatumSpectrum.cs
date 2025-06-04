using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class correlation : MonoBehaviour
{
    [Header("Channel Configuration")]
    [Tooltip("Main audio source to split into channels")]
    public AudioSource inputAudioSource;
    
    [Header("Output Settings")]
    [SerializeField] private bool autoPlayOnStart = true;
    [SerializeField] private bool createOutputSourcesAutomatically = true;
    
    [Header("Spectral Analysis")]
    [SerializeField] private FFTWindow fftWindow = FFTWindow.BlackmanHarris;
    [Range(64, 8192)] [SerializeField] private int fftSize = 1024;
    [Range(0, 100)] [SerializeField] private int overlapPercentage = 50;
    [Range(0.01f, 1f)] [SerializeField] private float spectrumSmoothingTime = 0.1f;

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
    
    // Analysis state
    private int overlapSamples;
    private int samplesProcessed = 0;
    private float frequencyResolution;
    private bool isProcessing = false;

    #region Unity Lifecycle

    void Start()
    {
        if (!ValidateInputSource()) return;
        
        InitializeAudioSystem();
        
        if (autoPlayOnStart)
        {
            PlayChannels();
        }
    }

    void OnDestroy()
    {
        CleanupAudioClips();
    }

    void OnValidate()
    {
        // Ensure FFT size is a power of two for optimal performance
        fftSize = Mathf.ClosestPowerOfTwo(fftSize);
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
        CreateOutputSources();
        SplitAudioChannels();
        CreateChannelClips();
        InitializeSpectralAnalysis();
        
        StartCoroutine(SpectralProcessing());
    }

    private void CreateOutputSources()
    {
        if (!createOutputSourcesAutomatically && 
           (leftChannelSource == null || rightChannelSource == null))
        {
            Debug.LogError("Output sources not assigned and auto-creation disabled!");
            return;
        }

        if (createOutputSourcesAutomatically)
        {
            leftChannelSource = CreateChannelSource("LeftChannelSource");
            rightChannelSource = CreateChannelSource("RightChannelSource");
        }
    }

    private AudioSource CreateChannelSource(string name)
    {
        var source = new GameObject(name).AddComponent<AudioSource>();
        source.transform.SetParent(transform);
        source.playOnAwake = false;
        source.outputAudioMixerGroup = inputAudioSource.outputAudioMixerGroup;
        source.spatialBlend = inputAudioSource.spatialBlend;
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
        if (leftChannelClip != null)
        {
            Destroy(leftChannelClip);
        }
        
        if (rightChannelClip != null)
        {
            Destroy(rightChannelClip);
        }
    }

    #endregion

    #region Spectral Analysis

    private void InitializeSpectralAnalysis()
    {
        leftSpectrum = new float[fftSize];
        rightSpectrum = new float[fftSize];
        leftSmoothedSpectrum = new float[fftSize];
        rightSmoothedSpectrum = new float[fftSize];

        overlapSamples = Mathf.FloorToInt(fftSize * overlapPercentage / 100f);
        leftPreviousSamples = new float[overlapSamples];
        rightPreviousSamples = new float[overlapSamples];

        frequencyResolution = inputAudioSource.clip.frequency / fftSize;
        isProcessing = true;
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
            }

            if (rightChannelSource.isPlaying)
            {
                rightChannelSource.GetSpectrumData(rightSpectrum, 0, fftWindow);
                ProcessChannelSpectrum(rightSpectrum, rightSmoothedSpectrum, rightPreviousSamples);
            }

            samplesProcessed++;
            yield return new WaitForSeconds(waitTime);
        }
    }

    private void ProcessChannelSpectrum(float[] spectrum, float[] smoothedSpectrum, float[] previousSamples)
    {
        // Apply overlap if we've processed at least one sample
        if (samplesProcessed > 0)
        {
            for (int i = 0; i < overlapSamples; i++)
            {
                spectrum[i] = (spectrum[i] + previousSamples[i]) * 0.5f;
            }
        }

        // Store current samples for next overlap
        System.Array.Copy(spectrum, previousSamples, overlapSamples);

        // Apply exponential smoothing
        float smoothingFactor = Mathf.Clamp01(Time.deltaTime / spectrumSmoothingTime);
        for (int i = 0; i < fftSize; i++)
        {
            smoothedSpectrum[i] = Mathf.Lerp(smoothedSpectrum[i], spectrum[i], smoothingFactor);
        }
    }

    #endregion

    #region Public Interface

    public void PlayChannels()
    {
        if (leftChannelSource != null) leftChannelSource.Play();
        if (rightChannelSource != null) rightChannelSource.Play();
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
    public float GetFrequencyResolution() => frequencyResolution;
    public AudioClip GetLeftChannelClip() => leftChannelClip;
    public AudioClip GetRightChannelClip() => rightChannelClip;
    public AudioSource GetLeftChannelSource() => leftChannelSource;
    public AudioSource GetRightChannelSource() => rightChannelSource;

    #endregion
}