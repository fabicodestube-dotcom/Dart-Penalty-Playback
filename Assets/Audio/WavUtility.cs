using System;
using System.IO;
using UnityEngine;

public static class WavUtility
{
    public static byte[] FromAudioClip(AudioClip clip)
    {
        if (clip == null)
            return null;

        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        short[] intData = new short[samples.Length];

        byte[] bytesData = new byte[samples.Length * 2];

        const float rescaleFactor = 32767f;

        for (int i = 0; i < samples.Length; i++)
        {
            intData[i] = (short)Mathf.Clamp(samples[i] * rescaleFactor, short.MinValue, short.MaxValue);
            byte[] byteArr = BitConverter.GetBytes(intData[i]);
            byteArr.CopyTo(bytesData, i * 2);
        }

        byte[] header = CreateWavHeader(
            clip.samples,
            clip.channels,
            clip.frequency
        );

        byte[] wav = new byte[header.Length + bytesData.Length];

        Buffer.BlockCopy(header, 0, wav, 0, header.Length);
        Buffer.BlockCopy(bytesData, 0, wav, header.Length, bytesData.Length);

        return wav;
    }

    private static byte[] CreateWavHeader(int samples, int channels, int frequency)
    {
        int headerSize = 44;
        int byteRate = frequency * channels * 2;

        int dataSize = samples * channels * 2;

        byte[] header = new byte[headerSize];

        // RIFF
        System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(header, 0);
        BitConverter.GetBytes(headerSize + dataSize - 8).CopyTo(header, 4);
        System.Text.Encoding.ASCII.GetBytes("WAVE").CopyTo(header, 8);

        // fmt
        System.Text.Encoding.ASCII.GetBytes("fmt ").CopyTo(header, 12);
        BitConverter.GetBytes(16).CopyTo(header, 16); // PCM
        BitConverter.GetBytes((short)1).CopyTo(header, 20); // PCM format
        BitConverter.GetBytes((short)channels).CopyTo(header, 22);
        BitConverter.GetBytes(frequency).CopyTo(header, 24);
        BitConverter.GetBytes(byteRate).CopyTo(header, 28);
        BitConverter.GetBytes((short)(channels * 2)).CopyTo(header, 32);
        BitConverter.GetBytes((short)16).CopyTo(header, 34);

        // data
        System.Text.Encoding.ASCII.GetBytes("data").CopyTo(header, 36);
        BitConverter.GetBytes(dataSize).CopyTo(header, 40);

        return header;
    }
}