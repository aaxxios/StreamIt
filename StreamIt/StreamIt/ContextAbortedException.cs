namespace StreamIt;

public class ContextAbortedException: Exception
{
    public ContextAbortedException() : base("the context was aborted"){}
}