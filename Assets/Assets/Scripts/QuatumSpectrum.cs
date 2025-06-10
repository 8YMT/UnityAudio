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
        // 
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
        fftSize = Mathf.ClosestPowerOfTwo(fftSize);
    }

    #endregion

    #region Initialization

   /********************************************************************
    * ValidateInputSource: Validates whether the audiosource is valid or not *
    *                          if the audiosource isn't equal to null it returns true  *
    *                          else it returns false and debugs and error to the console log  *
    ********************************************************************/
    private bool ValidateInputSource()
    {
        if (inputAudioSource != null)
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

    /********************************************************************
    * StopChannels:  Returns an audioSource for both right and left channels*
    ********************************************************************/
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

    /********************************************************************
    * SplitAudioChannels:  Take the original Raw audio data and splits on two new arrays*
    *                      for left and right channels*
    ********************************************************************/
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

    /********************************************************************
    * CreateChannelClips:  Creates a new audioClip for both left and right channels seperatly *
    ********************************************************************/
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

    /********************************************************************
    * CleanupAudioClips:  Destorys left and right channels if not equal to null*
    ********************************************************************/
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

    /********************************************************************
    * InitializeSpectralAnalysis:  Allocating memory for the audio processing arrays*
    ********************************************************************/
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

    /********************************************************************
    * SpectralProccessing:  IEnumerator method (Coroutine)*
    *                       That would Start the audio Processing *
    *                       Waits until left and right channels are playing*
    *                       Gets their spectral data using GetSpectrumData*
    *                       Calls ProcessChannelSpectrum
    ********************************************************************/
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

    /********************************************************************
    * ProcessChannelSpectrum:  spectrum: the channels spectral data array*
    *                          smoothedSpecturm: smoothed channel's spectral data*
    *                          previousSamples: The previous spectral data fetch array
    *                          returns: Void
    *                          Fills smoothedSpectrum with the overlapping data using the current
    *                          and previous Spectral data Fetch
    ********************************************************************/
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

    #region Public Interface

    /********************************************************************
    * PlayChannels:  Plays the left and right audiosources *
    *                checks if both audiosources valid (not equal to null)*
    *                if valid it plays them*
    ********************************************************************/
    public void PlayChannels()
    {
        if (leftChannelSource != null) leftChannelSource.Play();
        if (rightChannelSource != null) rightChannelSource.Play();
    }

    /********************************************************************
    * StopChannels:  Stops the left and right audiosources from playing*
    *                checks if both audiosources valid (not equal to null)*
    *                if valid it stops them from playing*
    ********************************************************************/
    public void StopChannels()
    {
        if (leftChannelSource != null) leftChannelSource.Stop();
        if (rightChannelSource != null) rightChannelSource.Stop();
    }

    /********************************************************************
    * ToggleChannels: Checks if audiosources are playing*
    *                 if any of the channels are playing it calls StopsChannels*
    *                 if none of the channels are playing it calls PlayChannels*
    ********************************************************************/
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