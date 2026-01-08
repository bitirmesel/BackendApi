using System;
using System.Collections.Generic;
using Newtonsoft.Json;

// Namespace'i servisinizin 'using' listesindeki ile eşitledik
namespace DktApi.Dtos.Game 
{
    [Serializable]
    public class GameAssetConfigDto // İSMİ DEĞİŞTİ (Sonuna Dto geldi)
    {
        [JsonProperty("config_id")]
        public string ConfigId { get; set; }

        [JsonProperty("base_url")]
        public string BaseUrl { get; set; }

        [JsonProperty("items")]
        public List<AssetItemDto> Items { get; set; } 
    }

    [Serializable]
    public class AssetItemDto // Alt sınıfı da isimlendirme standardına uydurduk
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("file")]
        public string File { get; set; }
    }
}