namespace StreamIt;

public class StreamItOptions
{
    public TimeSpan ReadMessageTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public int MaxMessageSize { get; set; } = 1024;
    /// <summary>
    /// the name of the topic to use for ping messages
    /// </summary>
    public string PingTopic { get; set; } = "ping";

    /// <summary>
    /// duration after which the client is considered unauthorized
    /// </summary>
    public TimeSpan? AuthorizedClientLifetime { get; set; }
}