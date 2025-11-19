using NAudio.Midi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static TTSToVideo.Helpers.Implementations.Ffmpeg.FFMPEGHelpers;

namespace TTSToVideo.Helpers.Implementations.Ffmpeg
{
    public static class FFMPEGHelpers
    {

        public static async Task CreateVideoWithSubtitle(string outputPath, string text, string imagePath, TimeSpan duration, FfmpegOptions ffmpegOptions, CancellationToken token)
        {
            //if (!File.Exists(outputPath))
            {
                string subtitleFilePathRare = "";
                Process process;
                string error;
                string forceStyle = "";
                string subtitleFilePath = "";

                if (ffmpegOptions.FontStyle.SubtitleVisible == true)
                {
                    subtitleFilePath = Path.GetTempFileName();

                    File.WriteAllText(subtitleFilePath, $"1{Environment.NewLine}0:0:0.000 --> {duration:h\\:m\\:s\\.fff}{Environment.NewLine}{text}");

                    // Subtitles to ASS
                    process = new();
                    process.StartInfo.FileName = "ffmpeg";
                    process.StartInfo.Arguments = $" -i {subtitleFilePath} {subtitleFilePath}.ass";

                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;

                    process.Start();
                    await process.WaitForExitAsync(token);

                    error = process.StandardError.ReadToEnd();

                    Console.WriteLine("Error: " + error);

                    subtitleFilePathRare = subtitleFilePath
                                .Replace("\\", "\\\\\\\\")
                                .Replace(":", "\\:");

                    //Force_Style for ffmpeg
                    var options = new List<string>();
                    if (ffmpegOptions.FontStyle.Alignment != null)
                    {
                        options.Add($"Alignment={MapToAssAlignment(ffmpegOptions.FontStyle.Alignment)}");
                    }


                    if (ffmpegOptions.FontStyle.FontSize != null)
                    {
                        options.Add($"Fontsize={(byte)ffmpegOptions.FontStyle.FontSize.Value}");
                    }

                    if (ffmpegOptions.FontStyle.MarginV != null)
                    {
                        options.Add($"MarginV={(byte)ffmpegOptions.FontStyle.MarginV.Value}");
                    }

                    if (ffmpegOptions.FontStyle.MarginV != null)
                    {
                        options.Add($"MarginL={(byte)ffmpegOptions.FontStyle.MarginV.Value}");
                    }

                    if (ffmpegOptions.FontStyle.MarginR != null)
                    {
                        options.Add($"MarginR={(byte)ffmpegOptions.FontStyle.MarginR.Value}");
                    }

                    forceStyle = string.Join(",", options);

                    if (!string.IsNullOrEmpty(forceStyle))
                    {
                        forceStyle = $":force_style={forceStyle}";
                    }
                }

                var isVideo = Path.GetExtension(imagePath) == ".mp4";


                var videoDuration = duration;

                double inputVideoDuration = 0;
                if (isVideo)
                {
                    inputVideoDuration = duration.TotalSeconds / GetVideoDuration(imagePath).TotalSeconds + 1;
                    inputVideoDuration = Math.Ceiling(inputVideoDuration);
                }

                //If image path extension is a video, then i assing -loop option in a string
                string loop = isVideo ? $"-stream_loop {inputVideoDuration}" : "-loop 1";

                // Run FFmpeg process
                process = new Process();
                process.StartInfo.FileName = "ffmpeg";
                process.StartInfo.Arguments = $"{loop} -y" +
                                              $" -i \"{imagePath}\" " +
                                              $" -f lavfi " +
                                              $" -i anullsrc=r=44100:cl=stereo " +
                                              $" -t \"{videoDuration:h\\:m\\:s\\.fff}\" " +
                                              (ffmpegOptions.FontStyle.SubtitleVisible == true ? $"-vf \"subtitles='{subtitleFilePathRare}.ass':force_style='{forceStyle}'\" " : "") +
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

                if (!string.IsNullOrEmpty(subtitleFilePath))
                {
                    File.Delete(subtitleFilePath);
                }
            }
        }

