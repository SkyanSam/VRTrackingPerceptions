﻿using CharacterAI.Models;
using CharacterAI.Services;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;

namespace CharacterAI
{
    public class CharacterAIClient : CommonService
    {
        public string UserToken { get; }
        public bool CaiPlusEnabled { get; }
        private readonly PuppeteerService _puppeteerService;

        /// <summary>
        /// Create a new integration with CharacterAI service
        /// </summary>
        /// <param name="userToken">Your c.ai user token to use by default</param>
        /// <param name="caiPlusMode">Enable if you have c.ai+ and want the integration to use "plus.character.ai" subdomain instead of "beta.character.ai"; disabled by default.</param>
        /// <param name="customBrowserDirectory">Directory where the browser will be downloaded to. Not required.</param>
        /// <param name="customBrowserExecutablePath">Full path to the chrome executabe binary file. Not required.</param>
        public CharacterAIClient(string? userToken = null, bool caiPlusMode = false, string? customBrowserDirectory = null, string? customBrowserExecutablePath = null)
        {
            UserToken = userToken ?? "";
            CaiPlusEnabled = caiPlusMode;
            _puppeteerService = new(customBrowserDirectory, customBrowserExecutablePath);
        }

        public void LaunchBrowser(bool killDuplicates = true)
        {
            lock (_puppeteerService)
            {
                _puppeteerService.LaunchBrowserAsync(killDuplicates).Wait();
            }
        }

        public void KillBrowser()
        {
            lock (_puppeteerService)
            {
                _puppeteerService.KillBrowser();
            }
        }

        /// <summary>
        /// Send message and get response
        /// </summary>
        /// <returns>new CharacterResponse()</returns>
        public async Task<CharacterResponse> CallCharacterAsync(string characterId, string characterTgt, string historyId, string message = "", string? imagePath = null, string? primaryMsgUuId = null, string? parentMsgUuId = null, string? customAuthToken = null, bool? customPlusMode = null)
        {
            var contentDynamic = BasicCallContent(characterId, characterTgt, message, imagePath, historyId);

            // Fetch new answer ("perform swipe").
            if (parentMsgUuId is not null)
            {   // When parent_msg_id is present, character will generate new response for a last message.
                contentDynamic.parent_msg_uuid = parentMsgUuId;
            }
            // Or set new (swiped) answer as one to reply on.
            else if (primaryMsgUuId is not null)
            {   // Provide primary_msg_id to point out which character's response you've chosen.
                contentDynamic.primary_msg_uuid = primaryMsgUuId;
                // (seen_msg_ids[] is also required, either it just won't work, but I didn't bother to collect
                //  every single swiped message, just fill it with chosen one)
                contentDynamic.seen_msg_uuids = new string[] { primaryMsgUuId };
            }

            string url = $"https://{((customPlusMode.HasValue ? (customPlusMode == true) : CaiPlusEnabled) ? "plus" : "beta" )}.character.ai/chat/streaming/";
            FetchResponse fetchResponse = await _puppeteerService.FetchRequestAsync(url, "POST", customAuthToken ?? UserToken, contentDynamic);

            if (fetchResponse.IsBlocked)
            {   // Fallback on slower but more stable request method
                var puppeteerResponse = await _puppeteerService.RequestPostWithDownloadAsync(url, customAuthToken ?? UserToken, contentDynamic);
                return new CharacterResponse(puppeteerResponse);
            }

            return new CharacterResponse(fetchResponse);
        }

        /// <summary>
        /// Get info about character
        /// </summary>
        /// <returns>new Character()</returns>
        public async Task<Character> GetInfoAsync(string characterId, string? customAuthToken = null, bool? customPlusMode = null)
        {
            string url = $"https://{((customPlusMode.HasValue ? (customPlusMode == true) : CaiPlusEnabled) ? "plus" : "beta" )}.character.ai/chat/character/info/";
            var data = new Dictionary<string, string> { { "external_id", characterId } };
            var response = await _puppeteerService.RequestPostAsync(url, customAuthToken ?? UserToken, data);

            if (response.InQueue)
            {
                lock (_puppeteerService)
                {
                    _puppeteerService.TryToLeaveQueueAsync(log: false).Wait();
                }

                response = await _puppeteerService.RequestPostAsync(url, customAuthToken ?? UserToken, data);
            }

            dynamic? character = null;

            if (response.IsSuccessful)
                character = JsonConvert.DeserializeObject<dynamic>(response.Content!)?.character;
            else
                Failure(response: response.Content);

            return new Character(character);
        }

        public async Task<string?> GetLastChatAsync(string characterId, string? customAuthToken = null, bool? customPlusMode = null)
        {
            string url = $"https://{((customPlusMode.HasValue ? (customPlusMode == true) : CaiPlusEnabled) ? "plus" : "beta" )}.character.ai/chat/history/continue/";

            var data = new FormUrlEncodedContent(new Dictionary<string, string> {
                { "character_external_id", characterId }
            });

            var response = await _puppeteerService.RequestPostAsync(url, customAuthToken ?? UserToken, data);

            if (response.InQueue)
            {
                lock (_puppeteerService)
                {
                    _puppeteerService.TryToLeaveQueueAsync(log: false).Wait();
                }

                response = await _puppeteerService.RequestPostAsync(url, customAuthToken ?? UserToken, data);
            }

            if (response.IsSuccessful)
                return JsonConvert.DeserializeObject<dynamic>(response.Content!)?.external_id;

            await Task.Delay(5000);
            return await CreateNewChatAsync(characterId, customAuthToken, customPlusMode);
        }

