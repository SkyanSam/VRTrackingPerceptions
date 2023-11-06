using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System.Threading.Tasks;

public class TextToSpeech : MonoBehaviour
{
    public string speechKey;
    public string speechRegion;
    public string speechLanguage = "en-US";
    public string voiceName = "en-US-JennyNeural";
    public static TextToSpeech Instance;
    SpeechConfig speechConfig;
    SpeechSynthesizer speechSynthesizer;
    public AudioClip audioClip;
    public AudioSource audioSource;
    void Start()
    {
        Instance = this;
        StartAudio();
    }
    void Update()
    {
        
    }
    public void Speak(string message)
    {
        Task.Run(() => SynthesizeAudio(message));
    }
    public void StartAudio()
    {
        speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
        // Set either the `SpeechSynthesisVoiceName` or `SpeechSynthesisLanguage`.
        speechConfig.SpeechSynthesisLanguage = speechLanguage;
        speechConfig.SpeechSynthesisVoiceName = voiceName;
        speechSynthesizer = new SpeechSynthesizer(speechConfig, null);
    }
    async Task SynthesizeAudio(string message)
    {
        using var audioConfig = AudioConfig.FromWavFileOutput(Application.streamingAssetsPath + "voice.wav");
        using var speechSynthesizer = new SpeechSynthesizer(speechConfig, audioConfig);
        await speechSynthesizer.SpeakTextAsync(message);
        // TODO: WORK ON WEB REQUESTS
    }
}
