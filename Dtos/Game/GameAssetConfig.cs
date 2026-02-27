using System.Collections.Generic;
using Newtonsoft.Json;

namespace GraduationProject.Models
{
    [System.Serializable]
    public class GameAssetConfig
    {
        [JsonProperty("configId")]
        public string ConfigId { get; set; }

        [JsonProperty("baseUrl")]
        public string BaseUrl { get; set; }

        [JsonProperty("audioBaseUrl")]
        public string? AudioBaseUrl { get; set; }

        [JsonProperty("items")]
        public List<AssetItem> Items { get; set; }

        // --- EKSİK OLAN KISIM BURASIYDI ---
        // Bu satırı eklemezsen APIManager hata verir!
        [JsonProperty("assetJson")] 
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
    }
}