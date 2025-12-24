using System.Collections.Generic;
using Newtonsoft.Json; // Eğer yoksa: dotnet add package Newtonsoft.Json

namespace DktApi.DTOs.Game
{
    public class GameAssetConfigDto
    {
        [JsonProperty("config_id")]
        public string ConfigId { get; set; }

        [JsonProperty("base_url")]
        public string BaseUrl { get; set; }

        [JsonProperty("items")]
        public List<AssetItemDto> Items { get; set; }
    }

    public class AssetItemDto
    {
        [JsonProperty("key")]
        public string Key { get; set; }   // Örn: "kedi"

        [JsonProperty("file")]
        public string File { get; set; }  // Örn: "kedi.png"
    }

}