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
            this.FontSize = 12;

            foreach (var id in Enum.GetValues(typeof(FfmpegAlignment)))
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
        }

        private async Task WindowClosed()
        {
            if (Statement != null)
            {
                if (Statement.FontStyle?.Alignment != (FfmpegAlignment)this.SelectedFontPosition.Id
                    || Statement.FontStyle?.FontSize != this.FontSize) 
                {
                    var path = Statement?.Images?.FirstOrDefault()?.Path;
                    if (path != null)
                    {
                        File.Delete($"{path}.mp4");
                        File.Delete($"{path}.wav.mp4");
                    }
                }
                Statement.FontStyle.Alignment = (FfmpegAlignment)this.SelectedFontPosition.Id;
                Statement.FontStyle.FontSize = this.FontSize;
            }
            await Task.Delay(1);
        }

        public ObservableCollection<FontPositionModel> FontPosition { get; set; }
        public AsyncRelayCommand WindowClosedCommand { get; }
        public AsyncRelayCommand WindowOpenedCommand { get; }
        public FontPositionModel SelectedFontPosition { get; set; }
        public int? FontSize { get; set; }
        public StatementModel? Statement { get; set; }
    }
}