        public static async Task CreateVideo(
            string outputPath,
            string imagePath,
            TimeSpan duration,
            CancellationToken token,
            string preset = "veryfast",
            bool fastStart = true,
            bool tuneStillImage = true)
        {
            var isVideo = string.Equals(Path.GetExtension(imagePath), ".mp4", StringComparison.OrdinalIgnoreCase);
            double loops = 0;
            if (isVideo)
            {
                var srcDur = GetVideoDuration(imagePath).TotalSeconds;
                if (srcDur <= 0) throw new InvalidOperationException("Source video duration unknown.");
                // -stream_loop N repeats N additional times (so total plays = N+1)
                loops = Math.Ceiling(duration.TotalSeconds / srcDur) - 1;
                if (loops < 0) loops = 0;
            }
            string loopArg = isVideo ? $"-stream_loop {loops}" : "-loop 1";

            var sb = new StringBuilder();
            sb.Append(loopArg).Append(" -y ");
            sb.Append("-i ").Append('"').Append(imagePath).Append("\" ");
            sb.Append("-f lavfi -i anullsrc=r=44100:cl=stereo ");
            sb.Append("-t ").Append('"').Append(duration.ToString("hh\\:mm\\:ss\\.fff")).Append("\" ");
            if (tuneStillImage && !isVideo) sb.Append("-tune stillimage ");
            sb.Append("-r 30 ");
            sb.Append("-c:v libx264 ");
            sb.Append("-preset ").Append(preset).Append(' ');
            if (fastStart) sb.Append("-movflags +faststart ");
            sb.Append("-shortest ");
            sb.Append('"').Append(outputPath).Append('"');

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = sb.ToString(),
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            process.Start();

            // Incremental stderr read for progress (optional)
            var stderr = new StringBuilder();
            while (!process.HasExited)
            {
                var line = await process.StandardError.ReadLineAsync(token);
                if (line == null) break;
                stderr.AppendLine(line);
                // Could parse "time=..." for progress here
            }

            await process.WaitForExitAsync(token);
            if (process.ExitCode != 0)
                throw new Exception("ffmpeg failed: " + stderr);
        }

