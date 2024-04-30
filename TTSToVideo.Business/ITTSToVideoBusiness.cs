using TTSToVideo.Business.Models;

namespace TTSToVideo.Business
{
    public interface ITTSToVideoBusiness
    {
        Task GeneratePortraitVideoCommandExecute(string imagePath, string outputPath);
        Task GeneratePortraitImageCommandExecute(Statement statement, string outputFolder, CancellationToken token);
        Task ProcessCommandExecute(string projectPath
                                               , string projectName
                                               , string text
                                               , string negativePrompt
                                               , bool portraitEnable
                                               , string portraitText
                                               , string portraitVoice
                                               , string portraitImagePath
                                               , string portraitVideoPath
                                               , string musicDir
                                               , CancellationToken token);

    }
}
