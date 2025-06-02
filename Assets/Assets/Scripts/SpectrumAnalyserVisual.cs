using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class SpectrumAnalyzerVisual : MonoBehaviour
{
    public AudioSource audioSource;
    [Header("FFT Settings")]
    [SerializeField] private FFTWindow fftWindow = FFTWindow.BlackmanHarris;
    [SerializeField] private int fftSize = 8192; // Block size
    [SerializeField][Range(0, 100)] private int overlapPercentage = 75; // Overlap percentage
    [SerializeField] private float avgTime = 1f; // Average time in seconds

    [Header("Display Settings")]
    [SerializeField] private float freqLow = 20f; // Lowest frequency to display (Hz)
    [SerializeField] private float freqHigh = 20000f; // Highest frequency to display (Hz)
    [SerializeField] private float rangeLow = -120f; // Lowest amplitude to display (dB)
    [SerializeField] private float rangeHigh = 0f; // Highest amplitude to display (dB)
    [SerializeField] private float slope = 0f; // Slope in dB/octave

    [Header("Calibration Settings")]
    [SerializeField][Range(-1f, 1f)] private float globalOffset = 0f; // Global frequency offset

    [Header("Spectrum Visualization")]
    [SerializeField] private RawImage spectrumImage;
    [SerializeField] private Vector2 textureSize = new Vector2(512, 256);
    [SerializeField] private Color backgroundColor = Color.black;
    [SerializeField] private Color lineColor = Color.white;
    [SerializeField] private int lineThickness = 2;
    [SerializeField] private Gradient spectrumGradient;

    [Header("UI Settings")]
    [SerializeField] private Text peakFrequencyText; // Reference to the UI Text element
    [SerializeField] private Text closestNoteText; // Reference to the UI Text element for the closest note
    [SerializeField] private Text accuracyText; // Reference to the UI Text element for the accuracy in cents

    private Texture2D spectrumTexture;
    private Color[] spectrumPixels;
    private float[] spectrumData;
    private float[] averagedSpectrum;
    private float sampleRate;
    private float frequencyResolution;
    private float[] binIndices;

    private int overlapSamples;
    private float[] previousSamples;
    private int samplesProcessed = 0;

    private float peakFrequency = 0f; // Track the peak frequency


    private AudioClip Clip;
    
    void Update()
    {

    }
    // Dictionary for note frequencies in the 0th octave
    private Dictionary<string, float> noteFrequencies = new Dictionary<string, float>()
    {
        {"C", 16.35f},
        {"C#", 17.32f},
        {"D", 18.35f},
        {"D#", 19.45f},
        {"E", 20.60f},
        {"F", 21.83f},
        {"F#", 23.12f},
        {"G", 24.50f},
        {"G#", 25.96f},
        {"A", 27.50f},
        {"A#", 29.14f},
        {"B", 30.87f}
    };

    void Start()
    {
        if (!InitializeComponents())
        {
            Debug.LogError("Initialization failed. Check inspector assignments.");
            enabled = false;
            return;
        } 
       

        InitializeArrays();
        CalculateFrequencyParameters();
        InitializeTexture();
        Clip = audioSource.clip;
        

        StartCoroutine(SpectralAnalysis());
    }

    bool InitializeComponents()
    {
        if (audioSource == null)
        {
            Debug.LogError("AudioSource not assigned.");
            return false;
        }

        if (spectrumImage == null)
        {
            Debug.LogError("Spectrum Image not assigned.");
            return false;
        }

        if (peakFrequencyText == null || closestNoteText == null || accuracyText == null)
        {
            Debug.LogError("UI Text elements not assigned.");
            return false;
        }

        return true;
    }

    void InitializeArrays()
    {
        spectrumData = new float[fftSize];
        averagedSpectrum = new float[fftSize];
        overlapSamples = Mathf.FloorToInt(fftSize * overlapPercentage / 100f);
        previousSamples = new float[overlapSamples];
    }

    void CalculateFrequencyParameters()
    {
        sampleRate = AudioSettings.outputSampleRate;
        frequencyResolution = sampleRate / fftSize;

        binIndices = new float[(int)textureSize.x];
        for (int x = 0; x < (int)textureSize.x; x++)
        {
            float normalizedX = (float)x / ((int)textureSize.x - 1);
            float targetFrequency = Mathf.Pow(10, normalizedX * Mathf.Log10(freqHigh / freqLow)) * freqLow; // Logarithmic scale
            binIndices[x] = targetFrequency / frequencyResolution;
        }
    }

    void InitializeTexture()
    {
        spectrumTexture = new Texture2D((int)textureSize.x, (int)textureSize.y, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        spectrumImage.texture = spectrumTexture;
        spectrumImage.color = Color.white;

        spectrumPixels = new Color[(int)(textureSize.x * textureSize.y)];
        ClearTexture();
    }

    void ClearTexture()
    {
        for (int i = 0; i < spectrumPixels.Length; i++)
        {
            spectrumPixels[i] = backgroundColor;
        }
        spectrumTexture.SetPixels(spectrumPixels);
        spectrumTexture.Apply();
    }

    IEnumerator SpectralAnalysis()
    {
        while (true)
        {
            if (audioSource == null || !audioSource.isPlaying)
            {
                Debug.LogWarning("AudioSource not available or not playing");
                yield return new WaitForSeconds(1f);
                continue;
            }

            audioSource.GetSpectrumData(spectrumData, 0, fftWindow);

            // Apply overlap and averaging
            if (samplesProcessed > 0)
            {
                for (int i = 0; i < overlapSamples; i++)
                {
                    spectrumData[i] = (spectrumData[i] + previousSamples[i]) / 2f;
                }
            }

            // Update previous samples for overlap
            System.Array.Copy(spectrumData, previousSamples, overlapSamples);

            // Apply averaging over time
            for (int i = 0; i < fftSize; i++)
            {
                averagedSpectrum[i] = Mathf.Lerp(averagedSpectrum[i], spectrumData[i], Time.deltaTime / avgTime);
            }

            // Find the peak frequency
            peakFrequency = FindPeakFrequency();

            // Update visualization
            UpdateSpectrumTexture();

            samplesProcessed++;
            yield return new WaitForSeconds((fftSize - overlapSamples) / sampleRate);
        }
    }
    float FindPeakFrequency()
    {
        float maxMagnitude = 0f;
        int peakBin = 0;

        // Find the bin with the highest magnitude
        for (int i = 0; i < fftSize; i++)
        {
            if (averagedSpectrum[i] > maxMagnitude)
            {
                maxMagnitude = averagedSpectrum[i];
                peakBin = i;
            }
        }

        // Interpolate to find the exact peak frequency
        float peakFrequency = InterpolatePeakFrequency(peakBin);

        // Apply global offset to the peak frequency
        peakFrequency *= Mathf.Pow(2, -globalOffset);

        return peakFrequency;
    }

    float InterpolatePeakFrequency(int peakBin)
    {
        if (peakBin <= 0 || peakBin >= fftSize - 1)
        {
            return peakBin * frequencyResolution;
        }

        // Parabolic interpolation to find the exact peak frequency
        float dL = averagedSpectrum[peakBin - 1];
        float dC = averagedSpectrum[peakBin];
        float dR = averagedSpectrum[peakBin + 1];

        float offset = (dL - dR) / (2 * (dL - 2 * dC + dR));
        float peakFrequency = (peakBin + offset) * frequencyResolution;

        return peakFrequency;
    }

    void UpdateSpectrumTexture()
    {
        if (spectrumTexture == null || spectrumPixels == null) return;

        // Clear the texture
        ClearTexture();

        // Draw the frequency curve with calibration
        for (int x = 0; x < (int)textureSize.x; x++)
        {
            float frequency = FrequencyAtX(x);
            float bin = frequency / frequencyResolution;
            int binFloor = Mathf.FloorToInt(bin);
            float fraction = bin - binFloor;

            binFloor = Mathf.Clamp(binFloor, 0, fftSize - 1);
            int binCeil = Mathf.Min(binFloor + 1, fftSize - 1);

            float magnitude = Mathf.Lerp(averagedSpectrum[binFloor], averagedSpectrum[binCeil], fraction);
            float dB = 20 * Mathf.Log10(magnitude + 1e-12f); // Convert to dB
            dB = ApplySlope(dB, binFloor * frequencyResolution); // Apply slope
            dB = Mathf.Clamp(dB, rangeLow, rangeHigh); // Clamp to amplitude range
            float normalized = Mathf.Clamp01((dB - rangeLow) / (rangeHigh - rangeLow)); // Normalize to amplitude range
            int barHeight = Mathf.FloorToInt(normalized * textureSize.y);

            for (int y = 0; y < (int)textureSize.y; y++)
            {
                int index = x + y * (int)textureSize.x;
                if (y < barHeight)
                {
                    spectrumPixels[index] = spectrumGradient.Evaluate(normalized);
                }
            }
        }

        // Draw frequency markings (static)
        DrawFrequencyMarkings();

        // Draw amplitude markings (horizontal lines)
        DrawAmplitudeMarkings();

        // Draw the peak frequency marker (with global offset applied)
        DrawPeakFrequencyMarker();

        // Update the UI Text with the peak frequency
        if (peakFrequencyText != null)
        {
            peakFrequencyText.text = $" {peakFrequency:F2} Hz";
        }

        // Find the closest note and accuracy in cents
        var (closestNote, closestOctave, closestFrequency) = FindClosestNote(peakFrequency);
        int cents = CalculateCents(peakFrequency, closestFrequency);

        // Update the UI with the closest note and accuracy in cents
        if (closestNoteText != null)
        {
            closestNoteText.text = $" {closestNote}{closestOctave}";
        }
        if (accuracyText != null)
        {
            accuracyText.text = $" {cents}";
        }

        spectrumTexture.SetPixels(spectrumPixels);
        spectrumTexture.Apply();
    }

    float FrequencyAtX(float x)
    {
        // Logarithmic mapping of x to frequency with calibration
        float normalizedX = x / (textureSize.x - 1);
        float frequency = Mathf.Pow(10, normalizedX * Mathf.Log10(freqHigh / freqLow)) * freqLow;

        // Apply global offset
        frequency *= Mathf.Pow(2, globalOffset);

        return frequency;
    }

    void DrawPeakFrequencyMarker()
    {
        if (peakFrequency < freqLow || peakFrequency > freqHigh) return;

        // Apply global offset to the peak frequency
        float calibratedPeakFrequency = peakFrequency * Mathf.Pow(1, globalOffset);

        // Map the calibrated peak frequency to the x-position
        float x = FrequencyToX(calibratedPeakFrequency);

        // Draw the peak frequency marker
        DrawVerticalLine((int)x, Color.red, lineThickness);
    }

    float ApplySlope(float dB, float frequency)
    {
        // Apply slope in dB/octave
        float octaves = Mathf.Log(frequency / freqLow, 2);
        return dB + slope * octaves;
    }

    void DrawFrequencyMarkings()
    {
        float[] frequencies = {
            20, 30, 40, 50, 60, 70, 80, 90, 100, // Sub Frequencies
            200, 300, 400, 500, 600, 700, 800, 900, 1000, // Low-Mid Frequencies
            2000, 3000, 4000, 5000, 6000, 7000, 8000, 9000, 10000, // Mid-High Frequencies
            20000 // High Frequency
        };

        foreach (float frequency in frequencies)
        {
            if (frequency >= freqLow && frequency <= freqHigh)
            {
                float x = FrequencyToX(frequency);
                DrawVerticalLine((int)x, lineColor, lineThickness);
            }
        }
    }

    void DrawAmplitudeMarkings()
    {
        float[] amplitudes = GetAmplitudeMarkings();
        foreach (float amplitude in amplitudes)
        {
            if (amplitude >= rangeLow && amplitude <= rangeHigh)
            {
                float y = AmplitudeToY(amplitude);
                DrawHorizontalLine((int)y, lineColor, lineThickness);
            }
        }
    }

    private float[] GetAmplitudeMarkings()
    {
        // Define the number of markings you want
        int numberOfMarkings = 10; // Adjust this number as needed
        float[] amplitudes = new float[numberOfMarkings];

        // Calculate the step size between each marking
        float step = (rangeHigh - rangeLow) / (numberOfMarkings - 1);

        // Generate the amplitude values
        for (int i = 0; i < numberOfMarkings; i++)
        {
            amplitudes[i] = rangeLow + i * step;
        }

        return amplitudes;
    }

    private float AmplitudeToY(float dB)
    {
        // Normalize the dB value to the range [0, 1]
        float normalized = Mathf.Clamp01((dB - rangeLow) / (rangeHigh - rangeLow));
        // Map the normalized value to the texture's y-coordinate
        return normalized * (textureSize.y - 1);
    }

    void DrawHorizontalLine(int y, Color color, int thickness)
    {
        int yStart = Mathf.Clamp(y - thickness / 2, 0, (int)textureSize.y - 1);
        int yEnd = Mathf.Clamp(y + thickness / 2, 0, (int)textureSize.y - 1);

        for (int yPos = yStart; yPos <= yEnd; yPos++)
        {
            for (int x = 0; x < textureSize.x; x++)
            {
                int index = x + yPos * (int)textureSize.x;
                spectrumPixels[index] = color;
            }
        }
    }

    void DrawVerticalLine(int x, Color color, int thickness)
    {
        int xStart = Mathf.Clamp(x - thickness / 2, 0, (int)textureSize.x - 1);
        int xEnd = Mathf.Clamp(x + thickness / 2, 0, (int)textureSize.x - 1);

        for (int xPos = xStart; xPos <= xEnd; xPos++)
        {
            for (int y = 0; y < textureSize.y; y++)
            {
                int index = xPos + y * (int)textureSize.x;
                spectrumPixels[index] = color;
            }
        }
    }

    float FrequencyToX(float frequency)
    {
        // Logarithmic mapping of frequency to x position
        float normalized = Mathf.Log10(frequency / freqLow) / Mathf.Log10(freqHigh / freqLow);
        return normalized * (textureSize.x - 1);
    }

    private (string note, int octave, float frequency) FindClosestNote(float targetFrequency)
    {
        string closestNote = "";
        int closestOctave = 0;
        float closestFrequency = 0f;
        float minDifference = float.MaxValue;

        foreach (var note in noteFrequencies)
        {
            // Calculate the octave range to search
            int minOctave = -1; // Start from octave -1
            int maxOctave = 10; // Go up to octave 10 (adjust as needed)

            for (int octave = minOctave; octave <= maxOctave; octave++)
            {
                float frequency = GetNoteFrequency(note.Key, octave);
                float difference = Mathf.Abs(targetFrequency - frequency);

                if (difference < minDifference)
                {
                    minDifference = difference;
                    closestNote = note.Key;
                    closestOctave = octave;
                    closestFrequency = frequency;
                }
            }
        }

        return (closestNote, closestOctave, closestFrequency);
    }

    private float GetNoteFrequency(string note, int octave)
    {
        if (noteFrequencies.ContainsKey(note))
        {
            return noteFrequencies[note] * Mathf.Pow(2, octave);
        }
        else
        {
            Debug.LogError($"Note {note} not found in the dictionary.");
            return 0f;
        }
    }

    private int CalculateCents(float targetFrequency, float closestNoteFrequency)
    {
        if (closestNoteFrequency == 0f)
        {
            Debug.LogError("Closest note frequency is zero. Cannot calculate cents.");
            return 0;
        }

        // Calculate cents
        float cents = 1200 * Mathf.Log(targetFrequency / closestNoteFrequency, 2);

        // Round to the nearest integer
        int centsInt = Mathf.RoundToInt(cents);

        // Wrap around if the cents exceed ±50
        if (centsInt > 50)
        {
            centsInt -= 100;
        }
        else if (centsInt < -50)
        {
            centsInt += 100;
        }

        return centsInt;
    }
}