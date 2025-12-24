using CommunityToolkit.Mvvm.Input;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using TTSToVideo.Helpers.Implementations.Ffmpeg;
using TTSToVideo.WPF.Models;

namespace TTSToVideo.WPF.ViewsModels
{
    [AddINotifyPropertyChangedInterface]
    public class FontStyleViewModel
    {
        public FontStyleViewModel()
        {
            this.FontPosition = [];
            this.FontSize = null;

            foreach (var id in Enum.GetValues<FfmpegAlignment>())
            {
                this.FontPosition.Add(new FontPositionModel { Id = (int)id, Name = id.ToString() });
            }

            WindowClosedCommand = new AsyncRelayCommand(WindowClosed);
            WindowOpenedCommand = new AsyncRelayCommand(WindowOpened);

            int alignmentInt = (int)(Statement?.FontStyle?.Alignment ?? FfmpegAlignment.TopCenter);

            this.SelectedFontPosition = this.FontPosition?.FirstOrDefault(x => x.Id == alignmentInt)
                ?? throw new ArgumentNullException("Font position not found");
        }

        private async Task WindowOpened()
        {
            int alignmentInt = (int)(Statement?.FontStyle?.Alignment ?? FfmpegAlignment.TopCenter);

            this.SelectedFontPosition = this.FontPosition?.FirstOrDefault(x => x.Id == alignmentInt)
                ?? throw new ArgumentNullException("Font position not found");

            this.FontSize = Statement?.FontStyle?.FontSize;
            this.SubtitleVisible = Statement?.FontStyle?.SubtitleVisible ?? true;
            this.MarginV = Statement?.FontStyle?.MarginV; // load MarginV
            
            // Load colors with proper defaults
            var textColorHex = Statement?.FontStyle?.TextColor ?? "#FFFFFFFF";
            var backColorHex = Statement?.FontStyle?.BackgroundColor ?? "#64000000";
            
            Console.WriteLine($"FontStyleViewModel Loading - Text: {textColorHex}, Back: {backColorHex}");
            
            this.TextColor = ParseColorFromHex(textColorHex);
            this.BackgroundColor = ParseColorFromHex(backColorHex);
        }

        private async Task WindowClosed()
        {
            if (Statement != null)
            {
                var newTextColor = ColorToHex(this.TextColor);
                var newBackColor = ColorToHex(this.BackgroundColor);
                
                Console.WriteLine($"FontStyleViewModel Saving - Text: {newTextColor}, Back: {newBackColor}");
                
                if (Statement.FontStyle?.Alignment != (FfmpegAlignment)this.SelectedFontPosition.Id
                    || Statement.FontStyle?.FontSize != this.FontSize
                    || Statement.FontStyle?.SubtitleVisible != this.SubtitleVisible
                    || Statement.FontStyle?.MarginV != this.MarginV
                    || Statement.FontStyle?.TextColor != newTextColor
                    || Statement.FontStyle?.BackgroundColor != newBackColor
                    )
                {
                    var path = Statement?.Images?.FirstOrDefault()?.Path;
                    if (path != null)
                    {
                        path = $"{Path.Combine(Path.GetDirectoryName(path), "v-" + Path.GetFileNameWithoutExtension(path))}.wav.mp4";
                        if (File.Exists(path)) File.Delete(path);
                    }
                }
                Statement.FontStyle.Alignment = (FfmpegAlignment)this.SelectedFontPosition.Id;
                Statement.FontStyle.FontSize = this.FontSize;
                Statement.FontStyle.SubtitleVisible = this.SubtitleVisible;
                Statement.FontStyle.MarginV = this.MarginV;
                Statement.FontStyle.TextColor = newTextColor;
                Statement.FontStyle.BackgroundColor = newBackColor;
            }
            await Task.Delay(1);
        }

        private Color ParseColorFromHex(string hex)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                Console.WriteLine($"Parsed {hex} -> A:{color.A} R:{color.R} G:{color.G} B:{color.B}");
                return color;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to parse color {hex}: {ex.Message}, using White");
                return Colors.White;
            }
        }

        private string ColorToHex(Color? color)
        {
            if (color == null)
            {
                Console.WriteLine("Color is null, returning default white");
                return "#FFFFFFFF";
            }
            var hex = $"#{color.Value.A:X2}{color.Value.R:X2}{color.Value.G:X2}{color.Value.B:X2}";
            Console.WriteLine($"ColorToHex: A:{color.Value.A} R:{color.Value.R} G:{color.Value.G} B:{color.Value.B} -> {hex}");
            return hex;
        }

        public ObservableCollection<FontPositionModel> FontPosition { get; set; }
        public AsyncRelayCommand WindowClosedCommand { get; }
        public AsyncRelayCommand WindowOpenedCommand { get; }
        public FontPositionModel SelectedFontPosition { get; set; }
        public int? FontSize { get; set; }
        public bool SubtitleVisible { get; set; } = true;
        public int? MarginV { get; set; } // new MarginV editor
        public Color? TextColor { get; set; }
        public Color? BackgroundColor { get; set; }
        public StatementModel? Statement { get; set; }
    }
}
