using UnityEngine;
using UnityEngine.UI;


public class OscilloscopeVisual : MonoBehaviour
{
    public RawImage oscilloscopeDisplay;
    public AudioSource audioSource;
    public Text tempoText;


    [Header("Drawing Colors")]
    public Color backgroundColor = Color.black;
    public Color waveformColor = Color.green;
    public Color centerLineColor = Color.gray;
    public Color beatMarkerColor = new Color(0.3f, 0.3f, 0.3f);

    private Texture2D displayTexture;
    private Color[] blankPixels;

    private int width = 512;
    private int height = 256;
    private float verticalScale = 128f;

    private float[] rightSamples;
    private float[] leftSamples;
    private int sampleRate;
    private float bpm;
    private float secondsPerBar;
    private int samplesPerBar;
    private int samplesPerQuarterBar;
    private int firstTransientSample;

    private int currentDisplayStartSample = 0;
    private int referenceBarStartSample = 0;
    private enum DisplayMode { FullBar, QuarterBar }
    private DisplayMode currentDisplayMode = DisplayMode.FullBar;

   

    void Start()
    {
        InitializeOscilloscope();
    }


    void InitializeOscilloscope()
    {

        if (displayTexture == null || displayTexture.width != width || displayTexture.height != height)
        {
            displayTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            oscilloscopeDisplay.texture = displayTexture;
            blankPixels = new Color[width * height];
        }

        sampleRate = audioSource.clip.frequency;
        rightSamples = new float[audioSource.clip.samples];
        leftSamples = new float[audioSource.clip.samples];
        audioSource.clip.GetData(rightSamples, 0);

        if (audioSource.clip.channels == 2)
        {
            float[] allSamples = new float[audioSource.clip.samples * 2];
            audioSource.clip.GetData(allSamples, 0);

            for (int i = 0; i < audioSource.clip.samples; i++)
            {
                leftSamples[i] = allSamples[i * 2];
                rightSamples[i] = allSamples[i * 2 + 1];
            }
        }

        if (float.TryParse(tempoText.text, out bpm))
        {
            secondsPerBar = 60f / bpm * 4f;
            samplesPerBar = Mathf.FloorToInt(secondsPerBar * sampleRate);
            samplesPerQuarterBar = samplesPerBar / 4;
        }

        firstTransientSample = FindFirstNonSilentSample();
        referenceBarStartSample = firstTransientSample;
        currentDisplayStartSample = referenceBarStartSample;


        UpdateOscilloscopeDisplay();
    }

    void Update()
    {

 

        int currentSample = audioSource.timeSamples;

        if (currentDisplayMode == DisplayMode.FullBar)
        {
            int barOffset = currentSample - referenceBarStartSample;
            if (Mathf.Abs(barOffset) >= samplesPerBar)
            {
                int barsJumped = Mathf.FloorToInt(barOffset / (float)samplesPerBar);
                referenceBarStartSample += barsJumped * samplesPerBar;
                currentDisplayStartSample = referenceBarStartSample;
                UpdateOscilloscopeDisplay();
            }
        }
        else if (currentDisplayMode == DisplayMode.QuarterBar)
        {
            int quarterOffset = currentSample - currentDisplayStartSample;
            if (Mathf.Abs(quarterOffset) >= samplesPerQuarterBar)
            {
                int quartersJumped = Mathf.FloorToInt(quarterOffset / (float)samplesPerQuarterBar);
                currentDisplayStartSample += quartersJumped * samplesPerQuarterBar;
                UpdateOscilloscopeDisplay();
            }
        }
    }




    private bool IsMainSourcePlaying()
    {
        return audioSource.isPlaying;
    }

    public void SetFullBarMode()
    {
        currentDisplayMode = DisplayMode.FullBar;
        currentDisplayStartSample = referenceBarStartSample;
        UpdateOscilloscopeDisplay();
    }

    public void SetQuarterBarMode()
    {
        currentDisplayMode = DisplayMode.QuarterBar;
        int currentSample = audioSource.timeSamples;
        int offsetInBar = currentSample - referenceBarStartSample;
        int quarterIndex = Mathf.Clamp(offsetInBar / samplesPerQuarterBar, 0, 3);
        currentDisplayStartSample = referenceBarStartSample + quarterIndex * samplesPerQuarterBar;
        UpdateOscilloscopeDisplay();
    }

    private int FindFirstNonSilentSample()
    {
        float peak = -40f;
        int peakpos = 0;
        
        for (int i = 0; i < samplesPerBar; i++)
        {
            if (Mathf.Abs(rightSamples[i]) > peak)
            {
                peak = rightSamples[i];
                peakpos = i;
            }
        }
        return peakpos;
    }

    private void ClearDisplay()
    {
        for (int i = 0; i < blankPixels.Length; i++)
            blankPixels[i] = backgroundColor;
        displayTexture.SetPixels(blankPixels);
        displayTexture.Apply();
    }

    private void UpdateOscilloscopeDisplay()
    {
        ClearDisplay();
        DrawGridLines();
        DrawWaveform();
        displayTexture.Apply();
    }

    private void DrawGridLines()
    {
        DrawHorizontalLine(height / 2, centerLineColor);

        int beats = currentDisplayMode == DisplayMode.FullBar ? 4 : 1;
        for (int i = 0; i <= beats; i++)
        {
            int xPos = Mathf.FloorToInt(width * i / (float)beats);
            DrawVerticalLine(xPos, beatMarkerColor);
        }
    }

    private void DrawHorizontalLine(int y, Color color)
    {
        for (int x = 0; x < width; x++)
            displayTexture.SetPixel(x, y, color);
    }

    private void DrawVerticalLine(int x, Color color)
    {
        for (int y = 0; y < height; y++)
            displayTexture.SetPixel(x, y, color);
    }

    private void DrawWaveform()
    {
        int samplesToDraw = currentDisplayMode == DisplayMode.FullBar ? samplesPerBar : samplesPerQuarterBar;
        int startSample = Mathf.Max(currentDisplayStartSample, firstTransientSample);
        int endSample = Mathf.Min(startSample + samplesToDraw, rightSamples.Length);
        
        if (endSample <= startSample) return;

        float samplesPerPixel = (endSample - startSample) / (float)width;

        for (int x = 0; x < width; x++)
        {
            int pixelStartSample = startSample + Mathf.FloorToInt(x * samplesPerPixel);
            int pixelEndSample = startSample + Mathf.FloorToInt((x + 1) * samplesPerPixel);
            pixelEndSample = Mathf.Min(pixelEndSample, rightSamples.Length - 1);

            float min = float.MaxValue, max = float.MinValue;
            for (int s = pixelStartSample; s < pixelEndSample; s++)
            {
                float sample = rightSamples[s];
                min = Mathf.Min(min, sample);
                max = Mathf.Max(max, sample);
            }

            int yMin = Mathf.FloorToInt(height / 2f - min * verticalScale);
            int yMax = Mathf.FloorToInt(height / 2f - max * verticalScale);
            yMin = Mathf.Clamp(yMin, 0, height - 1);
            yMax = Mathf.Clamp(yMax, 0, height - 1);

            if (yMin > yMax) (yMin, yMax) = (yMax, yMin);

            for (int y = yMin; y <= yMax; y++)
                displayTexture.SetPixel(x, y, waveformColor);
        }
    }
}