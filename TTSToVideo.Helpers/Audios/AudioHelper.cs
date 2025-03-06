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

namespace TTSToVideo.Helpers.Audios
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


        public static void DecreaseVolumeAtSpecificTime(string inputFilePath, string outputFilePath, TimeSpan targetTime, TimeSpan duration, float volumeDecibel)
        {
            using (var reader = new AudioFileReader(inputFilePath))
            {
                using (var writer = new WaveFileWriter(outputFilePath, reader.WaveFormat))
                {
                    var samplesPerSecond = reader.WaveFormat.SampleRate * reader.WaveFormat.Channels;
                    var bytesPerSample = reader.WaveFormat.BitsPerSample / 8;

                    // Convert time to samples
                    int startSample = (int)(targetTime.TotalSeconds * samplesPerSecond);
                    int endSample = (int)((targetTime + duration).TotalSeconds * samplesPerSecond);

                    float[] buffer = new float[samplesPerSecond];
                    int samplesRead;
                    int sampleIndex = 0;

                    while ((samplesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        for (int i = 0; i < samplesRead; i++)
                        {
                            if (sampleIndex >= startSample && sampleIndex < endSample)
                            {
                                float volumeFactor = (float)Math.Pow(10.0, volumeDecibel / 20.0);
                                buffer[i] *= volumeFactor;
                            }

                            sampleIndex++;
                        }

                        writer.WriteSamples(buffer, 0, samplesRead);
                    }
                }
            }
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

        public static void CreateSilentWavAudio(string silenceAudioTemp, TimeSpan timeSpan, CancellationToken token)
        {
            // Calculate the number of samples needed for the silent audio
            int sampleRate = 44100; // Assuming a sample rate of 44100 Hz
            int numSamples = (int)(timeSpan.TotalSeconds * sampleRate);

            // Create an array to hold the silent audio samples
            float[] samples = new float[numSamples];

            // Write the silent audio samples to a WAV file
            using var writer = new WaveFileWriter(silenceAudioTemp, new WaveFormat(sampleRate, 16, 1));
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

        public static TimeSpan GetAudioDuration(string audioFile)
        {
            using var audioFileReader = new AudioFileReader(audioFile);
            return audioFileReader.TotalTime;
        }

        public static void GradualPanning(string inputFile, string outputFile, float panDuration, float switchInterval, float totalDuration)
        {
            // Load your audio file
            using var audioFileReader = new AudioFileReader(inputFile);
            // Create an instance of the GradualPanningSampleProvider
            var panningProvider = new GradualPanningSampleProvider(
                audioFileReader,
                panDuration,
                switchInterval,
                totalDuration
            );

            // Create a WaveFileWriter to save the processed audio
            using var waveFileWriter = new WaveFileWriter(outputFile, panningProvider.WaveFormat);
            // Create a buffer to hold the audio samples
            float[] buffer = new float[panningProvider.WaveFormat.SampleRate * panningProvider.WaveFormat.Channels];
            int samplesRead;

            // Read and process the audio samples
            while ((samplesRead = panningProvider.Read(buffer, 0, buffer.Length)) > 0)
            {
                waveFileWriter.WriteSamples(buffer, 0, samplesRead);
            }
        }

    }


}