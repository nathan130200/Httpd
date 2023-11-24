using System.Diagnostics;

namespace Httpd;

public delegate Task AsyncEventHandler<in T>(T e) where T : AsyncEventArgs;

public class AsyncEventArgs
{
    public bool Handled { get; set; }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public static AsyncEventArgs Empty => new();
}

public class AsyncEvent<T> where T : AsyncEventArgs
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly List<AsyncEventHandler<T>> _handlerList = new();

    public AsyncEvent()
    {

    }

    public void AddHandler(AsyncEventHandler<T> func)
    {
        lock (_handlerList)
            _handlerList.Add(func);
    }

    public void RemoveHandler(AsyncEventHandler<T> func)
    {
        lock (_handlerList)
            _handlerList.Remove(func);
    }

    public async Task InvokeAsync(T e)
    {
        AsyncEventHandler<T>[] handlers;

        lock (_handlerList)
            handlers = _handlerList.ToArray();

        var exceptions = new List<Exception>();

        foreach (var handler in handlers)
        {
            try
            {
                await handler(e);

                if (e.Handled)
                    break;
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        if (exceptions.Count > 0)
            throw new AggregateException(exceptions);
    }
}