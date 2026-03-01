# Developer
## You need to use secrets to configure Leonardo.AI Token

```pwsh
dotnet user-secrets init 
dotnet user-secrets set "LeonardoAIToken" "<your token>"
```

## This is for ElevenLab Token https://elevenlabs.io/ai-speech-classifier

```pwsh
dotnet user-secrets set "ElevenLabsToken" "<your token>"
```

## Translator
### Azure
```pwsd
dotnet user-secrets set "AzureTranslator:Token" "<azure token translator>"
```

## Install ffmpeg

```pwsh
winget install ffmpeg
```

# How to use app

## Patterns to use in prompt

<V:10m> - Video with 10 minutes duration (without voice)

Prompt example:

"Hellow World <V:10m> How are you today."

In this example the video will have 10 minutes aways between the text "Hello World" and "How are you today."

# Publish
```pwsh
Remove-Item "C:\bin\tts2video\*" -Force -ErrorAction SilentlyContinue

dotnet publish TTSToVideo.WPF\TTSToVideo.WPF.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output "C:\bin\tts2video" `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:DebugType=embedded

# Rename
if (Test-Path "C:\bin\tts2video\TTSToVideo.WPF.exe") {
    Rename-Item "C:\bin\tts2video\TTSToVideo.WPF.exe" "C:\bin\tts2video\ttstovideo.exe" -Force
    Write-Host "Successfully created: C:\bin\tts2video\ttstovideo.exe" -ForegroundColor Green
}
```

# Configure secrets for deployed application
# Edit C:\bin\tts2video\appsettings.json and add your tokens:
# {
#   "LeonardoAIToken": "<your-token>",
#   "ElevenLabsToken": "<your-token>",
#   "AzureTranslator": {
#     "Token": "<your-token>"
#   }
# }


