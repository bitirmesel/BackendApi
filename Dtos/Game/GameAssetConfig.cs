using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GraduationProject.Models
{
    [Serializable]
    public class GameAssetConfig
    {
        [JsonProperty("config_id")]
        public string ConfigId { get; set; }

        [JsonProperty("base_url")]
        public string BaseUrl { get; set; }

        // DÜZELTME: Backend 'items' yolluyor, biz de 'Items' diyoruz.
        [JsonProperty("items")]
        public List<AssetItem> Items { get; set; } 
    }

    [Serializable]
    public class AssetItem
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("file")]
        public string File { get; set; }
    }
}