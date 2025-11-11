namespace StreamIt;

public class SocketCloseException: Exception
{
    public SocketCloseException() : base("connection han been closed"){}
}