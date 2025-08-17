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

# Board


