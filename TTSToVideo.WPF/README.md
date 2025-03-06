# Developer
## You need to use secrets to configure Leonardo.AI Token

```pwsh
dotnet user-secrets init https://app.leonardo.ai/api-access
dotnet user-secrets set "LeonardoAIToken" "<your token>"
```


## This is for ElevenLab Token https://elevenlabs.io/ai-speech-classifier

```pwsh
dotnet user-secrets set "ElevenLabsToken" "<your token>"
```

## You need to install ffmpeg and add it tho PATH enviroment variable https://www.gyan.dev/ffmpeg/builds/


# How to use app

## Patterns to use in prompt

<V:10m> - Video with 10 minutes duration (without voice)

Prompt example:

"Hellow World <V:10m> How are you today."

In this example the video will have 10 minutes aways between the text "Hello World" and "How are you today."

