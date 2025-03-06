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
                                                 TTSToVideoOptions options,
                                                 CancellationToken token);

        Task<List<Statement>> ProcessCommandExecute(string projectPath
                                 , string projectName
                                 , string statements
                                 , string negativePrompt
                                 , string globalPrompt
                                 , string selectedMusicFile
                                 , string[] imageModelId
                                 , TtsVoice selectedVoice
                                 , bool portraitEnabled
                                 , TTSToVideoOptions options
                                 , CancellationToken token);
    }
}
