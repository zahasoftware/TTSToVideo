using Microsoft.VisualBasic;
using NAudio.Lame;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Runtime.CompilerServices;

namespace TTSToVideo.Helpers
{
    public static class AudioHelper
    {
        public static void ConcatenateAudioFiles(string outputFile, params string[] inputFiles)
        {
            // Create a new WaveFileWriter for the output file
            WaveFileWriter waveFileWriter = null;

            try
            {
                foreach (string inputFile in inputFiles)
                {
                    using WaveFileReader reader = new(inputFile);
                    if (waveFileWriter == null)
                    {
                        // Create the output file with the same format as the first input file
                        waveFileWriter = new WaveFileWriter(outputFile, reader.WaveFormat);
                    }
                    else if (!reader.WaveFormat.Equals(waveFileWriter.WaveFormat))
                    {
                        throw new InvalidOperationException("Can't concatenate WAV files with different formats.");
                    }

                    // Read and write audio data
                    byte[] buffer = new byte[reader.WaveFormat.AverageBytesPerSecond];
                    int bytesRead;
                    while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        waveFileWriter.Write(buffer, 0, bytesRead);
                    }

                    reader.Close();
                }
            }
            finally
            {
                waveFileWriter?.Dispose();
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

        public static void CutAudio(string inputPath, string outputPath, double durationInSeconds)
        {
            TimeSpan startTime = TimeSpan.FromSeconds(0);
            TimeSpan endTime = TimeSpan.FromSeconds(durationInSeconds);

            using (var reader = new AudioFileReader(inputPath))
            using (var writer = new WaveFileWriter(outputPath, reader.WaveFormat))
            {
                reader.CurrentTime = startTime; // Set the start time
                var bytesPerMillisecond = reader.WaveFormat.AverageBytesPerSecond / 1000;

                var startPos = (int)startTime.TotalMilliseconds * bytesPerMillisecond;
                startPos = startPos - startPos % reader.WaveFormat.BlockAlign;

                var endPos = (int)endTime.TotalMilliseconds * bytesPerMillisecond;
                endPos = endPos - endPos % reader.WaveFormat.BlockAlign;

                var buffer = new byte[1024];
                while (reader.Position < endPos)
                {
                    var bytesRequired = (int)(endPos - reader.Position);
                    if (bytesRequired > 0)
                    {
                        var bytesToRead = Math.Min(bytesRequired, buffer.Length);
                        var bytesRead = reader.Read(buffer, 0, bytesToRead);
                        if (bytesRead > 0)
                        {
                            writer.Write(buffer, 0, bytesRead);
                        }
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
                using FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                byte[] headerBytes = new byte[12];
                int bytesRead = stream.Read(headerBytes, 0, 12);

                if (bytesRead == 12)
                {
                    string headerHex = BitConverter.ToString(headerBytes).Replace("-", "");

                    // Check for "RIFF" header and "WAVE" format identifier
                    if (headerHex.StartsWith("52494646") && headerHex.Substring(16, 8) == "57415645")
                    {
                        return AudioFormat.WAV;
                    }
                    // Check for "ID3" tag which indicates an MP3 file
                    else if (headerHex.StartsWith("494433") || headerHex.StartsWith("FFFB"))
                    {
                        return AudioFormat.MP3;
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
                        throw new Exception("Audio file isn't recognized");
                    }

                }
                return stream;
            }
        }

        //Convert MP3 to WAV using NAudio
        public static void ConvertMp3ToWav(string mp3File, string wavFile)
        {
            using var reader = new Mp3FileReader(mp3File);
            WaveFileWriter.CreateWaveFile(wavFile, reader);
        }

        public static async Task CreateSilentWavAudio(string silenceAudioTemp, TimeSpan timeSpan, CancellationToken token)
        {
            // Calculate the number of samples needed for the silent audio
            int sampleRate = 44100; // Assuming a sample rate of 44100 Hz
            int numSamples = (int)(timeSpan.TotalSeconds * sampleRate);

            // Create an array to hold the silent audio samples
            float[] samples = new float[numSamples];

            // Write the silent audio samples to a WAV file
            using (var writer = new WaveFileWriter(silenceAudioTemp, new WaveFormat(sampleRate, 16, 1)))
            {
                byte[] buffer = new byte[numSamples * 2]; // 2 bytes per sample for 16-bit audio

                // Convert the float samples to 16-bit PCM and write to the buffer
                for (int i = 0; i < numSamples; i++)
                {
                    short sampleValue = (short)(samples[i] * short.MaxValue);
                    buffer[i * 2] = (byte)(sampleValue & 0xFF);
                    buffer[i * 2 + 1] = (byte)(sampleValue >> 8);
                }

                // Write the buffer to the WAV file
                writer.Write(buffer, 0, buffer.Length);
            }
        }
    }


}