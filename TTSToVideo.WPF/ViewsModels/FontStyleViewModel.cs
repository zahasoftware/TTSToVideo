using CommunityToolkit.Mvvm.Input;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        }

        private async Task WindowClosed()
        {
            if (Statement != null)
            {
                if (Statement.FontStyle?.Alignment != (FfmpegAlignment)this.SelectedFontPosition.Id
                    || Statement.FontStyle?.FontSize != this.FontSize
                    || Statement.FontStyle?.SubtitleVisible != this.SubtitleVisible
                    || Statement.FontStyle?.MarginV != this.MarginV
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
                Statement.FontStyle.MarginV = this.MarginV; // save MarginV
            }
            await Task.Delay(1);
        }

        public ObservableCollection<FontPositionModel> FontPosition { get; set; }
        public AsyncRelayCommand WindowClosedCommand { get; }
        public AsyncRelayCommand WindowOpenedCommand { get; }
        public FontPositionModel SelectedFontPosition { get; set; }
        public int? FontSize { get; set; }
        public bool SubtitleVisible { get; set; } = true;
        public int? MarginV { get; set; } // new MarginV editor
        public StatementModel? Statement { get; set; }
    }
}
