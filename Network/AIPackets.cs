using ProtoBuf;

namespace chatAIVintageStoryMod.Network;

[ProtoContract]
public class AIQueryPacket
{
    [ProtoMember(1)] public string Question { get; set; } = "";
}

[ProtoContract]
public class AIResponsePacket
{
    [ProtoMember(1)] public string ProviderName { get; set; } = "";
    [ProtoMember(2)] public string Response { get; set; } = "";
    [ProtoMember(3)] public string? Error { get; set; }
}

[ProtoContract]
public class AIConfigSyncPacket
{
    [ProtoMember(1)] public string Provider { get; set; } = "";
    [ProtoMember(2)] public int RateLimitSeconds { get; set; }
    [ProtoMember(3)] public bool HasApiKey { get; set; }
    [ProtoMember(4)] public bool IsAdmin { get; set; }
}

[ProtoContract]
public class AIConfigChangePacket
{
    [ProtoMember(1)] public string Provider { get; set; } = "";
    [ProtoMember(2)] public int? RateLimitSeconds { get; set; }   // null = don't change
    [ProtoMember(3)] public string ApiKey { get; set; } = "";     // empty = don't change
}