        public static async Task MixAudioWithVideo(string videoFilePath, string audioFilePath, string outputFilePath, CancellationToken token)
        {
            // Check if ffmpeg executable exists in the system PATH
            if (!IsFFmpegAvailable())
            {
                throw new FileNotFoundException("ffmpeg executable not found. Make sure it's installed and added to the system PATH.");
            }

            // Execute ffmpeg command to mix audio with video
            string arguments = $"-i \"{videoFilePath}\" -i \"{audioFilePath}\" -c:v copy -c:a aac -y " +
                               "-filter_complex \"[0:a][1:a]amix=inputs=2:duration=longest:normalize=0 [audio_out]\" " +
                               $"-map 0:v -map \"[audio_out]\" \"{outputFilePath}\"";



            var process = new Process();
            process.StartInfo.FileName = "ffmpeg";
            process.StartInfo.Arguments = arguments;

            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();

            process.BeginOutputReadLine();
            string tmpErrorOut = await process.StandardError.ReadToEndAsync(token);

            await process.WaitForExitAsync(token);

            if (tmpErrorOut.Contains("Error"))
            {
                Console.WriteLine("Error: " + tmpErrorOut);
                throw new Exception("Error when try to mix audio with video", new Exception(tmpErrorOut));
            }

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

        public static async Task JoiningVideos(
            string[] videoPaths,
            string outputPath,
            FfmpegOptions options,
            CancellationToken token
        )
        {
            if (videoPaths.Length == 1)
            {
                File.Copy(videoPaths[0], outputPath, true);
                return;
            }

            // for each outputPath maps their file names on temp files to reduce the fiel path size 
            List<string> tempFiles = [];
            for (int i = 0; i < videoPaths.Length; i++)
            {
                var tempFile = Path.Combine(Path.GetTempPath(), $"vid_{i}");
                File.Copy(videoPaths[i], tempFile, true);
                tempFiles.Add(tempFile);
            }

            // Build input arguments
            string inputArgs = string.Join(" ", tempFiles.Select(v => $"-i \"{v}\""));

            // Generate filter_complex for concatenation
            string scale = $"{options.WidthResolution}:{options.HeightResolution}";
            var filterParts = new List<string>();
            for (int i = 0; i < tempFiles.Count; i++)
            {
                filterParts.Add($"[{i}:v]scale={scale},setsar=1[v{i}]");
            }

            string videoInputs = string.Join("", tempFiles.Select((_, i) => $"[v{i}][{i}:a]"));
            string filterComplex = string.Join(";", filterParts) +
                                   $";{videoInputs}concat=n={tempFiles.Count}:v=1:a=1[vv][a];" +
                                   $"[vv]fps=30,format=yuv420p[v]";

            // Construct FFmpeg command
            string ffmpegCmd = $"{options.AdditionalArgs} {inputArgs} " +
                               $"-filter_complex \"{filterComplex}\" " +
                               "-map \"[v]\" -map \"[a]\" -c:v libx264 -y " +
                               $"\"{outputPath}\"";

            // Run FFmpeg process
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = """ffmpeg""",
                    Arguments = ffmpegCmd,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            string errorOutput = await process.StandardError.ReadToEndAsync(token);

            if (errorOutput.Contains("Error"))
            {
                throw new Exception(errorOutput);
            }

            await process.WaitForExitAsync(token);

            // Remove each temp file after processing
            foreach (var tempFile in tempFiles)
            {
                try
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                catch
                {
                    // Ignore errors on cleanup
                }
            }
        }

        public static async Task GenerateVideoWithImage(string outputPath, string inputImagePath, TimeSpan? duration, CancellationToken token)
        {
            if (duration == null)
            {
                return;
            }

            var isVideo = Path.GetExtension(inputImagePath) == ".mp4";

            double inputVideoDuration = 0;
            if (isVideo)
            {
                inputVideoDuration = duration.Value.TotalSeconds / GetVideoDuration(inputImagePath).TotalSeconds + 1;
                inputVideoDuration = Math.Ceiling(inputVideoDuration);
            }

            //If image path extension is a video, then i assing -loop option in a string
            string loop = isVideo ? $"-stream_loop {inputVideoDuration}" : "-loop 1";

            var p = new Process();
            p.StartInfo.FileName = "ffmpeg";
            p.StartInfo.Arguments = $"{loop} -y" +
                                    $" -i \"{inputImagePath}\" " +
                                    $" -f lavfi " +
                                    $" -t \"{duration:h\\:m\\:s\\.fff}\" " +
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

        private static readonly string[] separator = ["Duration: "];

        static TimeSpan GetVideoDuration(string filePath)
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = "ffmpeg",
                Arguments = $"-i \"{filePath}\"",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(startInfo) ?? throw new ApplicationException($"{startInfo.FileName} cannot be executed , null");
            using StreamReader reader = process.StandardError;

            string result = reader.ReadToEnd();
            string duration = result.Split(separator, StringSplitOptions.None)[1].Split(',')[0];
            return ParseDuration(duration.Trim());
        }

        static TimeSpan ParseDuration(string duration)
        {
            return TimeSpan.ParseExact(duration, @"hh\:mm\:ss\.ff", CultureInfo.InvariantCulture);
        }

        public record AssSubtitleSegment(TimeSpan Duration, string Text, FfmpegFontStyle? Style = null);


        private static string FormatAssTime(TimeSpan t) =>
            $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}.{t.Milliseconds / 10:D2}";

