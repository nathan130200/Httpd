namespace Httpd.Impl;

public class HttpRequest : IDisposable
{
    public required HttpMethod Method { get; init; }
    public required string Version { get; init; }
    public required string RawUrl { get; init; }
    public required string LocalPath { get; init; }
    public required string RawQueryString { get; init; }
    public required IReadOnlyDictionary<string, string> Headers { get; init; }
    public required IReadOnlyDictionary<string, string> QueryString { get; init; }
    public Stream InputStream { get; private set; }

    public HttpRequest(Stream stream)
        => InputStream = stream;

    public void Dispose()
        => InputStream = default;

    public string ContentType
        => Headers["content-type"];

    public long ContentLength
    {
        get
        {
            if (Headers.TryGetValue("content-length", out var s))
                return long.Parse(s);

            return 0L;
        }
    }
}
