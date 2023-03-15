using Newtonsoft.Json;

namespace KonataAdapter.Connection;

[Serializable]
public class SocketMessage
{
    [JsonProperty("type")] public string Type { get; set; } = "connection";
    [JsonProperty("conn")] public string Conn { get; set; } = "socket";
    [JsonProperty("protocol")] public string Protocol { get; set; } = "cs";
    [JsonProperty("botadapter")] public string BotAdapter { get; set; } = "mirai";
}

[Serializable]
public class MessageHeader
{
    [JsonProperty("sequence")] public uint Sequence { get; set; }
    [JsonProperty("group_id")] public uint GroupId { get; set; }
    [JsonProperty("group_name")] public string GroupName { get; set; } = "";
    [JsonProperty("sender_id")] public uint SenderId { get; set; }
    [JsonProperty("sender_name")] public string SenderName { get; set; } = "";
    [JsonProperty("level")] public uint Level { get; set; }
    [JsonProperty("uuid")]public long Uuid { get; set; }
    [JsonProperty("time")]public uint Time { get; set; }
}