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
    static TaskCompletionSource<int> stopRecognition;
    async static Task StartSpeech()
    {
        print("begin startspeech()");
        speechConfig = SpeechConfig.FromSubscription(Instance.speechKey, Instance.speechRegion);
        print(speechConfig);
        print("begin audioconfig");
        audioConfig = AudioConfig.FromDefaultMicrophoneInput();
        print("init vars");
        speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);
        stopRecognition = new TaskCompletionSource<int>();
        print("Running start speech");
        speechRecognizer.Recognizing += (s, e) =>
        {
            Console.WriteLine($"RECOGNIZING: Text={e.Result.Text}");
        };

        speechRecognizer.Recognized += (s, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech)
            {
                Console.WriteLine($"RECOGNIZED: Text={e.Result.Text}");
                Instance.BroadcastMessage("OnSpeechToTextRecognized", e.Result.Text);
            }
            else if (e.Result.Reason == ResultReason.NoMatch)
            {
                Console.WriteLine($"NOMATCH: Speech could not be recognized.");
            }
        };

        speechRecognizer.Canceled += (s, e) =>
        {
            Console.WriteLine($"CANCELED: Reason={e.Reason}");

            if (e.Reason == CancellationReason.Error)
            {
                Console.WriteLine($"CANCELED: ErrorCode={e.ErrorCode}");
                Console.WriteLine($"CANCELED: ErrorDetails={e.ErrorDetails}");
                Console.WriteLine($"CANCELED: Did you set the speech resource key and region values?");
            }

            stopRecognition.TrySetResult(0);
        };

        speechRecognizer.SessionStopped += (s, e) =>
        {
            Console.WriteLine("\n    Session stopped event.");
            stopRecognition.TrySetResult(0);
        };

        await speechRecognizer.StartContinuousRecognitionAsync();

        // Waits for completion. Use Task.WaitAny to keep the task rooted.
        Task.WaitAny(new[] { stopRecognition.Task });
    }
    async static void StopSpeech()
    {
        await speechRecognizer.StopContinuousRecognitionAsync();
    }
    private void Start()
    {
        Instance = this;
        StartSpeech().Start();
    }
    private void Update()
    {
    }
    private void OnApplicationQuit()
    {
        StopSpeech();
    }
}
