using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CharacterAI;
using System;
using System.Threading.Tasks;
using System.Runtime.Remoting.Messaging;

public class CharacterAIBehaviour : MonoBehaviour
{
    public string authToken = "_YOUR_AUTH_TOKEN_";
    public string characterId = "_CHARACTER_ID_";
    CharacterAIClient client;
    CharacterAI.Models.Character character;
    public static List<CharacterAIBehaviour> instances = new List<CharacterAIBehaviour>();
    string historyId;
    public void Start()
    {
        instances.Add(this);
        Task.Run(() => ConnectToCharacterAI(authToken).Start());
    }
    public static void CallOnSpeechToTextRecognized(string text)
    {
        foreach (var instance in instances) instance.OnSpeechToTextRecognized(text);
    }
    public void OnSpeechToTextRecognized(string text)
    {
        // (!) Note to add condition of if the player is within close enough distance to the character.
        Task.Run(() => SendAndRecieveMessage(text).Start());
    }
    async Task ConnectToCharacterAI(string token)
    {
        client = new CharacterAIClient(token);
        print(client);
        // Launch Puppeteer headless browser
        client.LaunchBrowser(killDuplicates: true);

        // Highly recommend to do this
        AppDomain.CurrentDomain.ProcessExit += (s, args) => client.KillBrowser();

        // Send message to a character
        character = await client.GetInfoAsync(characterId);
        historyId = await client.CreateNewChatAsync(characterId);

        if (historyId is null)
        {
            return;
            
        }
    }
    async Task SendAndRecieveMessage(string _message)
    {
        print("sending message " + _message);
        var characterResponse = await client.CallCharacterAsync(
            characterId: character.Id,
            characterTgt: character.Tgt,
            historyId: historyId,
            message: _message
        );

        if (!characterResponse.IsSuccessful)
        {
            Debug.Log(characterResponse.ErrorReason);
            return;
        }

        string message = characterResponse.Response.Text; // => "Hey!"
        string userMessageUuid = characterResponse.LastUserMsgUuId; // needed to perform "swipe" based on your last message

        // Swipe
        var newCharacterResponse = await client.CallCharacterAsync(
            characterId: character.Id,
            characterTgt: character.Tgt,
            historyId: historyId,
            parentMsgUuId: userMessageUuid // (!)
        );

        message = newCharacterResponse.Response.Text; // => "Holla!" second variation of character response
        userMessageUuid = newCharacterResponse.LastUserMsgUuId; // or is it response.lastusermsgUuid (!)
        string characterMessageUuid = newCharacterResponse.Response.UuId; // needed to specify the message you're reffering to after "swipe"; not needed if no swipe was performed

        // Respond on a message
        var actuallyNewCharacterResponse = await client.CallCharacterAsync(
            characterId: character.Id,
            characterTgt: character.Tgt,
            historyId: historyId,
            primaryMsgUuId: characterMessageUuid // (!)
        );

        Debug.Log("CHARACTERAI MESSAGE : " + message);
        TextToSpeech.Instance.Speak(message);
    }
}
