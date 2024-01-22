using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Extreal.Integration.Messaging
{
    [SuppressMessage("Usage", "CC0047")]
    public class GroupListResponse
    {
        [JsonPropertyName("groups")]
        public List<GroupResponse> Groups { get; set; }
    }

    [SuppressMessage("Usage", "CC0047")]
    public class GroupResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
}
