using UnityEngine;
using UnityEngine.UI;

public class CrestFactor : MonoBehaviour
{
    [Header("Audio Source")]
    public AudioSource audioSource;
    private AudioClip audioClip;


    [Header("Crest Factor Settings")]
    [Tooltip("RMS window size (seconds) for Crest Factor calculation")]
    [Range(0.01f, 1.0f)] public float rmsWindowSize = 0.05f; // 50ms default
    [Tooltip("Overlap percentage between windows (0-100%)")]
    [Range(0, 100)] public int overlapPercentage = 75;


    [Header("UI Display")]

    public Text maxRightCrestFactorText;
    public Text maxLeftCrestFactorText;

    [Tooltip("Display Crest Factor in dB")]
    public bool displayInDecibels = true;

    // Internal variables
    private float[] samples;
    private float[] leftChannel;
    private float[] rightChannel;
    private float sampleRate;
    private int rmsWindowSamples;
    private int hopSize;
    private int totalBlocks;

    private float[] crestFactorLeft;
    private float[] crestFactorRight;
    private float[] maxPeakLeft;
    private float[] maxPeakRight;
    private float[] rmsLeft;
    private float[] rmsRight;
    
    // Current position tracking
    private int currentBlockIndex = 0;
    private float maxCrestLeft = 0;
    private float maxCrestRight = 0;

    void Start()
    {
        InitializeAudioProcessing();
    }

    void Update()
    {

        if (!audioSource.isPlaying) return;
        if (crestFactorLeft == null || crestFactorLeft.Length == 0) return;

        // Get current playback position in samples
        int currentSample = audioSource.timeSamples;


        // Calculate current block index based on sample position
        int newBlockIndex = Mathf.FloorToInt(currentSample / (float)hopSize);

        // Only update if we've moved to a new block
        if (newBlockIndex != currentBlockIndex && newBlockIndex < crestFactorLeft.Length)
        {
            currentBlockIndex = newBlockIndex;
            UpdateDisplay();
        }
    }

    void InitializeAudioProcessing()
    {
        if (audioSource == null || audioSource.clip == null)
        {
            Debug.LogError("AudioSource or AudioClip missing!");
            return;
        }

        audioClip = audioSource.clip;
        sampleRate = audioClip.frequency;
        rmsWindowSamples = Mathf.RoundToInt(rmsWindowSize * sampleRate);
        hopSize = Mathf.Max(1, rmsWindowSamples * (100 - overlapPercentage) / 100);

        // Load audio data
        samples = new float[audioClip.samples * audioClip.channels];
        audioClip.GetData(samples, 0);

        // Split channels
        SplitChannels();

        // Pre-calculate Crest Factor for entire audio
        PrecalculateCrestFactor();

        // Reset tracking variables
        currentBlockIndex = 0;
        maxCrestLeft = 0;
        maxCrestRight = 0;
    }

    void SplitChannels()
    {
        int numSamples = samples.Length / audioClip.channels;
        leftChannel = new float[numSamples];
        rightChannel = new float[numSamples];

        for (int i = 0, j = 0; i < samples.Length; i += audioClip.channels, j++)
        {
            leftChannel[j] = samples[i];
            rightChannel[j] = (audioClip.channels > 1) ? samples[i + 1] : samples[i];
        }
    }

    void PrecalculateCrestFactor()
    {
        totalBlocks = Mathf.Max(1, (leftChannel.Length - rmsWindowSamples) / hopSize + 1);
        crestFactorLeft = new float[totalBlocks];
        crestFactorRight = new float[totalBlocks];
        maxPeakLeft = new float[totalBlocks];
        maxPeakRight = new float[totalBlocks];
        rmsLeft = new float[totalBlocks];
        rmsRight = new float[totalBlocks];

        for (int block = 0; block < totalBlocks; block++)
        {
            int startIdx = block * hopSize;
            float leftPeak = 0f;
            float rightPeak = 0f;
            float leftSumSquares = 0f;
            float rightSumSquares = 0f;

            // Calculate peak and RMS for the current block
            for (int i = 0; i < rmsWindowSamples; i++)
            {
                int sampleIdx = startIdx + i;
                if (sampleIdx >= leftChannel.Length) break;

                float leftSample = leftChannel[sampleIdx];
                float rightSample = rightChannel[sampleIdx];
                
                leftPeak = Mathf.Max(leftPeak, Mathf.Abs(leftSample));
                rightPeak = Mathf.Max(rightPeak, Mathf.Abs(rightSample));
                leftSumSquares += leftSample * leftSample;
                rightSumSquares += rightSample * rightSample;
            }

            float leftRms = Mathf.Sqrt(leftSumSquares / rmsWindowSamples);
            float rightRms = Mathf.Sqrt(rightSumSquares / rmsWindowSamples);
            
            crestFactorLeft[block] = (leftRms > 0) ? leftPeak / leftRms : 1f;
            crestFactorRight[block] = (rightRms > 0) ? rightPeak / rightRms : 1f;
            
            maxPeakLeft[block] = leftPeak;
            maxPeakRight[block] = rightPeak;
            rmsLeft[block] = leftRms;
            rmsRight[block] = rightRms;
        }
    }

    void UpdateDisplay()
    {
        if (currentBlockIndex >= totalBlocks) return;

        float currentLeftCF = crestFactorLeft[currentBlockIndex];
        float currentRightCF = crestFactorRight[currentBlockIndex];

        // Update max crest factors
        if (currentLeftCF > maxCrestLeft)
        {
            maxCrestLeft = currentLeftCF;
            maxLeftCrestFactorText.text = FormatCrestFactor(maxCrestLeft);
        }

        if (currentRightCF > maxCrestRight)
        {
            maxCrestRight = currentRightCF;
            maxRightCrestFactorText.text = FormatCrestFactor(maxCrestRight);
        }
    }

    string FormatCrestFactor(float value)
    {
        if (displayInDecibels)
        {
            return (20f * Mathf.Log10(value)).ToString("F1") + " dB";
        }
        return value.ToString("F2");
    }
}