        // Add a helper to compute dynamic ASS subtitle sizing based on target resolution.
        private static (int playResX, int playResY, int fontSize, int marginV, int marginLH) ComputeSubtitleScale(
            int targetWidth,
            int targetHeight,
            int? requestedFontSize,
            int? marginV,
            int? marginL,
            int? marginR)
        {
            int playResX = targetWidth;
            int playResY = targetHeight;

            // Old logic artificially added +60 which compressed variation between low/high user values.
            // New logic: treat the user provided FontSize (expected range 0..50) as a semantic scale
            // and map it to a percentage of the video height, giving a much broader visual spread.
            // If user does not provide a size -> fallback to a default % of height.
            int fs;
            if (requestedFontSize is null)
            {
                // Default ~7% of height (tuned) when no explicit font size is set.
                fs = (int)Math.Round(playResY * 0.07);
            }
            else
            {
                // Clamp user input into expected domain
                int user = Math.Clamp(requestedFontSize.Value, 0, 50);
                // Map user (0..50) -> percent range (2% .. 13%) of height (customizable)
                // This makes upper values substantially larger visually.
                double minPct = 0.02;   // 2%
                double maxPct = 0.13;   // 13%  (1920 => 250px approx)
                double pct = minPct + (user / 50.0) * (maxPct - minPct);
                fs = (int)Math.Round(playResY * pct);
            }

            // Clamp to reasonable pixel bounds to avoid extreme values for unusual resolutions
            fs = Math.Clamp(fs, 24, 300);

            // Margins: if user supplied explicit margins, honor them; otherwise compute dynamic defaults.
            // Vertical margin default ~6% of height (provides breathing room for larger fonts)
            int mv = marginV ?? (int)Math.Round(playResY * 0.06);
            // Horizontal margin default ~4% of width
            int ml = marginL ?? (int)Math.Round(playResX * 0.04);
            int mr = marginR ?? ml;

            return (playResX, playResY, fs, mv, Math.Min(ml, mr));
        }

        // Modify CreateAssSubtitleFile to use the new scaling logic.
        public static string CreateAssSubtitleFile(IEnumerable<AssSubtitleSegment> segments, string outputPath = "", int targetWidth = 1080, int targetHeight = 1920)
        {
            if (segments == null || !segments.Any())
                throw new ArgumentException("No subtitle segments provided", nameof(segments));

            string path = outputPath;
            if (string.IsNullOrWhiteSpace(path))
                path = Path.ChangeExtension(Path.GetTempFileName(), ".ass");
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                path = Path.ChangeExtension(path, ".ass");
            }

            // Derive a “global” representative style (first visible segment that has a style).
            var firstStyled = segments.FirstOrDefault(s => s.Style is { SubtitleVisible: not false });

            var (playResX, playResY, baseFontSize, baseMarginV, baseMarginLH) = ComputeSubtitleScale(
                targetWidth,
                targetHeight,
                firstStyled?.Style?.FontSize,
                firstStyled?.Style?.MarginV,
                firstStyled?.Style?.MarginL,
                firstStyled?.Style?.MarginR);

            var sb = new StringBuilder();
            sb.AppendLine("[Script Info]");
            sb.AppendLine("ScriptType: v4.00+");
            sb.AppendLine($"PlayResX: {playResX}");
            sb.AppendLine($"PlayResY: {playResY}");
            sb.AppendLine("ScaledBorderAndShadow: yes");
            sb.AppendLine();

            sb.AppendLine("[V4+ Styles]");
            sb.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");

            // Base style (Alignment default bottom center = 2)
            sb.AppendLine($"Style: Default,Arial,{baseFontSize},&H00FFFFFF,&H000000FF,&H00000000,&H64000000," +
                          "0,0,0,0,100,100,0,0,1,2,0," +
                          $"{MapToAssAlignment(firstStyled?.Style?.Alignment ?? FfmpegAlignment.BottomCenter)},{baseMarginLH},{baseMarginLH},{baseMarginV},1");

            sb.AppendLine();
            sb.AppendLine("[Events]");
            sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");

            TimeSpan currentTime = TimeSpan.Zero;
            int styleCounter = 1;
            var styleMap = new Dictionary<string, string>();