        /// <summary>
        /// Create new chat with a character
        /// </summary>
        /// <returns>returns chat_history_id if successful; null if fails.</returns>
        public async Task<string?> CreateNewChatAsync(string characterId, string? customAuthToken = null, bool? customPlusMode = null)
        {
            string url = $"https://{((customPlusMode.HasValue ? (customPlusMode == true) : CaiPlusEnabled) ? "plus" : "beta" )}.character.ai/chat/history/create/";
            var data = new Dictionary<string, string> {
                { "character_external_id", characterId }
            };

            var response = await _puppeteerService.RequestPostAsync(url, customAuthToken ?? UserToken, data);

            if (response.InQueue)
            {
                lock (_puppeteerService)
                {
                    _puppeteerService.TryToLeaveQueueAsync(log: false).Wait();
                }

                response = await _puppeteerService.RequestPostAsync(url, customAuthToken ?? UserToken, data);
            }

            // Their servers are shit and sometimes it requires a second request
            if (!response.IsSuccessful)
            {
                await Task.Delay(3000);
                response = await _puppeteerService.RequestPostAsync(url, customAuthToken ?? UserToken, data);
            }

            if (!response.IsSuccessful)
            {
                Failure(response: response.Content);
                return null;
            }

            var externalId = JsonConvert.DeserializeObject<dynamic>(response.Content!)?.external_id;
            if (externalId is null)
                Failure("Something went wrong...", response: response.Content);

            return externalId!;
        }

        // not working
        //public async Task<HistoriesResponse> GetHistoriesAsync(string? characterId = null)
        //{
        //    string url = $"https://{(CAIplus ? "plus" : "beta" )}.character.ai/chat/character/histories/";

        //    var data = new Dictionary<string, string> {
        //        { "external_id", characterId ?? CurrentCharacter.Id! },
        //        { "number", "50" } // Idk what it is. Probably an amount of chats to show. Default value is 50, so I'll just leave it like this.
        //    };

        //    var response = await Request(HttpMethod.Get, url, data);

        //    return new HistoriesResponse(response);
        //}

        // Search for a character
        public async Task<SearchResponse> SearchAsync(string query, string? customAuthToken = null, bool? customPlusMode = null)
        {
            string url = $"https://{((customPlusMode.HasValue ? (customPlusMode == true) : CaiPlusEnabled) ? "plus" : "beta" )}.character.ai/chat/characters/search/?query={query}";
            var response = await _puppeteerService.RequestGetAsync(url, customAuthToken ?? UserToken);
            if (response.InQueue)
            {
                lock (_puppeteerService)
                {
                    _puppeteerService.TryToLeaveQueueAsync(log: false).Wait();
                }

                response = await _puppeteerService.RequestGetAsync(url, customAuthToken ?? UserToken);
            }

            return new SearchResponse(response, query);
        }

        /// <summary>
        /// CURRENTLY NOT WORKING
        /// Upload image on a server. Use it to attach image to your reply.
        /// </summary>
        /// <returns>
        /// image path if successful; null if fails
        /// </returns>
        public async Task<string?> UploadImageAsync(byte[] img, string fileName = "image.jpeg", string? customAuthToken = null, bool? customPlusMode = null)
        {
            return null;

            string url = $"https://{((customPlusMode.HasValue ? (customPlusMode == true) : CaiPlusEnabled) ? "plus" : "beta" )}.character.ai/chat/upload-image/";

            if (!fileName.Contains('.')) fileName += ".jpeg";
            string ext = fileName.Split(".").Last();

            var content = new ByteArrayContent(img);
            content.Headers.ContentType = new MediaTypeHeaderValue($"image/{ext}");

            string boundary = "----RandomBoundaryString" + new System.Random().Next(1024).ToString();
            var formData = new MultipartFormDataContent(boundary) { { content, "image", fileName } };

            string data = await formData.ReadAsStringAsync();
            string contentType = $"multipart/form-data; boundary={boundary}";
            WriteToLogFile(data);

            var response = await _puppeteerService.RequestPostAsync(url, customAuthToken ?? UserToken, data, contentType);

            if (response.InQueue)
            {
                lock (_puppeteerService)
                {
                    _puppeteerService.TryToLeaveQueueAsync(log: false).Wait();
                }

                response = await _puppeteerService.RequestPostAsync(url, customAuthToken ?? UserToken, data, contentType);
            }

            if (response.IsSuccessful)
                return JsonConvert.DeserializeObject<dynamic>(response.Content!)?.value;

            Failure(response: response.Content);
            return null;
        }
    }
}