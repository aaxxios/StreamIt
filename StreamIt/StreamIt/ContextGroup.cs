using System.Collections;
using System.Runtime.CompilerServices;

namespace StreamIt;

public class ContextGroup<TItem>: IEnumerable<TItem>
{
    private readonly HashSet<TItem> _list = new();
    public int Count => _list.Count;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(TItem item)
    {
        _list.Add(item);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Remove(TItem item)
    {
        _list.Remove(item);
    }

    public IEnumerator<TItem> GetEnumerator()
    {
        return _list.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}