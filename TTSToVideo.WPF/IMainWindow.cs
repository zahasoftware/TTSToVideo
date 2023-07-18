using TTSToVideo.WPF.ViewModel;

namespace TTSToVideo
{
    internal interface IMainWindow
    {
        IVMMainWindow ViewModel { get; }

        void Show();
    }
}