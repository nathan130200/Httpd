﻿using System.Net;
using System.Net.Http.Headers;

namespace Httpd.Impl;

public class HttpResponse : IDisposable
{
    public HttpRequest Request { get; internal set; }

    public void Dispose()
    {
        Request = default;

        Content?.Dispose();
        Content = default;
    }

    private string _httpVersion = "HTTP/1.1";

    public string Version
        => Request?.Version ?? _httpVersion;

    private HttpContent _content;

    public HttpStatusCode Code { get; set; } = HttpStatusCode.OK;

    public HttpContent Content
    {
        get => _content;
        set
        {
            if ((_content = value) is not null)
                MergeHeaders(Headers, _content.Headers);
        }
    }

    static void MergeHeaders(Dictionary<string, string> source, HttpContentHeaders headers)
    {
        foreach (var (key, values) in headers)
            source[key.ToLowerInvariant()] = string.Join(';', values);
    }

    public Dictionary<string, string> Headers { get; } = new()
    {
        ["server"] = "Httpd/1.0",
        ["date"] = DateTime.Now.ToString("F"),
        ["connection"] = "close"
    };

    public void SetHeader(string name, object value)
        => Headers[name.ToLowerInvariant()] = value.ToString();

    public HttpResponse WithHeader(string name, string value)
    {
        SetHeader(name, value);
        return this;
    }

    public HttpResponse WithContent(HttpContent content)
    {
        _content = content;
        return this;
    }

    public HttpResponse WithStringContent(string value)
    {
        _content = new StringContent(value);
        return this;
    }

    public HttpResponse WithByteArrayContent(byte[] buffer, int offset = 0, int? count = default)
    {
        _content = new ByteArrayContent(buffer, offset, count ?? buffer.Length);
        return this;
    }

    public HttpResponse WithCode(HttpStatusCode code)
    {
        Code = code;
        return this;
    }

    public async Task CopyToAsync(Stream s)
    {
        await s.WriteHttpLineAsync($"{Version} {(int)Code}");

        foreach (var header in Headers.DistinctBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            await s.WriteHttpLineAsync($"{header.Key}: {header.Value}");

        await s.WriteHttpLineAsync();

        if (Content != null)
            await Content.CopyToAsync(s);
    }
}
