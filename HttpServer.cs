using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Httpd.Impl;

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
        }
    }

    async Task HandleRequestAsync(HttpContext ctx)
    {
        var request = ctx.Request;

        var evt = new RequestEventArgs(ctx);

        await _onRequest.InvokeAsync(evt);

        if (!evt.Handled)
        {
            ctx.Response.WithCode(HttpStatusCode.NotFound)
                .WithStringContent($@"

<div>
    <i>404 - Not found</i> — <b>{request.LocalPath}</b>
    <br/>
    <hr/>
    <p>
        {DateTime.Now:R} — {Guid.NewGuid():N}
    </p>
</div>

");
        }
    }

    private AsyncEvent<RequestEventArgs> _onRequest = new();

    public event AsyncEventHandler<RequestEventArgs> OnRequest
    {
        add => _onRequest.AddHandler(value);
        remove => _onRequest.RemoveHandler(value);
    }
}

public class RequestEventArgs : AsyncEventArgs
{
    public HttpContext Context { get; }

    public RequestEventArgs(HttpContext ctx)
        => Context = ctx;
}
