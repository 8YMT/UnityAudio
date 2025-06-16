using UnityEngine;
using System;
using UnityEngine.UI;

public class LUFS : MonoBehaviour
{
    public AudioSource audioSource;
    private AudioClip audioClip;

    private float[] samples;
    private float[] rightChannelSamples;
    private float[] leftChannelSamples;

    [Range(0, 100)]
    public int overlap = 75; // Percentage
    private int momentaryBlockLength; // Samples per block (400ms)
    private int hopSize; // Samples between block starts
    private int momentaryBlockCount;
    private float sampleRate;

    // Short-term (3s, no overlap)
    private int shortTermBlockLength;
    private int shortTermBlockCount;

    // Filter coefficients (will be set based on sample rate)
    private float[] bHPF, aHPF; // High-pass filter coefficients
    private float[] bShelf, aShelf; // High-shelf filter coefficients

    // Storage arrays
    private float[,] momentaryLeft;
    private float[,] momentaryRight;
    private float[] momentaryEnergyLeft;
    private float[] momentaryEnergyRight;
    private float[] momentaryLUFS;

    private float[,] shortTermLeft;
    private float[,] shortTermRight;
    private float[] shortTermEnergyLeft;
    private float[] shortTermEnergyRight;
    private float[] shortTermLUFS;

    private float integratedLUFS;

    private float momentaryTimer = 0f; // Tracks time for momentary LUFS updates
    private float shortTermTimer = 0f; // Tracks time for short-term LUFS updates
    private int momentaryIndex = 0;    // Momentary LUFS array index
    private int shortTermIndex = 0;    // Short-Term LUFS array index

    private const float momentaryInterval = 0.100f; // 100ms per block
    private const float shortTermInterval = 3.0f;   // 3s per block

    public Text Momentarytxt;
    public Text ShortTermtxt;
    public Text Integratedtxt;




    void Start()
    {
        audioClip = audioSource.clip;
        sampleRate = audioClip.frequency;
        
        // Set coefficients based on sample rate
        SetFilterCoefficients();
        
        // Get all samples (interleaved L/R)
        samples = new float[audioClip.samples * audioClip.channels];
        audioClip.GetData(samples, 0);
        
        // Initialize channel arrays
        int sampleCount = samples.Length / audioClip.channels;
        leftChannelSamples = new float[sampleCount];
        rightChannelSamples = new float[sampleCount];
        
        // Separate channels AND apply K-weighting filters
        SeparateAndFilterChannels();
        
        // Momentary LUFS processing (400ms blocks)
        CalculateMomentaryParameters();
        ProcessMomentaryBlocks();
        CalculateMomentaryEnergy();
        CalculateMomentaryLUFS();
        
        // Short-term LUFS processing (3s blocks)
        CalculateShortTermParameters();
        ProcessShortTermBlocks();
        CalculateShortTermEnergy();
        CalculateShortTermLUFS();
        
        // Integrated LUFS
        CalculateIntegratedLUFS();
    }

   void Update()
{
       // Get current playback position in samples
    int currentSample;
    currentSample = audioSource.timeSamples;

    // Calculate current block indices based on sample position
    int newMomentaryIndex = Mathf.FloorToInt(currentSample / (float)hopSize);
    int newShortTermIndex = Mathf.FloorToInt(currentSample / (float)shortTermBlockLength);

    // Only update if we've moved to a new block
    if (newMomentaryIndex != momentaryIndex && newMomentaryIndex < momentaryLUFS.Length)
    {
        momentaryIndex = newMomentaryIndex;
        Momentarytxt.text = momentaryLUFS[momentaryIndex].ToString("F2");
    }

    if (newShortTermIndex != shortTermIndex && newShortTermIndex < shortTermLUFS.Length)
    {
        shortTermIndex = newShortTermIndex;
        ShortTermtxt.text = shortTermLUFS[shortTermIndex].ToString("F2");
    }
}
    void SetFilterCoefficients()
    {
        if (Mathf.Approximately(sampleRate, 48000f))
        {
            // High-shelf filter @ 48kHz
            bShelf = new float[] { 1.5351722789171889f, -2.6917030820199477f, 1.1983529243618682f };
            aShelf = new float[] { 1f, -1.6906327554159912f, 0.7324548766751006f };
            
            // High-pass filter @ 48kHz
            bHPF = new float[] { 1f, -2f, 1f };
            aHPF = new float[] { 1f, -1.99004745483398f, 0.99007225036621f };
        }
        else if (Mathf.Approximately(sampleRate, 44100f))
        {
            // High-shelf filter @ 44.1kHz
            bShelf = new float[] { 1.5270604978757836f, -2.6146232365796047f, 1.1432961778963153f };
            aShelf = new float[] { 1f, -1.6394317259680504f, 0.6951651651605447f };
            
            // High-pass filter @ 44.1kHz
            bHPF = new float[] { 1f, -2f, 1f };
            aHPF = new float[] { 1f, -1.98838142f, 0.98841517f };
        }
        else
        {
            Debug.LogWarning($"Unsupported sample rate: {sampleRate}Hz. Using 48kHz coefficients.");
            // Fallback to 48kHz coefficients
            bShelf = new float[] { 1.5351722789171889f, -2.6917030820199477f, 1.1983529243618682f };
            aShelf = new float[] { 1f, -1.6906327554159912f, 0.7324548766751006f };
            bHPF = new float[] { 1f, -2f, 1f };
            aHPF = new float[] { 1f, -1.99004745483398f, 0.99007225036621f };
        }
    }

