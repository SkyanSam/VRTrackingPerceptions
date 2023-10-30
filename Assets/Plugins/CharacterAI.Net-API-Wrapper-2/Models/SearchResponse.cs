﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CharacterAI.Services;

namespace CharacterAI.Models
{
    public class SearchResponse : CommonService
    {
        public List<Character> Characters { get; } = new();
        public string? ErrorReason { get; }
        public string OriginalQuery { get; }
        public bool IsSuccessful { get => ErrorReason is null; }
        public bool IsEmpty { get => !Characters.Any(); }

        public SearchResponse(PuppeteerResponse response, string query)
        {
            OriginalQuery = query;
            dynamic? responseParsed = ParseSearchResponse(response);

            if (responseParsed is null) return;
            if (responseParsed is string)
            {
                ErrorReason = responseParsed;

                return;
            }

            Characters = responseParsed;
        }

        private static dynamic? ParseSearchResponse(PuppeteerResponse response)
        {
            if (!response.IsSuccessful)
            {
                Failure(response: response.Content);
                return "Something went wrong";
            }

            JArray jCharacters = JsonConvert.DeserializeObject<dynamic>(response.Content!)!.characters;
            if (!jCharacters.HasValues) return null;

            var charactersList = new List<Character>();
            foreach (var character in jCharacters.ToObject<List<dynamic>>()!)
                charactersList.Add(new Character(character));

            return charactersList;
        }
    }
}
