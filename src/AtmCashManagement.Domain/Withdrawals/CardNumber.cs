using AtmCashManagement.Domain.Common;

namespace AtmCashManagement.Domain.Withdrawals;

public static class CardNumber
{
    /// <summary>Strips spaces and dashes and validates length and Luhn checksum; returns the bare digits.</summary>
    public static string Normalize(string input)
    {
        var digits = new string(input.Where(c => c is not (' ' or '-')).ToArray());
        if (digits.Length is < 12 or > 19 || !digits.All(char.IsAsciiDigit) || !PassesLuhn(digits))
        {
            throw new DomainException(WithdrawalErrors.InvalidCardNumber());
        }

        return digits;
    }

    private static bool PassesLuhn(string digits)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var digit = digits[digits.Length - 1 - i] - '0';
            if (i % 2 == 1)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
        }

        return sum % 10 == 0;
    }
}
