using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

// This code was based on the continous speech followed in this documentation:
// https://learn.microsoft.com/en-us/azure/ai-services/speech-service/how-to-recognize-speech?pivots=programming-language-csharp

public class SpeechToText : MonoBehaviour
{
    public string speechKey;
    public string speechRegion;
    public static SpeechToText Instance;
    static SpeechConfig speechConfig;
    static AudioConfig audioConfig;
    static SpeechRecognizer speechRecognizer;
    void StartSpeech()
    {
        speechConfig = SpeechConfig.FromSubscription(Instance.speechKey, Instance.speechRegion);
        audioConfig = AudioConfig.FromDefaultMicrophoneInput();
        speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);

        speechRecognizer.Recognizing += (s, e) =>
        {
            //Debug.Log($"RECOGNIZING: Text={e.Result.Text}");
        };

        speechRecognizer.Recognized += (s, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech)
            {
                Debug.Log($"RECOGNIZED: Text={e.Result.Text}");
                CharacterAIBehaviour.CallOnSpeechToTextRecognized(e.Result.Text);
            }
            else if (e.Result.Reason == ResultReason.NoMatch)
            {
                Debug.Log($"NOMATCH: Speech could not be recognized.");
            }
        };

        speechRecognizer.Canceled += (s, e) =>
        {
            Debug.Log($"CANCELED: Reason={e.Reason}");

            if (e.Reason == CancellationReason.Error)
            {
                Debug.Log($"CANCELED: ErrorCode={e.ErrorCode}");
                Debug.Log($"CANCELED: ErrorDetails={e.ErrorDetails}");
                Debug.Log($"CANCELED: Did you set the speech resource key and region values?");
            }
        };

        speechRecognizer.SessionStopped += (s, e) =>
        {
            Debug.Log("\nSession stopped event.");
        };

        speechRecognizer.StartContinuousRecognitionAsync();
        print("It worked?");
        print(speechRecognizer);
    }
    void StopSpeech()
    {
        speechRecognizer.StopContinuousRecognitionAsync();
    }
    private void Start()
    {
        Instance = this;
        StartSpeech();
    }
    private void Update()
    {
    }
    private void OnApplicationQuit()
    {
        StopSpeech();
    }
}