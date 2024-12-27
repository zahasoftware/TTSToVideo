
# You need to use secrets to configure Leonardo.AI Token

dotnet user-secrets init https://app.leonardo.ai/api-access
dotnet user-secrets set "LeonardoAIToken" "<your token>"


# This is for ElevenLab Token https://elevenlabs.io/ai-speech-classifier
dotnet user-secrets set "ElevenLabsToken" "<your token>"

# You need to install ffmpeg and add it tho PATH enviroment variable https://www.gyan.dev/ffmpeg/builds/

