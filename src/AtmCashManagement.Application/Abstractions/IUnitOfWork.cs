namespace AtmCashManagement.Application.Abstractions;

public interface IUnitOfWork
{
    /// <summary>Runs the operation in a database transaction, saves the changes and commits.</summary>
    Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken);
}
