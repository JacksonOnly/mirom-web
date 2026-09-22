using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace XiaomiLib;

public class OtaBaseDto<T> where T : class
{
    [JsonProperty("code")]
    [JsonPropertyName("code")]
    public int Code { get; set; }
    [JsonProperty("desc")]
    [JsonPropertyName("desc")]
    public string Message { get; set; }
    [JsonProperty("data")]
    [JsonPropertyName("data")]
    public T? Data { get; set; }
}
