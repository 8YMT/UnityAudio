using UnityEngine;
using UnityEngine.UI;

public class AudioSignalCalculator : MonoBehaviour
{
    public AudioSource audioSource; // Assign your audio source in the Inspector
    public Slider leftChannelSlider;
    public Slider rightChannelSlider;
    public Text leftChannelText;
    public Text rightChannelText;
    public Text overallPeakText;
    public Text leftChannelRMSText; // New UI text for left channel RMS
    public Text rightChannelRMSText; // New UI text for right channel RMS
    public Button resetPeaksButton; // New UI button to reset peaks

    [Header("Smoothing Settings")]
    [Range(0.1f, 1f)]
    public float sliderSmoothingFactor = 0.5f; // Controls the smoothness of the sliders

    [Range(0.001f, 0.99f)]
    public float rmsSmoothingFactorMin = 0.1f; // Minimum smoothing factor for RMS values
    [Range(0.001f, 0.99f)]
    public float rmsSmoothingFactorMax = 0.9f; // Maximum smoothing factor for RMS values
    private float currentRMSSmoothingFactor;

    [Header("RMS Averaging")]
    [SerializeField] private float rmsAverageWindowMs = 300f; // RMS averaging window in milliseconds
    private float[] leftChannelRMSHistory;
    private float[] rightChannelRMSHistory;
    private int rmsHistoryIndex = 0;
    private int rmsAverageWindowFrames; // Number of frames to average RMS over

    private const int sampleDataLength = 1024; // Number of samples to analyze per frame
    private float[] samples;

    // Peak values
    private float leftChannelPeak = -60f; // Initialize to silence
    private float rightChannelPeak = -60f;
    private float overallPeak = -60f;

    // Smoothed RMS values
    private float leftChannelRMSSmoothed = -60f;
    private float rightChannelRMSSmoothed = -60f;

    // Timer for dynamic smoothing
    private float smoothingTimer = 0f;
    private const float smoothingDuration = 2f; // Time to use max smoothing factor (in seconds)

    void Start()
    {
        samples = new float[sampleDataLength * audioSource.clip.channels];

        // Calculate the number of frames for the RMS averaging window
        float frameDurationMs = (sampleDataLength / (float)audioSource.clip.frequency) * 1000f;
        rmsAverageWindowFrames = Mathf.CeilToInt(rmsAverageWindowMs / frameDurationMs);

        // Initialize RMS history arrays
        leftChannelRMSHistory = new float[rmsAverageWindowFrames];
        rightChannelRMSHistory = new float[rmsAverageWindowFrames];

        // Set slider range to match dBFS range
        leftChannelSlider.minValue = -60f;
        leftChannelSlider.maxValue = 0f;
        rightChannelSlider.minValue = -60f;
        rightChannelSlider.maxValue = 0f;

        // Add listener to the reset peaks button
        if (resetPeaksButton != null)
        {
            resetPeaksButton.onClick.AddListener(ResetPeaksAndRMS);
        }

        // Start with max smoothing factor
        currentRMSSmoothingFactor = rmsSmoothingFactorMax;
    }

    void Update()
    {
        if (audioSource.isPlaying)
        {
            // Update the smoothing factor over time
            smoothingTimer += Time.deltaTime;
            if (smoothingTimer < smoothingDuration)
            {
                // Use max smoothing factor initially
                currentRMSSmoothingFactor = rmsSmoothingFactorMax;
            }
            else
            {
                // Transition to min smoothing factor
                currentRMSSmoothingFactor = Mathf.Lerp(currentRMSSmoothingFactor, rmsSmoothingFactorMin, Time.deltaTime);
            }

            // Get the most recent audio data
            audioSource.GetOutputData(samples, 0);

            // Calculate RMS for volume tracking (sliders)
            float leftChannelRMS = CalculateRMS(samples, 0);
            float rightChannelRMS = CalculateRMS(samples, 1);

            // Convert RMS to dBFS for sliders and RMS text
            float leftDBFS = 20 * Mathf.Log10(leftChannelRMS);
            float rightDBFS = 20 * Mathf.Log10(rightChannelRMS);

            // Handle silence (avoid -Infinity)
            if (float.IsNegativeInfinity(leftDBFS)) leftDBFS = -60f;
            if (float.IsNegativeInfinity(rightDBFS)) rightDBFS = -60f;

            // Store RMS values in history
            leftChannelRMSHistory[rmsHistoryIndex] = leftDBFS;
            rightChannelRMSHistory[rmsHistoryIndex] = rightDBFS;
            rmsHistoryIndex = (rmsHistoryIndex + 1) % rmsAverageWindowFrames;

            // Calculate averaged RMS values
            float leftRMSAverage = CalculateAverage(leftChannelRMSHistory);
            float rightRMSAverage = CalculateAverage(rightChannelRMSHistory);

            // Smooth RMS values separately using the current smoothing factor
            leftChannelRMSSmoothed = Smooth(leftRMSAverage, leftChannelRMSSmoothed, currentRMSSmoothingFactor);
            rightChannelRMSSmoothed = Smooth(rightRMSAverage, rightChannelRMSSmoothed, currentRMSSmoothingFactor);

            // Smoothly update slider values using Lerp
            leftChannelSlider.value = Mathf.Lerp(leftChannelSlider.value, leftRMSAverage, sliderSmoothingFactor);
            rightChannelSlider.value = Mathf.Lerp(rightChannelSlider.value, rightRMSAverage, sliderSmoothingFactor);

            // Update RMS text with smoothed values
            leftChannelRMSText.text = $"{leftChannelRMSSmoothed:F2}";
            rightChannelRMSText.text = $"{rightChannelRMSSmoothed:F2}";

            // Calculate peaks directly from samples
            CalculatePeaks();

            // Update UI text for peaks
            leftChannelText.text = $"{leftChannelPeak:F2}";
            rightChannelText.text = $"{rightChannelPeak:F2}";
            overallPeakText.text = $"{overallPeak:F2}";
        }
    }

