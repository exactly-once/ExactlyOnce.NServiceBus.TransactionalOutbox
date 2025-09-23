using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace ExactlyOnce.NServiceBus.AtomicConsumeDispatchOutbox.Sql;

public static class ServiceBusSas
{
    /// <summary>
    /// Generate a Shared Access Signature (SAS) token for a Service Bus resource.
    /// </summary>
    /// <param name="resourceUri">
    /// The full URI of the resource you will call (e.g. "https://{ns}.servicebus.windows.net/queue/messages/{lock-token}").
    /// </param>
    /// <param name="keyName">SharedAccessKeyName from your policy (e.g., "RootManageSharedAccessKey").</param>
    /// <param name="keyValue">SharedAccessKey (base64 string) from the connection string.</param>
    /// <param name="ttl">Time-to-live for the token.</param>
    /// <returns>The value to set in the Authorization header.</returns>
    public static string GenerateToken(string resourceUri, string keyName, string keyValue, TimeSpan ttl)
    {
        // Per MS guidance, canonicalize to lowercase before signing, then URL-encode.
        string encodedResourceUri = HttpUtility.UrlEncode(resourceUri);

        long expiry = DateTimeOffset.UtcNow.Add(ttl).ToUnixTimeSeconds();
        string stringToSign = $"{encodedResourceUri}\n{expiry}";

        // Key is base64-encoded; decode then HMAC-SHA256 the stringToSign
        byte[] keyBytes = Convert.FromBase64String(keyValue);
        using var hmac = new HMACSHA256(keyBytes);
        byte[] signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
        string signature = HttpUtility.UrlEncode(Convert.ToBase64String(signatureBytes));

        return $"SharedAccessSignature sr={encodedResourceUri}&sig={signature}&se={expiry}&skn={keyName}";
    }

    private static string CreateToken(string resourceUri, string keyName, string key)
    {
        TimeSpan sinceEpoch = DateTime.UtcNow - new DateTime(1970, 1, 1);
        var week = 60 * 60 * 24 * 7;
        var expiry = Convert.ToString((int)sinceEpoch.TotalSeconds + week);
        string stringToSign = HttpUtility.UrlEncode(resourceUri) + "\n" + expiry;
        HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
        var sasToken = String.Format(CultureInfo.InvariantCulture, "SharedAccessSignature sr={0}&sig={1}&se={2}&skn={3}", HttpUtility.UrlEncode(resourceUri), HttpUtility.UrlEncode(signature), expiry, keyName);
        return sasToken;
    }

    /// <summary>
    /// Convenience: generate a SAS token directly from a Service Bus connection string.
    /// </summary>
    /// <param name="connectionString">
    /// "Endpoint=sb://.../;SharedAccessKeyName=...;SharedAccessKey=..."
    /// </param>
    /// <param name="resourceUri">The full HTTPS URI you will call.</param>
    /// <param name="ttl">Time-to-live for the token.</param>
    public static string GenerateTokenFromConnectionString(string connectionString, string resourceUri, TimeSpan ttl)
    {
        var parts = ParseConnectionString(connectionString);
        if (!parts.TryGetValue("SharedAccessKeyName", out var keyName) ||
            !parts.TryGetValue("SharedAccessKey", out var keyValue))
        {
            throw new ArgumentException("Connection string missing SharedAccessKeyName or SharedAccessKey.");
        }
        return CreateToken(resourceUri, keyName, keyValue);
    }

    private static Dictionary<string, string> ParseConnectionString(string cs)
    {
        return cs.Split([';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split(new[] { '=' }, 2))
            .Where(kv => kv.Length == 2)
            .ToDictionary(kv => kv[0].Trim(), kv => kv[1].Trim(), StringComparer.OrdinalIgnoreCase);
    }
}