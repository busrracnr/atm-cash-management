namespace AtmCashManagement.Application.Abstractions;

/// <summary>Turns a card number into a stable, non-reversible identifier; the number itself is never stored.</summary>
public interface ICardNumberHasher
{
    string Hash(string normalizedCardNumber);
}
