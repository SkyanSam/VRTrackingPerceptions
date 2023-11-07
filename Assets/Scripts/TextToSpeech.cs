using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System.Threading.Tasks;
using UnityEngine.Networking;
using System;

public class TextToSpeech : MonoBehaviour
{
    public string speechKey;
    public string speechRegion;
    public string speechLanguage = "en-US";
    public string voiceName = "en-US-JennyNeural";
    public static TextToSpeech Instance;
    SpeechConfig speechConfig;
    SpeechSynthesizer speechSynthesizer;
    public AudioSource audioSource;
    void Start()
    {
        Instance = this;
        StartAudio();
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
        string date = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.ffffffK");
        string path = Application.streamingAssetsPath + $"voice{date}.wav";
        using var audioConfig = AudioConfig.FromWavFileOutput(path);
        using var speechSynthesizer = new SpeechSynthesizer(speechConfig, audioConfig);
        await speechSynthesizer.SpeakTextAsync(message);
        Instance.StartCoroutine(ReadAudio(path));
    }
    IEnumerator ReadAudio(string path)
    {
        print("Start Read Audio Coroutine");
        var www = UnityWebRequestMultimedia.GetAudioClip("file://" + path, AudioType.WAV);
        yield return www.SendWebRequest();
        var audioClip = DownloadHandlerAudioClip.GetContent(www);
        print("Audio Clip Recieved " + audioClip);
        audioSource.clip = audioClip;
        audioSource.Play();
    }
}
