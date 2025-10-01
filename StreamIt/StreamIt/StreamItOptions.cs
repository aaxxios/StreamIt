using System.Text.Json;

namespace StreamIt;

public class StreamItOptions
{
    public TimeSpan ReadMessageTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(10);
    public int MaxMessageSize { get; set; } = 1024;
    
    public JsonSerializerOptions SerializerOptions { get; set; } = new(JsonSerializerDefaults.Web);
}