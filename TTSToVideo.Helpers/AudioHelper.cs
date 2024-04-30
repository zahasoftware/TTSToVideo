using NAudio.Lame;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Runtime.CompilerServices;

public static class AudioHelper
{
    public static void ConcatenateAudioFiles(string outputFile, params string[] sourceFiles)
    {
        byte[] buffer = new byte[1024];
        Stream waveFileWriter = null;

        try
        {
            foreach (string sourceFile in sourceFiles)
            {
                var audioFormat = DetectAudioFormat(sourceFile);
                using (WaveStream reader = OpenAudio(sourceFile))
                {
                    if (waveFileWriter == null)
                    {
                        // first time in create new Writer
                        waveFileWriter = AudioWriter(outputFile, audioFormat, reader);
                    }
                    else
                    {
                        //if (!reader.WaveFormat.Equals(waveFileWriter.WaveFormat))
                        {
                            //throw new InvalidOperationException("Can't concatenate WAV Files that don't share the same format");
                        }
                    }

                    int read;
                    while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        waveFileWriter.Write(buffer, 0, read);
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
        using var reader = OpenAudio(inputFilePath);
        // Get the sample rate (number of samples per second) of the audio
        int sampleRate = reader.WaveFormat.SampleRate;

        // Calculate the number of samples corresponding to the target time
        long targetSample = (long)(targetTime.TotalSeconds * sampleRate);

        // Create a WaveProvider to process the audio
        var waveProvider = new OffsetSampleProvider(reader as ISampleProvider);
        waveProvider.SkipOver = TimeSpan.FromSeconds(targetSample);

        // Create a volume adjustment provider
        var volumeProvider = new VolumeSampleProvider(waveProvider);
        volumeProvider.Volume = volumeDecibel;

        // Write the processed audio to a new output file
        WaveFileWriter.CreateWaveFile16(outputFilePath, volumeProvider);
    }

    public static void CutAudio(string inputFilePath, string outputFilePath, double durationInSeconds)
    {
        var audioFormat = DetectAudioFormat(inputFilePath);
        using (WaveStream audioFileReader = OpenAudio(inputFilePath))
        {
            // Calculate the desired start and end positions in bytes
            long startPosition = (long)(0 * audioFileReader.WaveFormat.AverageBytesPerSecond);
            long endPosition = (long)(durationInSeconds * audioFileReader.WaveFormat.AverageBytesPerSecond);

            // Ensure the end position is within the actual file length
            if (endPosition > audioFileReader.Length)
            {
                endPosition = audioFileReader.Length;
            }

            // Create a new WaveFileWriter for the output
            using Stream writer = AudioWriter(outputFilePath, audioFormat, audioFileReader);

            // Set the reader's position to the desired start position
            audioFileReader.Position = startPosition;

            byte[] buffer = new byte[4096]; // Adjust buffer size as needed
            long remainingBytes = endPosition - startPosition;

            while (remainingBytes > 0)
            {
                int bytesToRead = (int)Math.Min(buffer.Length, remainingBytes);
                int bytesRead = audioFileReader.Read(buffer, 0, bytesToRead);

                if (bytesRead > 0)
                {
                    writer.Write(buffer, 0, bytesRead);
                    remainingBytes -= bytesRead;
                }
                else
                {
                    break; // End of file reached
                }
            }
        }
    }

    private static Stream AudioWriter(string outputFilePath, AudioFormat audioFormat, WaveStream audioFileReader)
    {
        Stream writer;
        if (audioFormat == AudioFormat.MP3)
            writer = new LameMP3FileWriter(outputFilePath, audioFileReader.WaveFormat, LAMEPreset.STANDARD);
        else
            writer = new WaveFileWriter(outputFilePath, audioFileReader.WaveFormat);
        return writer;
    }

    public enum AudioFormat
    {
        Unknown,
        MP3,
        WAV
    }

    public static AudioFormat DetectAudioFormat(string filePath)
    {
        if (File.Exists(filePath))
        {
            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                byte[] headerBytes = new byte[4];
                int bytesRead = stream.Read(headerBytes, 0, 4);

                if (bytesRead == 4)
                {
                    string headerHex = BitConverter.ToString(headerBytes).Replace("-", "");

                    if (headerHex.StartsWith("494433") || headerHex.StartsWith("46464952"))
                    {
                        return AudioFormat.WAV;
                    }
                    else if (headerHex.StartsWith("FFFB") || headerHex.StartsWith("FFF3") || headerHex.StartsWith("FFF2"))
                    {
                        return AudioFormat.MP3;
                    }
                }
            }
        }

        return AudioFormat.Unknown;
    }

    public static WaveStream OpenAudio(string filePath)
    {
        if (DetectAudioFormat(filePath) == AudioFormat.MP3)
        {
            return new Mp3FileReader(filePath);
        }
        else if (DetectAudioFormat(filePath) == AudioFormat.WAV)
        {
            return new WaveFileReader(filePath);
        }
        else
        {
            WaveStream stream;

            try
            {
                stream = new AudioFileReader(filePath);
            }
            catch
            {
                try
                {
                    stream = new Mp3FileReader(filePath);
                }
                catch
                {
                    try
                    {
                        stream = new Mp3FileReader(filePath);
                    }
                    catch
                    {
                        throw new Exception("Audio file isn't recognized");
                    }
                }

            }
            return stream;
        }
    }
}
