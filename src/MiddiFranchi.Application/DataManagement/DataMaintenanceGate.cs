namespace MiddiFranchi.Application.DataManagement;

/// <summary>
/// Serializza backup/ripristino/eliminazione con il servizio di aggiornamento prezzi.
/// Il gate è registrato come singleton e condiviso da tutti i circuiti e dall'hosted service.
/// </summary>
public sealed class DataMaintenanceGate : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public bool IsBusy => _gate.CurrentCount == 0;

    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Releaser(_gate);
    }

    public bool TryEnter(out IDisposable? handle)
    {
        if (_gate.Wait(0))
        {
            handle = new Releaser(_gate);
            return true;
        }

        handle = null;
        return false;
    }

    public void Dispose() => _gate.Dispose();

    private sealed class Releaser : IDisposable
    {
        private SemaphoreSlim? _gate;

        public Releaser(SemaphoreSlim gate)
        {
            _gate = gate;
        }

        public void Dispose()
        {
            _gate?.Release();
            _gate = null;
        }
    }
}
