namespace MiddiFranchi.Application.Persistence;

public interface ITransactionRunner
{
    Task<T> RunAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
