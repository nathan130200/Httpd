using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Httpd.Impl;

public delegate Task AsyncEventHandler<in T>(T e) where T : EventArgs;

public class HttpServer
{
    private IPEndPoint _endpoint;
    private Socket _socket;

    public HttpServer(int port = 2323)
    {
        _endpoint = new IPEndPoint(IPAddress.IPv6Any, port);
    }

    public ValueTask Start()
    {
        _socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
        _socket.Bind(_endpoint);
        _socket.Listen(10);

        _ = Task.Run(async () =>
        {
            while (_socket != null)
            {
                try
                {
                    var client = await _socket.AcceptAsync();
                    _ = Task.Run(async () => await EndAccept(client));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }

                await Task.Delay(16);
            }
        });

        return ValueTask.CompletedTask;
    }

    public ValueTask Stop()
    {
        _socket?.Dispose();
        _socket = default;

        return ValueTask.CompletedTask;
    }

    async Task EndAccept(Socket client)
    {
        using (var ctx = new HttpContext(client))
        {
            var res = ctx.Response;

            try
            {
                await ctx.ParseAsync();

                await HandleRequestAsync(ctx);
            }
            catch (Exception ex)
            {
                var code = ex is HttpRequestException r
                    ? (HttpStatusCode)r.StatusCode
                    : HttpStatusCode.InternalServerError;

                res.WithCode(code)
                    .WithStringContent(ex.ToString());
            }

            await res.CopyToAsync(ctx.OutputStream);

            await Task.Delay(1000);
        }
    }

    async Task HandleRequestAsync(HttpContext ctx)
    {
        var func = (AsyncEventHandler<ContextEventArgs>)_handlers[_onRequestEvent];

        if (func != null)
        {
            var evt = new ContextEventArgs(ctx);
            await func(evt);

            if (!evt.Handled)
                ctx.Response.WithCode(HttpStatusCode.NotFound);
        }
        else
        {
            ctx.Response.WithCode(HttpStatusCode.NotImplemented)
                .WithStringContent($"<div><i>501: Feature Not Implemented</b></i><hr/>{DateTime.Now:F}</div>");
        }
    }

    readonly object _onRequestEvent = nameof(OnRequest);
    readonly EventHandlerList _handlers = new();

    public event AsyncEventHandler<ContextEventArgs> OnRequest
    {
        add => _handlers.AddHandler(_onRequestEvent, value);
        remove => _handlers.RemoveHandler(_onRequestEvent, value);
    }
}

public class ContextEventArgs : EventArgs
{
    public HttpContext Context { get; }
    public bool Handled { get; set; }

    public ContextEventArgs(HttpContext ctx)
        => Context = ctx;
}