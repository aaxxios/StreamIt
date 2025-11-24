namespace StreamIt;

public class MessageTooLargeException : Exception
{
    public MessageTooLargeException(int expected, int actual) : base(
        $"message too large, expected maximum size: {expected}, got {actual}")
    {
    }
}