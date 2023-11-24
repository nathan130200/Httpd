using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace Httpd.Impl;

public class HttpContext : IDisposable
{
    static readonly Uri BASE_URL = new("http://localhost:2323");

    private Socket _socket;
    private Stream _inStream;
    private Stream _outStream;

    private HttpRequest _request;
    private HttpResponse _response;

    public HttpRequest Request => _request;
    public HttpResponse Response => _response;

    public HttpContext(Socket socket)
    {
        _socket = socket;
        _outStream = new NetworkStream(_socket, FileAccess.Write, false);
        _inStream = new NetworkStream(_socket, FileAccess.Read, false);
        _response = new HttpResponse();
    }

    public Stream InputStream => _inStream;
    public Stream OutputStream => _outStream;

    public void Dispose()
    {
        _request?.Dispose();
        _request = default;

        _response?.Dispose();
        _response = default;

        _inStream?.Dispose();
        _inStream = default;

        _outStream?.Dispose();
        _outStream = default;

        _socket?.Dispose();
        _socket = default;
    }

    static readonly string[] s_HttpVersions = new[]
    {
        "http/1.0",
        "http/1.1"
    };

    static bool IsMethodSupported(string str)
        => Enum.TryParse<HttpMethod>(str, true, out _);

    static bool IsVersionSupported(string str)
        => s_HttpVersions.Any(x => string.Equals(str, x, StringComparison.OrdinalIgnoreCase));

    public async Task ParseAsync()
    {
        var header = await _inStream.ReadHttpLineAsync();

        if (string.IsNullOrEmpty(header))
            throw new HttpRequestException("HTTP header missing", default, HttpStatusCode.BadRequest);

        var hdr = header.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (hdr.Length != 3)
            throw new HttpRequestException("HTTP header is not well formed.", default, HttpStatusCode.BadRequest);

        var rawMethod = hdr[0];

        if (!IsMethodSupported(rawMethod))
            throw new HttpRequestException("HTTP method is not supported.", default, HttpStatusCode.MethodNotAllowed);

        var rawUrl = hdr[1];

        var version = hdr[2];

        if (!IsVersionSupported(version))
            throw new HttpRequestException("HTTP version is not supported", default, HttpStatusCode.HttpVersionNotSupported);

        var headers = new Dictionary<string, string>();
        await ParseHeadersAsync(headers, _inStream);

        if (!headers.TryGetValue("host", out var host))
            host = "http://localhost:2323";

        var baseUrl = new Uri(host);

        var qs = new Dictionary<string, string>();

        if (!Uri.TryCreate(baseUrl, rawUrl, out var url))
            throw new HttpRequestException("HTTP url is not well formed.", default, HttpStatusCode.BadRequest);

        var localPath = url.LocalPath;
        string rawQs = string.Empty;

        if (!string.IsNullOrWhiteSpace(url.Query))
            rawQs = url.Query[1..];

        if (rawQs.Contains('&'))
        {
            var items = rawQs.Split('&')
                .Select(x =>
                {
                    int ofs;
                    string key, value = string.Empty;

                    if ((ofs = x.IndexOf('=')) == -1)
                        key = x;
                    else
                    {
                        key = x[0..ofs];
                        value = x[(ofs + 1)..];
                    }

                    return new
                    {
                        key,
                        value
                    };
                });

            foreach (var it in items)
                qs[it.key] = it.value;
        }

        _request = new HttpRequest(_inStream)
        {
            Method = Enum.Parse<HttpMethod>(rawMethod, true),
            Version = version,
            RawUrl = rawUrl,
            LocalPath = localPath,
            Headers = headers,
            RawQueryString = rawQs,
            QueryString = qs
        };

        _response.Request = _request;
    }

    static async Task ParseHeadersAsync(Dictionary<string, string> result, Stream s)
    {
        Unsafe.SkipInit(out string str);

        while (true)
        {
            str = await s.ReadHttpLineAsync();

            if (string.IsNullOrEmpty(str))
                break;

            int ofs;

            if ((ofs = str.IndexOf(':')) == -1)
                throw new InvalidOperationException("HTTP request is not well formed.");

            var headerName = str[0..ofs];
            var headerValue = str[(ofs + 1)..];

            result[headerName.ToLowerInvariant()] = headerValue;
        }
    }

    public void Deconstruct(out HttpRequest req, out HttpResponse res)
    {
        req = _request;
        res = _response;
    }
}
