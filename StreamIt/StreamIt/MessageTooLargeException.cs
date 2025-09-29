namespace StreamIt;

public class MessageTooLargeException(int expected, int actual)
    : Exception($"message too large, expected maximum size: {expected}, got {actual}")
{
}