using Newtonsoft.Json;

namespace VirtualEnvManager.Models
{
    public class PackageInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }
    }
} 