    void SeparateAndFilterChannels()
    {
        // Left channel filter states
        float l_hpf_x1 = 0f, l_hpf_x2 = 0f, l_hpf_y1 = 0f, l_hpf_y2 = 0f;
        float l_shelf_x1 = 0f, l_shelf_x2 = 0f, l_shelf_y1 = 0f, l_shelf_y2 = 0f;
        
        // Right channel filter states
        float r_hpf_x1 = 0f, r_hpf_x2 = 0f, r_hpf_y1 = 0f, r_hpf_y2 = 0f;
        float r_shelf_x1 = 0f, r_shelf_x2 = 0f, r_shelf_y1 = 0f, r_shelf_y2 = 0f;

        for (int i = 0, j = 0; i < samples.Length; i += audioClip.channels, j++)
        {
            // Left channel processing
            float l_input = samples[i];
            
            // Apply high-pass filter
            float l_hpf = bHPF[0] * l_input + bHPF[1] * l_hpf_x1 + bHPF[2] * l_hpf_x2
                         - aHPF[1] * l_hpf_y1 - aHPF[2] * l_hpf_y2;
            
            // Update HPF states
            l_hpf_x2 = l_hpf_x1;
            l_hpf_x1 = l_input;
            l_hpf_y2 = l_hpf_y1;
            l_hpf_y1 = l_hpf;
            
            // Apply high-shelf filter
            float l_output = bShelf[0] * l_hpf + bShelf[1] * l_shelf_x1 + bShelf[2] * l_shelf_x2
                           - aShelf[1] * l_shelf_y1 - aShelf[2] * l_shelf_y2;
            
            // Update shelf states
            l_shelf_x2 = l_shelf_x1;
            l_shelf_x1 = l_hpf;
            l_shelf_y2 = l_shelf_y1;
            l_shelf_y1 = l_output;
            
            leftChannelSamples[j] = l_output;

            // Right channel processing (or duplicate left if mono)
            float r_input = (audioClip.channels > 1) ? samples[i + 1] : samples[i];
            
            // Apply high-pass filter
            float r_hpf = bHPF[0] * r_input + bHPF[1] * r_hpf_x1 + bHPF[2] * r_hpf_x2
                         - aHPF[1] * r_hpf_y1 - aHPF[2] * r_hpf_y2;
            
            // Update HPF states
            r_hpf_x2 = r_hpf_x1;
            r_hpf_x1 = r_input;
            r_hpf_y2 = r_hpf_y1;
            r_hpf_y1 = r_hpf;
            
            // Apply high-shelf filter
            float r_output = bShelf[0] * r_hpf + bShelf[1] * r_shelf_x1 + bShelf[2] * r_shelf_x2
                           - aShelf[1] * r_shelf_y1 - aShelf[2] * r_shelf_y2;
            
            // Update shelf states
            r_shelf_x2 = r_shelf_x1;
            r_shelf_x1 = r_hpf;
            r_shelf_y2 = r_shelf_y1;
            r_shelf_y1 = r_output;
            
            rightChannelSamples[j] = r_output;
        }
    }

    // ==================== MOMENTARY LUFS (400ms) ====================
    void CalculateMomentaryParameters()
    {
        momentaryBlockLength = Mathf.RoundToInt(0.4f * sampleRate);
        hopSize = Mathf.Max(1, Mathf.RoundToInt(momentaryBlockLength * (1 - overlap / 100f)));
        momentaryBlockCount = Mathf.Max(1, Mathf.FloorToInt((rightChannelSamples.Length - momentaryBlockLength) / (float)hopSize) + 1);
    }

    void ProcessMomentaryBlocks()
    {
        momentaryLeft = new float[momentaryBlockCount, momentaryBlockLength];
        momentaryRight = new float[momentaryBlockCount, momentaryBlockLength];
        momentaryEnergyLeft = new float[momentaryBlockCount];
        momentaryEnergyRight = new float[momentaryBlockCount];
        momentaryLUFS = new float[momentaryBlockCount];

        for (int block = 0; block < momentaryBlockCount; block++)
        {
            int startIndex = block * hopSize;
            
            for (int i = 0; i < momentaryBlockLength; i++)
            {
                int sampleIndex = startIndex + i;
                
                if (sampleIndex < leftChannelSamples.Length)
                {
                    momentaryLeft[block, i] = leftChannelSamples[sampleIndex];
                    momentaryRight[block, i] = rightChannelSamples[sampleIndex];
                }
                else
                {
                    // Zero-pad final block if needed
                    momentaryLeft[block, i] = 0;
                    momentaryRight[block, i] = 0;
                }
            }
        }
    }