            foreach (var seg in segments)
            {
                var start = currentTime;
                var end = currentTime + seg.Duration;
                currentTime = end;

                if (seg.Style?.SubtitleVisible == false)
                    continue;

                string styleName = "Default";

                if (seg.Style != null)
                {
                    var (prx, pry, fs, mv, mlh) = ComputeSubtitleScale(
                        targetWidth,
                        targetHeight,
                        seg.Style.FontSize,
                        seg.Style.MarginV,
                        seg.Style.MarginL,
                        seg.Style.MarginR);

                    string key = $"{fs}-{mv}-{mlh}-{seg.Style.Alignment}";
                    if (!styleMap.TryGetValue(key, out styleName))
                    {
                        styleName = $"Style_{styleCounter++}";
                        styleMap[key] = styleName;
                        sb.Insert(sb.ToString().IndexOf("[Events]"),
                            $"Style: {styleName},Arial,{fs},&H00FFFFFF,&H000000FF,&H00000000,&H64000000," +
                            "0,0,0,0,100,100,0,0,1,2,0," +
                            $"{MapToAssAlignment(seg.Style?.Alignment ?? FfmpegAlignment.BottomCenter)}," +
                            $"{mlh},{mlh},{mv},1\n");
                    }
                }

                sb.AppendLine($"Dialogue: 0,{FormatAssTime(start)},{FormatAssTime(end)},{styleName},,0,0,0,,{(string.IsNullOrEmpty(seg.Text) ? "" : seg.Text.Replace("\n"," "))}");
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            return path;
        }

        // Replace the existing InjectSubtitlesAsync (the 4‑parameter one) with this version
        public static async Task<string> InjectSubtitlesAsync(
            string inputVideo,
            string subtitleFile,
            string outputVideo,
            bool burnIn = false)
        {
            if (!File.Exists(inputVideo))
                throw new FileNotFoundException("Input video not found", inputVideo);
            if (!File.Exists(subtitleFile))
                throw new FileNotFoundException("Subtitle file not found", subtitleFile);

            Directory.CreateDirectory(Path.GetDirectoryName(outputVideo)!);

            string inputExt = Path.GetExtension(subtitleFile).ToLowerInvariant();
            bool isAss = inputExt == ".ass";
            bool wantsMp4 = outputVideo.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);

            // 1. If ASS + soft subtitles + MP4 target → you CANNOT keep ASS styles. Either:
            //    a) Burn-in, or
            //    b) Downgrade to SRT (styles lost).
            // We choose: if isAss && wantsMp4 && !burnIn -> auto burn-in unless caller overrides.
            if (isAss && wantsMp4 && !burnIn)
                burnIn = true; // avoids silent creation of a .mkv you never look at

            string workingOutput = outputVideo;
            string args;

            if (burnIn)
            {
                // Burn-in path must be escaped for filter
                string escaped = subtitleFile.Replace("\\", "\\\\\\\\")
                                             .Replace(":", "\\:");
                // (Optional) You could build force_style dynamically here
                args = $"-y -i \"{inputVideo}\" -vf \"subtitles='{escaped}'\" -c:v libx264 -c:a copy \"{workingOutput}\"";
            }
            else if (isAss)
            {
                // Keep container consistent: if caller asked for .mp4, switch to .mkv AND return new path
                if (wantsMp4)
                {
                    workingOutput = Path.ChangeExtension(outputVideo, ".mkv");
                }
                args = $"-y -i \"{inputVideo}\" -i \"{subtitleFile}\" -c copy -c:s ass \"{workingOutput}\"";
            }
            else
            {
                // SRT (or other) → soft mux
                string subCodec = wantsMp4 ? "mov_text" : "copy";
                args = $"-y -i \"{inputVideo}\" -i \"{subtitleFile}\" -c copy -c:s {subCodec} \"{workingOutput}\"";
            }

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            _ = process.StandardOutput.ReadToEndAsync();
            string err = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 || err.Contains("Error", StringComparison.OrdinalIgnoreCase))
                throw new Exception($"ffmpeg failed injecting subtitles. ExitCode={process.ExitCode}. Details: {err}");

            return workingOutput;
        }

        // Add near other private helpers (e.g., above CreateAssSubtitleFile)
        private static int MapToAssAlignment(FfmpegAlignment? a)
        {
            if (a is null) return 2; // BottomCenter default
            return a switch
            {
                FfmpegAlignment.BottomLeft   => 1,
                FfmpegAlignment.BottomCenter => 2,
                FfmpegAlignment.BottomRight  => 3,
                FfmpegAlignment.MiddleLeft   => 4,
                FfmpegAlignment.MiddleCenter => 5,
                FfmpegAlignment.MiddleRight  => 6,
                FfmpegAlignment.TopLeft      => 7,
                FfmpegAlignment.TopCenter    => 8,
                FfmpegAlignment.TopRight     => 9,
                _ => 2
            };
        }
    }
}