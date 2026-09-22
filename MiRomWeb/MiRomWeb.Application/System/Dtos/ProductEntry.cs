using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace XiaomiLib;

public class ProductEntry
{
    [JsonProperty("displayName")]
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; }
    [JsonProperty("displayEnglishName")]
    [JsonPropertyName("displayEnglishName")]
    public string DisplayEnglishName { get; set; }
    [JsonProperty("device")]
    [JsonPropertyName("device")]
    public string Device { get; set; }
    [JsonProperty("releaseDate")]
    [JsonPropertyName("releaseDate")]
    public string ReleaseDate { get; set; }
    [JsonProperty("product")]
    [JsonPropertyName("product")]
    public string Product { get; set; }
}
