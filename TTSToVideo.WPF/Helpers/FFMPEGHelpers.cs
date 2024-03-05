﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TTSToVideo.WPF.Helpers;

internal static class FFMPEGHelpers
{
    public static async Task CreateVideoWithSubtitle(string outputPath, string text, string imagePath, TimeSpan duration, FfmpegOptions ffmpegOptions, CancellationToken token)
    {
        if (!File.Exists(outputPath))
        {
            string subtitleFilePath = Path.GetTempFileName();

            File.WriteAllText(subtitleFilePath, $"1{Environment.NewLine}0:0:0.000 --> {duration:h\\:m\\:s\\.fff}{Environment.NewLine}{text}");

            // Subtitles to ASS
            Process process = new();
            process.StartInfo.FileName = "ffmpeg";
            process.StartInfo.Arguments = $" -i {subtitleFilePath} {subtitleFilePath}.ass";

            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            process.Start();
            await process.WaitForExitAsync(token);

            var error = process.StandardError.ReadToEnd();

            Console.WriteLine("Error: " + error);

            var subtitleFilePathRare = subtitleFilePath
                        .Replace("\\", "\\\\\\\\")
                        .Replace(":", "\\:");



            //Force_Style for ffmpeg
            var options = new List<string>();
            if (ffmpegOptions.FontStyle.Alignment != null)
            {
                options.Add($"Alignment={(byte)ffmpegOptions.FontStyle.Alignment.Value}");
            }

            if (ffmpegOptions.FontStyle.MarginV != null)
            {
                options.Add($"MarginV={(byte)ffmpegOptions.FontStyle.MarginV.Value}");
            }

            if (ffmpegOptions.FontStyle.FontSize != null)
            {
                options.Add($"Fontsize={(byte)ffmpegOptions.FontStyle.FontSize.Value}");
            }

            var forceStyle = string.Join(",", options);

            if (!string.IsNullOrEmpty(forceStyle))
            {
                forceStyle = $":force_style={forceStyle}";
            }

            // Run FFmpeg process
            process = new Process();
            process.StartInfo.FileName = "ffmpeg";
            process.StartInfo.Arguments = "-loop 1 -y" +
                                          $" -i \"{imagePath}\" " +
                                          $" -f lavfi " +
                                          $" -i anullsrc=r=44100:cl=stereo " +
                                          $" -t \"{duration:h\\:m\\:s\\.fff}\" " +
                                          $"-vf \"subtitles='{subtitleFilePathRare}.ass':force_style='{forceStyle}'\" " +
                                          $"-r 30 " +
                                          $"-c:v libx264 " +
                                          $"-shortest \"{outputPath}\"";

            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();

            process.BeginOutputReadLine();
            error = process.StandardError.ReadToEnd();

            if (error.Contains("Error"))
            {
                throw new Exception("Error when try to create video with image", new Exception(error));
            }

            await process.WaitForExitAsync(token);

            File.Delete(subtitleFilePath);
        }
    }

    public static async Task MixAudioWithVideo(string videoFilePath, string audioFilePath, string outputFilePath, CancellationToken token)
    {
        // Check if ffmpeg executable exists in the system PATH
        if (!IsFFmpegAvailable())
        {
            throw new FileNotFoundException("ffmpeg executable not found. Make sure it's installed and added to the system PATH.");
        }

        // Execute ffmpeg command to mix audio with video
        string arguments = $"-i \"{videoFilePath}\" -i \"{audioFilePath}\" -c:v copy -c:a aac -y -filter_complex \"[0:a][1:a] amix=inputs=2:duration=longest [audio_out]\" -map 0:v -map \"[audio_out]\" \"{outputFilePath}\"";

        var process = new Process();
        process.StartInfo.FileName = "ffmpeg";
        process.StartInfo.Arguments = arguments;

        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.Start();

        process.BeginOutputReadLine();
        string tmpErrorOut = process.StandardError.ReadToEnd();

        await process.WaitForExitAsync(token);

        Console.WriteLine("Error: " + tmpErrorOut);
    }

    public static bool IsFFmpegAvailable()
    {
        try
        {
            using Process process = new();
            process.StartInfo.FileName = "ffmpeg";
            process.StartInfo.Arguments = "-version";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static async Task JoiningVideos(
          string videoAPath
        , string videoBPath
        , string outputPath
        , FfmpegOptions options
        , CancellationToken token
        )
    {
        Process process;
        // Build the FFmpeg command to merge the videos
        string scale = $"{options.WidthResolution}:{options.HeightResolution}";
        string filter = $"[0:v]scale={scale},setsar=1[v0];[1:v]scale={scale},setsar=1[v1];[v0][0:a][v1][1:a]concat=n=2:v=1:a=1[vv][a];[vv]fps=30,format=yuv420p[v]";


        string ffmpegCmd = $" {options.AdditionalArgs} -i \"{videoAPath}\" -i \"{videoBPath}\" " +
                           $" -filter_complex {filter}" +
                           $" -map \"[v]\" -map \"[a]\" -c:v libx264 -y" +
                           $" \"{outputPath}\"";

        // Run FFmpeg process
        process = new Process();
        process.StartInfo.FileName = "ffmpeg";
        process.StartInfo.Arguments = ffmpegCmd;

        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.Start();

        process.BeginOutputReadLine();
        string tmpErrorOut = process.StandardError.ReadToEnd();
        if (tmpErrorOut.Contains("Error"))
        {
            throw new Exception(tmpErrorOut);
        }
        await process.WaitForExitAsync(token);
    }

    public static async Task GenerateVideoWithImage(string outputPath, string inputImagePath, CancellationToken token)
    {
        var p = new Process();
        p.StartInfo.FileName = "ffmpeg";
        p.StartInfo.Arguments = $"-loop 1 -y" +
                                $" -i \"{inputImagePath}\" " +
                                $" -f lavfi " +
                                $" -t 10 " +
                                $" -i anullsrc=r=44100:cl=stereo " +
                                $"-c:v libx264 " +
                                $"-shortest \"{outputPath}\"";

        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.RedirectStandardError = true;
        p.Start();

        p.BeginOutputReadLine();
        string tmpErrorOut = p.StandardError.ReadToEnd();
        if (tmpErrorOut.Contains("Error"))
        {
            throw new Exception(tmpErrorOut);
        }

        await p.WaitForExitAsync(token);
    }


}