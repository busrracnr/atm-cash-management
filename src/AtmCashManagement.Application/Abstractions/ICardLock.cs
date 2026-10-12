namespace AtmCashManagement.Application.Abstractions;

public interface ICardLock
{
    /// <summary>
    /// Serialises quote creation for one card until the surrounding transaction ends, so parallel requests at
    /// different ATMs cannot together exceed the daily limit. Always taken after the ATM lock to avoid deadlocks.
    /// </summary>
    Task AcquireAsync(string cardHash, CancellationToken cancellationToken);
}
