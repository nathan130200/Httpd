using System.Text;

namespace Httpd;

internal static class IOHelper
{
    public static Task<string> ReadHttpLineAsync(this Stream s)
    {
        var buf = new StringBuilder();
        char last = default;
        char curr;
        int val;

        while (true)
        {
            val = s.ReadByte();

            if (val == -1) break; // EOF
            else
            {
                curr = (char)val;

                if (!char.IsAscii(curr))
                    throw new InvalidDataException($"Char '0x{val:X2}' is not US-ASCII well formed");

                if (curr == '\n')
                {
                    if (last != '\r')
                        throw new InvalidOperationException("Unexcepted UNIX line ending");

                    break;
                }
                else
                {
                    last = curr;

                    if (curr != '\r')
                        buf.Append(curr);
                }
            }
        }

        return Task.FromResult(buf.ToString());
    }

    public static async ValueTask WriteHttpLineAsync(this Stream s, string str = "")
    {
        var buf = Encoding.ASCII.GetBytes(str + "\r\n");
        await s.WriteAsync(buf);
    }
}
