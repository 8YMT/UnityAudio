using UnityEngine;
using System.IO;
using System;
#if UNITY_EDITOR
using UnityEditor; // Needed for AssetDatabase.Refresh()
#endif

public class WhiteNoiseGenerator : MonoBehaviour
{
    public string fileName = "WhiteNoise.wav";
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

        // White noise generation (uniform random values)
        System.Random rand = new System.Random();
        for (int i = 0; i < totalSamples; i++)
        {
            // Generate white noise (-1 to 1 range)
            leftChannel[i] = (float)(rand.NextDouble() * 2.0 - 1.0) * gain;
            rightChannel[i] = (float)(rand.NextDouble() * 2.0 - 1.0) * gain;

            // OPTIONAL: For out-of-phase noise (like your pink noise example), uncomment:
            // rightChannel[i] = -leftChannel[i];
        }

        // Define the target directory
        string targetDir = Application.dataPath + "/Scenes/Mix Tools/";
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
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