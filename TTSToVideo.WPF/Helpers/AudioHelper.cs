using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

public class AudioHelper
{
    static public void ConcatenateAudioFiles(string outputFile, params string[] sourceFiles)
    {
        byte[] buffer = new byte[1024];
        WaveFileWriter waveFileWriter = null;

        try
        {
            foreach (string sourceFile in sourceFiles)
            {
                using (WaveFileReader reader = new WaveFileReader(sourceFile))
                {
                    if (waveFileWriter == null)
                    {
                        // first time in create new Writer
                        waveFileWriter = new WaveFileWriter(outputFile, reader.WaveFormat);
                    }
                    else
                    {
                        if (!reader.WaveFormat.Equals(waveFileWriter.WaveFormat))
                        {
                            throw new InvalidOperationException("Can't concatenate WAV Files that don't share the same format");
                        }
                    }

                    int read;
                    while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        waveFileWriter.WriteData(buffer, 0, read);
                    }
                }
            }
        }
        finally
        {
            if (waveFileWriter != null)
            {
                waveFileWriter.Dispose();
            }
        }
    }

    public static void DecreaseVolumeAtSpecificTime(string inputFilePath, string outputFilePath, TimeSpan targetTime, float volumeDecibel)
    {
        // Load the input audio file
        using (var reader = new AudioFileReader(inputFilePath))
        {
            // Get the sample rate (number of samples per second) of the audio
            int sampleRate = reader.WaveFormat.SampleRate;

            // Calculate the number of samples corresponding to the target time
            long targetSample = (long)(targetTime.TotalSeconds * sampleRate);

            // Create a WaveProvider to process the audio
            var waveProvider = new OffsetSampleProvider(reader);
            waveProvider.SkipOver = TimeSpan.FromSeconds(targetSample);

            // Create a volume adjustment provider
            var volumeProvider = new VolumeSampleProvider(waveProvider);
            volumeProvider.Volume = volumeDecibel;

            // Write the processed audio to a new output file
            WaveFileWriter.CreateWaveFile16(outputFilePath, volumeProvider);
        }
    }

    public static void CutAudio(string inputFilePath, string outputFilePath, double durationInSeconds)
    {
        using var reader = new AudioFileReader(inputFilePath);
        var totalSamples = reader.Length / reader.BlockAlign;
        var samplesToTake = (int)(durationInSeconds * reader.WaveFormat.SampleRate);

        if (samplesToTake > totalSamples)
            samplesToTake = (int)totalSamples;

        var buffer = new float[samplesToTake * reader.WaveFormat.Channels];
        var samplesRead = reader.Read(buffer, 0, buffer.Length);

        if (samplesRead > 0)
        {
            using var writer = new WaveFileWriter(outputFilePath, reader.WaveFormat);
            writer.WriteSamples(buffer, 0, samplesRead);
        }
    }
}