    // Helper method to calculate RMS for a specific channel
    private float CalculateRMS(float[] samples, int channel)
    {
        float sum = 0f;
        int channelCount = audioSource.clip.channels;

        for (int i = channel; i < samples.Length; i += channelCount)
        {
            sum += samples[i] * samples[i];
        }

        return Mathf.Sqrt(sum / (samples.Length / channelCount));
    }

    // Helper method to calculate peaks directly from samples
    private void CalculatePeaks()
    {
        float frameLeftPeak = -60f;
        float frameRightPeak = -60f;

        for (int i = 0; i < samples.Length; i += audioSource.clip.channels)
        {
            float leftSample = Mathf.Abs(samples[i]);
            float rightSample = Mathf.Abs(samples[i + 1]);

            // Convert sample values to dBFS, handling silence
            float leftDBFS = leftSample > 0 ? 20 * Mathf.Log10(leftSample) : -60f;
            float rightDBFS = rightSample > 0 ? 20 * Mathf.Log10(rightSample) : -60f;

            // Update frame peaks
            if (leftDBFS > frameLeftPeak) frameLeftPeak = leftDBFS;
            if (rightDBFS > frameRightPeak) frameRightPeak = rightDBFS;
        }

        // Update overall peaks
        if (frameLeftPeak > leftChannelPeak) leftChannelPeak = frameLeftPeak;
        if (frameRightPeak > rightChannelPeak) rightChannelPeak = frameRightPeak;
        overallPeak = Mathf.Max(leftChannelPeak, rightChannelPeak);
    }

    // Method to reset peaks and RMS when the button is clicked
    public void ResetPeaksAndRMS()
    {
        // Reset peaks
        leftChannelPeak = -60f;
        rightChannelPeak = -60f;
        overallPeak = -60f;

        // Reset RMS history and smoothed values
        leftChannelRMSSmoothed = -60f;
        rightChannelRMSSmoothed = -60f;
        leftChannelRMSHistory = new float[rmsAverageWindowFrames];
        rightChannelRMSHistory = new float[rmsAverageWindowFrames];
        rmsHistoryIndex = 0;

        // Reset smoothing timer and use max smoothing factor
        smoothingTimer = 0f;
        currentRMSSmoothingFactor = rmsSmoothingFactorMax;

        // Update UI text immediately after resetting
        leftChannelText.text = $"{leftChannelPeak:F2}";
        rightChannelText.text = $"{rightChannelPeak:F2}";
        overallPeakText.text = $"{overallPeak:F2}";
        leftChannelRMSText.text = $"{leftChannelRMSSmoothed:F2}";
        rightChannelRMSText.text = $"{rightChannelRMSSmoothed:F2}";
    }

    // Exponential smoothing for RMS values
    private float Smooth(float current, float previous, float smoothingFactor)
    {
        return previous + smoothingFactor * (current - previous);
    }

    // Calculate the average of an array of RMS values
    private float CalculateAverage(float[] rmsHistory)
    {
        float sum = 0f;
        for (int i = 0; i < rmsHistory.Length; i++)
        {
            sum += rmsHistory[i];
        }
        return sum / rmsHistory.Length;
    }
}