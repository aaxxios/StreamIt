using System.Text.Json;

namespace StreamIt;

public class StreamItOptions
{
    /// <summary>
    /// timeout before closing the connection if no message is received
    /// </summary>
    public TimeSpan ReadMessageTimeout { get; set; } = TimeSpan.FromSeconds(10);

    
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromMilliseconds(100);
    
    /// <summary>
    /// maximum message size in bytes
    /// </summary>
    public int MaxMessageSize { get; set; } = 1024;

    public JsonSerializerOptions? SerializerOptions { get; set; }
    
    /// <summary>
    /// if statistics should be collected for byte read/written in contexts
    /// </summary>
    public bool EnableStatistics { get; set; }
    
}