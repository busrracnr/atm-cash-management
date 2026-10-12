using System.Security.Cryptography;
using System.Text;

using AtmCashManagement.Application.Abstractions;

namespace AtmCashManagement.Infrastructure.Security;

/// <summary>
/// HMAC-SHA256 with a secret key kept outside the database. A plain hash would not be enough: card numbers have
/// little entropy (known issuer prefix, Luhn digit), so an unkeyed hash can be reversed by trying every number.
/// </summary>
internal sealed class HmacCardNumberHasher : ICardNumberHasher
{
    public const string KeySetting = "Security:CardHashKey";
    private const int MinimumKeyLength = 32;

    private readonly byte[] _key;

    public HmacCardNumberHasher(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length < MinimumKeyLength)
        {
            throw new InvalidOperationException($"'{KeySetting}' must be configured with at least {MinimumKeyLength} characters.");
        }

        _key = Encoding.UTF8.GetBytes(key);
    }

    public string Hash(string normalizedCardNumber) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(normalizedCardNumber)));
}
