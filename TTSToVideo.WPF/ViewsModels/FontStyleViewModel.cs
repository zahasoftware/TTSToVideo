using CommunityToolkit.Mvvm.Input;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TTSToVideo.Helpers;
using TTSToVideo.WPF.Models;

namespace TTSToVideo.WPF.ViewsModels
{
    [AddINotifyPropertyChangedInterface]
    public class FontStyleViewModel
    {
        public FontStyleViewModel()
        {
            this.FontPosition = new ObservableCollection<FontPositionModel>();

            foreach (var id in Enum.GetValues(typeof(FfmpegAlignment)))
            {
                this.FontPosition.Add(new FontPositionModel { Id = (int)id, Name = id.ToString() });
            }

            WindowClosedCommand = new AsyncRelayCommand(WindowClosed);

            int alignmentInt = (int)(Statement?.FontStyle?.Alignment ?? FfmpegAlignment.TopCenter);
            this.SelectedFontPosition = this.FontPosition?.FirstOrDefault(x => x.Id == alignmentInt)
                ?? throw new ArgumentNullException("Font position not found");
        }

        private async Task WindowClosed()
        {
            if (Statement != null)
            {
                Statement.FontStyle.Alignment = (FfmpegAlignment)this.SelectedFontPosition.Id;
            }
            await Task.Delay(1);
        }

        public ObservableCollection<FontPositionModel> FontPosition { get; set; }
        public AsyncRelayCommand WindowClosedCommand { get; }
        public FontPositionModel SelectedFontPosition { get; set; }
        public StatementModel? Statement { get; set; }
    }
}
