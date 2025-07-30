using UnityEngine;
using System.IO;
using System;
#if UNITY_EDITOR
using UnityEditor; // Needed for AssetDatabase.Refresh()
#endif

public class PinkNoiseGenerator : MonoBehaviour
{
    public string fileName = "OutOfPhasePinkNoise.wav";
    public int durationInSeconds = 5;
    public int sampleRate = 44100;
    public float gain = 0.5f;

    private void Start()
    {
        GenerateAndSaveNoise();
    }

    void GenerateAndSaveNoise()
    {
        int totalSamples = durationInSeconds * sampleRate;
        float[] leftChannel = new float[totalSamples];
        float[] rightChannel = new float[totalSamples];

        // Pink noise generation (Paul Kellet's method)
        System.Random rand = new System.Random();
        float b0 = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0, b5 = 0, b6 = 0;

        for (int i = 0; i < totalSamples; i++)
        {
            float white = (float)(rand.NextDouble() * 2.0 - 1.0);
            b0 = 0.99886f * b0 + white * 0.0555179f;
            b1 = 0.99332f * b1 + white * 0.0750759f;
            b2 = 0.96900f * b2 + white * 0.1538520f;
            b3 = 0.86650f * b3 + white * 0.3104856f;
            b4 = 0.55000f * b4 + white * 0.5329522f;
            b5 = -0.7616f * b5 - white * 0.0168980f;
            float pink = b0 + b1 + b2 + b3 + b4 + b5 + b6 + white * 0.5362f;
            b6 = white * 0.115926f;

            leftChannel[i] = pink * gain;
            rightChannel[i] = (i > 0) ? -leftChannel[i - 1] : -leftChannel[i]; // Inverted + 90° shift
        }

        // Define the target directory
        string targetDir = Application.dataPath + "/Scenes/Mix Tools/";
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir); // Create folder if missing
        }

        string fullPath = targetDir + fileName;
        SaveWav(fullPath, leftChannel, rightChannel, sampleRate);

        #if UNITY_EDITOR
        AssetDatabase.Refresh(); // Force Unity to detect the new file
        Debug.Log("Generated: " + fullPath);
        #endif
    }

    void SaveWav(string filePath, float[] left, float[] right, int sampleRate)
    {
        using (FileStream fs = new FileStream(filePath, FileMode.Create))
        using (BinaryWriter bw = new BinaryWriter(fs))
        {
            // WAV header (44 bytes)
            bw.Write(new char[] { 'R', 'I', 'F', 'F' });
            bw.Write(36 + left.Length * 4);
            bw.Write(new char[] { 'W', 'A', 'V', 'E' });
            bw.Write(new char[] { 'f', 'm', 't', ' ' });
            bw.Write(16);
            bw.Write((ushort)1);
            bw.Write((ushort)2);
            bw.Write(sampleRate);
            bw.Write(sampleRate * 4);
            bw.Write((ushort)4);
            bw.Write((ushort)32);
            bw.Write(new char[] { 'd', 'a', 't', 'a' });
            bw.Write(left.Length * 4);

            // Write interleaved L/R samples
            for (int i = 0; i < left.Length; i++)
            {
                bw.Write((short)(left[i] * 32767));
                bw.Write((short)(right[i] * 32767));
            }
        }
    }
}