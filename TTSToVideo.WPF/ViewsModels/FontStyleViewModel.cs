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
        private string? _originalBackgroundColorHex;
        private string? _originalTextColorHex;
        private string? _originalSecondaryColourHex;
        private string? _originalOutlineColourHex;

        public FontStyleViewModel()
        {
            this.FontPosition = [];
            this.FontSize = null;

            foreach (var id in Enum.GetValues<FfmpegAlignment>())
            {
                this.FontPosition.Add(new FontPositionModel { Id = (int)id, Name = id.ToString() });
            }

            // Initialize border style options
            ObservableCollection<BorderStyleModel> borderStyleModels = [
                new BorderStyleModel { Id = 1, Name = "Outline + Shadow" },
                new BorderStyleModel { Id = 3, Name = "Opaque Box" },
                new BorderStyleModel { Id = 4, Name = "Outline + Opaque Box" }
            ];
            BorderStyleOptions = borderStyleModels;

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
            this.MarginV = Statement?.FontStyle?.MarginV;
            this.MarginL = Statement?.FontStyle?.MarginL;
            this.MarginR = Statement?.FontStyle?.MarginR;

            // Load colors with proper defaults
            _originalTextColorHex = Statement?.FontStyle?.TextColor ?? throw new Exception("Color not defined");
            _originalBackgroundColorHex = Statement?.FontStyle?.BackgroundColor ?? throw new Exception("Color not defined");
            _originalSecondaryColourHex = Statement?.FontStyle?.SecondaryColour ?? throw new Exception("Color not defined");
            _originalOutlineColourHex = Statement?.FontStyle?.OutlineColour ?? throw new Exception("Color not defined");
            
            this.TextColor = ParseColorFromHex(_originalTextColorHex);
            this.BackgroundColor = ParseColorFromHex(_originalBackgroundColorHex);
            this.SecondaryColour = ParseColorFromHex(_originalSecondaryColourHex);
            this.OutlineColour = ParseColorFromHex(_originalOutlineColourHex);

            // Load new style properties
            this.Fontname = Statement?.FontStyle?.Fontname ?? "Arial";
            this.Bold = Statement?.FontStyle?.Bold ?? 0;
            this.Italic = Statement?.FontStyle?.Italic ?? 0;
            this.Underline = Statement?.FontStyle?.Underline ?? 0;
            this.StrikeOut = Statement?.FontStyle?.StrikeOut ?? 0;
            this.ScaleX = Statement?.FontStyle?.ScaleX ?? 100;
            this.ScaleY = Statement?.FontStyle?.ScaleY ?? 100;
            this.Spacing = Statement?.FontStyle?.Spacing ?? 0;
            this.Angle = Statement?.FontStyle?.Angle ?? 0;
            this.BorderStyle = Statement?.FontStyle?.BorderStyle ?? 3;
            this.Outline = Statement?.FontStyle?.Outline ?? 4;
            this.Shadow = Statement?.FontStyle?.Shadow ?? 0;

            SelectedBorderStyle = BorderStyleOptions.FirstOrDefault(x => x.Id == this.BorderStyle);
        }

        private async Task WindowClosed()
        {
            if (Statement != null)
            {
                var newTextColor = ColorToHexPreservingAlpha(this.TextColor, _originalTextColorHex);
                var newBackColor = ColorToHexPreservingAlpha(this.BackgroundColor, _originalBackgroundColorHex);
                var newSecondaryColour = ColorToHexPreservingAlpha(this.SecondaryColour, _originalSecondaryColourHex);
                var newOutlineColour = ColorToHexPreservingAlpha(this.OutlineColour, _originalOutlineColourHex);
                
                if (Statement.FontStyle?.Alignment != (FfmpegAlignment)this.SelectedFontPosition.Id
                    || Statement.FontStyle?.FontSize != this.FontSize
                    || Statement.FontStyle?.SubtitleVisible != this.SubtitleVisible
                    || Statement.FontStyle?.MarginV != this.MarginV
                    || Statement.FontStyle?.MarginL != this.MarginL
                    || Statement.FontStyle?.MarginR != this.MarginR
                    || Statement.FontStyle?.TextColor != newTextColor
                    || Statement.FontStyle?.BackgroundColor != newBackColor
                    || Statement.FontStyle?.SecondaryColour != newSecondaryColour
                    || Statement.FontStyle?.OutlineColour != newOutlineColour
                    || Statement.FontStyle?.Fontname != this.Fontname
                    || Statement.FontStyle?.Bold != this.Bold
                    || Statement.FontStyle?.Italic != this.Italic
                    || Statement.FontStyle?.Underline != this.Underline
                    || Statement.FontStyle?.StrikeOut != this.StrikeOut
                    || Statement.FontStyle?.ScaleX != this.ScaleX
                    || Statement.FontStyle?.ScaleY != this.ScaleY
                    || Statement.FontStyle?.Spacing != this.Spacing
                    || Statement.FontStyle?.Angle != this.Angle
                    || Statement.FontStyle?.BorderStyle != this.BorderStyle
                    || Statement.FontStyle?.Outline != this.Outline
                    || Statement.FontStyle?.Shadow != this.Shadow)
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
                Statement.FontStyle.MarginL = this.MarginL;
                Statement.FontStyle.MarginR = this.MarginR;
                Statement.FontStyle.TextColor = newTextColor;
                Statement.FontStyle.BackgroundColor = newBackColor;
                Statement.FontStyle.SecondaryColour = newSecondaryColour;
                Statement.FontStyle.OutlineColour = newOutlineColour;
                Statement.FontStyle.Fontname = this.Fontname;
                Statement.FontStyle.Bold = this.Bold;
                Statement.FontStyle.Italic = this.Italic;
                Statement.FontStyle.Underline = this.Underline;
                Statement.FontStyle.StrikeOut = this.StrikeOut;
                Statement.FontStyle.ScaleX = this.ScaleX;
                Statement.FontStyle.ScaleY = this.ScaleY;
                Statement.FontStyle.Spacing = this.Spacing;
                Statement.FontStyle.Angle = this.Angle;
                Statement.FontStyle.BorderStyle = this.BorderStyle;
                Statement.FontStyle.Outline = this.Outline;
                Statement.FontStyle.Shadow = this.Shadow;
            }
            await Task.Delay(1);
        }

        private Color ParseColorFromHex(string hex)
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(hex);
            }
            catch
            {
                return Colors.White;
            }
        }

        private string ColorToHexPreservingAlpha(Color? color, string? originalHex)
        {
            if (color == null)
            {
                return originalHex ?? "#FFFFFFFF";
            }

            // Get the current color as hex
            string currentHex = $"#{color.Value.A:X2}{color.Value.R:X2}{color.Value.G:X2}{color.Value.B:X2}";

            return currentHex;
        }

        public ObservableCollection<FontPositionModel> FontPosition { get; set; }
        public ObservableCollection<BorderStyleModel> BorderStyleOptions { get; set; }
        public AsyncRelayCommand WindowClosedCommand { get; }
        public AsyncRelayCommand WindowOpenedCommand { get; }
        public FontPositionModel SelectedFontPosition { get; set; }
        public BorderStyleModel? SelectedBorderStyle { get; set; }
        public int? FontSize { get; set; }
        public bool SubtitleVisible { get; set; } = true;
        public int? MarginV { get; set; }
        public int? MarginL { get; set; }
        public int? MarginR { get; set; }
        public Color? TextColor { get; set; }
        public Color? BackgroundColor { get; set; }
        public Color? SecondaryColour { get; set; }
        public Color? OutlineColour { get; set; }
        public string? Fontname { get; set; }
        public int? Bold { get; set; }
        public int? Italic { get; set; }
        public int? Underline { get; set; }
        public int? StrikeOut { get; set; }
        public int? ScaleX { get; set; }
        public int? ScaleY { get; set; }
        public double? Spacing { get; set; }
        public double? Angle { get; set; }
        public int? BorderStyle { get; set; }
        public int? Outline { get; set; }
        public int? Shadow { get; set; }
        public StatementModel? Statement { get; set; }
    }

    public class BorderStyleModel
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }
}
