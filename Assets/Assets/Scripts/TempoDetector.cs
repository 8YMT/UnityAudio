using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class TempoDetector : MonoBehaviour
{
    public AudioSource audioSource;
    public int blockSize = 1024;
    [Range(40, 240)] public int minTempo = 60;
    [Range(40, 240)] public int maxTempo = 200;

    public Text Tempo;

    private AudioClip audioClip;
    private AudioClip lastProcessedClip;
    private float[] rightChannel;
    private int sampleCount;
    private int blockCount;
    private float[] rmsValues;
    private float[] rmsDbValues;

    private int detectedTempo = -1;
    private float detectionConfidence = 0f;

    void Start()
    {
        InitializeAndDetectTempo();
    }

    void Update()
    {
        if (audioSource.clip != lastProcessedClip)
        {
            InitializeAndDetectTempo();
        }
    }

    public void InitializeAndDetectTempo()
    {
        if (!InitializeAudio()) return;

        ProcessAudio();
        DetectTempo();

        if (Tempo != null)
            Tempo.text = detectedTempo > 0 ? detectedTempo.ToString() : "N/A";


    }

    private bool InitializeAudio()
    {
        if (audioSource == null || audioSource.clip == null)
        {
            Debug.LogError("Audio source not properly configured!");
            return false;
        }

        audioClip = audioSource.clip;
        lastProcessedClip = audioClip;
        sampleCount = audioClip.samples;
        blockCount = Mathf.CeilToInt((float)sampleCount / blockSize);

        float[] allSamples = new float[audioClip.samples * audioClip.channels];
        audioClip.GetData(allSamples, 0);
        rightChannel = new float[sampleCount];

        for (int i = 0, j = 1; j < allSamples.Length && i < sampleCount; i++, j += 2)
        {
            rightChannel[i] = allSamples[j];
        }

        return true;
    }

    private void ProcessAudio()
    {
        rmsValues = new float[blockCount];
        rmsDbValues = new float[blockCount];

        for (int block = 0; block < blockCount; block++)
        {
            int startSample = block * blockSize;
            int endSample = Mathf.Min(startSample + blockSize, sampleCount);
            float sumOfSquares = 0f;

            for (int i = startSample; i < endSample; i++)
            {
                sumOfSquares += rightChannel[i] * rightChannel[i];
            }

            rmsValues[block] = Mathf.Sqrt(sumOfSquares / (endSample - startSample));
            rmsDbValues[block] = 20f * Mathf.Log10(Mathf.Max(rmsValues[block], 1e-6f));
        }
    }

    private void DetectTempo()
    {
        List<int> beatBlocks = FindPotentialBeats();
        if (beatBlocks.Count < 2)
        {
            Debug.LogWarning("Insufficient beats found for tempo detection");
            return;
        }

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

        if (candidateTempo > 0)
        {
            var (verifiedTempo, confidence) = UltimateTempoVerification(candidateTempo, beatBlocks);
            detectedTempo = verifiedTempo;
            detectionConfidence = confidence;
        }
    }

    private List<int> FindPotentialBeats()
    {
        float silenceThresholdDb = -40f;
        List<int> beats = new List<int>();
        float windowSeconds = 0.3f;
        int windowBlocks = Mathf.Max(1, Mathf.RoundToInt(windowSeconds * audioClip.frequency / blockSize));

        for (int i = 0; i < rmsDbValues.Length; i++)
        {
            if (rmsDbValues[i] <= silenceThresholdDb) continue;

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

    private (int tempo, float confidence) UltimateTempoVerification(int candidateTempo, List<int> beatBlocks)
    {
        const float silenceThresholdDb = -40f;
        const int requiredBeatsToMatch = 3;
        float blockDuration = blockSize / (float)audioClip.frequency;

        int firstBeatBlock = -1;
        for (int i = 0; i < beatBlocks.Count; i++)
        {
            if (rmsDbValues[beatBlocks[i]] > silenceThresholdDb &&
                (i == beatBlocks.Count - 1 || rmsDbValues[beatBlocks[i + 1]] > silenceThresholdDb * 0.8f))
            {
                firstBeatBlock = beatBlocks[i];
                break;
            }
        }

        if (firstBeatBlock < 0) return (-1, 0f);

        int bestTempo = candidateTempo;
        float bestScore = EvaluateTempoCandidate(firstBeatBlock, candidateTempo, requiredBeatsToMatch);

        for (int variation = 1; variation <= 2; variation++)
        {
            int higherTempo = candidateTempo + variation;
            if (higherTempo <= maxTempo)
            {
                float score = EvaluateTempoCandidate(firstBeatBlock, higherTempo, requiredBeatsToMatch);
                if (score > bestScore * 1.1f)
                {
                    bestScore = score;
                    bestTempo = higherTempo;
                }
            }

            int lowerTempo = candidateTempo - variation;
            if (lowerTempo >= minTempo)
            {
                float score = EvaluateTempoCandidate(firstBeatBlock, lowerTempo, requiredBeatsToMatch);
                if (score > bestScore * 1.2f)
                {
                    bestScore = score;
                    bestTempo = lowerTempo;
                }
            }
        }

        float confidence = Mathf.Clamp01(bestScore / requiredBeatsToMatch);
        return (bestTempo, confidence);
    }

    private float EvaluateTempoCandidate(int startBlock, int tempo, int maxBeatsToCheck, float silenceThresholdDb = -40f)
    {
        float intervalSeconds = 60f / tempo;
        float blockDuration = blockSize / (float)audioClip.frequency;
        int blocksBetweenBeats = Mathf.RoundToInt(intervalSeconds / blockDuration);

        if (blocksBetweenBeats <= 0) return 0f;

        float referenceLevel = rmsDbValues[startBlock];
        float score = 0f;
        int beatsMatched = 0;

        for (int beat = 1; beat <= maxBeatsToCheck; beat++)
        {
            int targetBlock = startBlock + beat * blocksBetweenBeats;
            if (targetBlock >= rmsDbValues.Length) break;

            float levelDifference = Mathf.Abs(rmsDbValues[targetBlock] - referenceLevel);
            float beatSimilarity = 1f - Mathf.Clamp01(levelDifference / -silenceThresholdDb);

            score += beatSimilarity * (1f / beat);
            beatsMatched++;
        }

        return beatsMatched > 0 ? score / beatsMatched : 0f;
    }

    public int GetDetectedTempo() => detectedTempo;
    public float GetConfidence() => detectionConfidence;
}
