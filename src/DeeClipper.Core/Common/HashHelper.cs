using System.Security.Cryptography;
using System.Text;

namespace DeeClipper.Core.Common;

public static class HashHelper
{
    public static string ComputeSha256(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content ?? string.Empty);
        return ComputeSha256(bytes);
    }

    public static string ComputeSha256(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
