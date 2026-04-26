using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GraduationProject.Models
{
    [System.Serializable]
    public class GameAssetConfig
    {
        [JsonProperty("config_id")]
        public string ConfigId { get; set; }

        [JsonProperty("base_url")]
        public string BaseUrl { get; set; }

        [JsonProperty("audio_base_url")]
        public string? AudioBaseUrl { get; set; }

        [JsonProperty("items")]
        public List<AssetItem> Items { get; set; }

        [JsonProperty("asset_json")]
        public string AssetJson { get; set; }
    }

    [System.Serializable]
    public class AssetItem
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("file")]
        public string File { get; set; }

        [JsonProperty("audio")]
        public string? Audio { get; set; }

        [JsonExtensionData]
        [JsonIgnore]
        public IDictionary<string, JToken>? ExtraFields { get; set; }
    }
}