    void CalculateMomentaryEnergy()
    {
        for (int block = 0; block < momentaryBlockCount; block++)
        {
            float sumLeft = 0f;
            float sumRight = 0f;
            
            for (int sample = 0; sample < momentaryBlockLength; sample++)
            {
                sumLeft += momentaryLeft[block, sample] * momentaryLeft[block, sample];
                sumRight += momentaryRight[block, sample] * momentaryRight[block, sample];
            }
            
            momentaryEnergyLeft[block] = sumLeft / momentaryBlockLength;
            momentaryEnergyRight[block] = sumRight / momentaryBlockLength;
        }
    }

    void CalculateMomentaryLUFS()
    {
        for (int block = 0; block < momentaryBlockCount; block++)
        {
            float meanEnergy = (momentaryEnergyLeft[block] + momentaryEnergyRight[block]);
            momentaryLUFS[block] = -0.691f + 10f * Mathf.Log10(Mathf.Max(meanEnergy, 1e-12f));
        }
    }

    // ==================== SHORT-TERM LUFS (3s) ====================
    void CalculateShortTermParameters()
    {
        shortTermBlockLength = Mathf.RoundToInt(3f * sampleRate);
        shortTermBlockCount = Mathf.Max(1, Mathf.FloorToInt(leftChannelSamples.Length / (float)shortTermBlockLength));
    }

    void ProcessShortTermBlocks()
    {
        shortTermLeft = new float[shortTermBlockCount, shortTermBlockLength];
        shortTermRight = new float[shortTermBlockCount, shortTermBlockLength];
        shortTermEnergyLeft = new float[shortTermBlockCount];
        shortTermEnergyRight = new float[shortTermBlockCount];
        shortTermLUFS = new float[shortTermBlockCount];

        for (int block = 0; block < shortTermBlockCount; block++)
        {
            int startIndex = block * shortTermBlockLength;
            
            for (int i = 0; i < shortTermBlockLength; i++)
            {
                int sampleIndex = startIndex + i;
                
                if (sampleIndex < leftChannelSamples.Length)
                {
                    shortTermLeft[block, i] = leftChannelSamples[sampleIndex];
                    shortTermRight[block, i] = rightChannelSamples[sampleIndex];
                }
                else
                {
                    // Zero-pad final block if needed
                    shortTermLeft[block, i] = 0;
                    shortTermRight[block, i] = 0;
                }
            }
        }
    }

    void CalculateShortTermEnergy()
    {
        for (int block = 0; block < shortTermBlockCount; block++)
        {
            float sumLeft = 0f;
            float sumRight = 0f;
            
            for (int sample = 0; sample < shortTermBlockLength; sample++)
            {
                sumLeft += shortTermLeft[block, sample] * shortTermLeft[block, sample];
                sumRight += shortTermRight[block, sample] * shortTermRight[block, sample];
            }
            
            shortTermEnergyLeft[block] = sumLeft / shortTermBlockLength;
            shortTermEnergyRight[block] = sumRight / shortTermBlockLength;
        }
    }

    void CalculateShortTermLUFS()
    {
        for (int block = 0; block < shortTermBlockCount; block++)
        {
            float meanEnergy = (shortTermEnergyLeft[block] + shortTermEnergyRight[block]);
            shortTermLUFS[block] = -0.691f + 10f * Mathf.Log10(Mathf.Max(meanEnergy, 1e-12f));
        }
    }

    // ==================== INTEGRATED LUFS ====================
    void CalculateIntegratedLUFS()
    {
        float totalEnergy = 0f;
        int validBlockCount = 0;

        for (int i = 0; i < shortTermBlockCount; i++)
        {
            float lufs = shortTermLUFS[i];
            if (lufs > -70f) // Apply absolute gate
            {
                float linearEnergy = Mathf.Pow(10f, (lufs + 0.691f) / 10f);
                totalEnergy += linearEnergy;
                validBlockCount++;
            }
        }

        if (validBlockCount > 0)
        {
            float meanEnergy = totalEnergy / validBlockCount;
            integratedLUFS = -0.691f + 10f * Mathf.Log10(meanEnergy);
        }
        else
        {
            integratedLUFS = -80f; // Silence fallback
        }

        Integratedtxt.text = integratedLUFS.ToString("F1");
    }

    // Public access methods
    public float[] GetMomentaryLUFS() => momentaryLUFS;
    public float[] GetShortTermLUFS() => shortTermLUFS;
    public float GetIntegratedLUFS() => integratedLUFS;
}