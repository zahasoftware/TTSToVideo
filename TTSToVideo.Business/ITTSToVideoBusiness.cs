using NetXP.Tts;
using TTSToVideo.Business.Models;

namespace TTSToVideo.Business
{
    public interface ITTSToVideoBusiness
    {
        Task GeneratePortraitVideoCommandExecute(string imagePath,
                                                  string outputPath);

        Task GeneratePortraitImageCommandExecute(Statement statement,
                                                  string[] imageModelIds,
                                                  string outputFolder,
                                                  CancellationToken token);

        Task ProcessCommandExecute(string projectPath
                                 , string projectName
                                 , string text
                                 , string negativePrompt
                                 , string globalPrompt
                                 , string[] imageModelId
                                 , TtsVoice selectedVoice
                                 , TTSToVideoPortraitParams portraitParams
                                 , TTSToVideoOptions options
                                 , CancellationToken token);

    }
}
