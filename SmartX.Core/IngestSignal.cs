namespace SmartX.Core;

// Shared by both queues so the processor wakes up no matter which one got something
public sealed class IngestSignal
{
    private readonly SemaphoreSlim _semaphore = new(0);

    public void Wake()
    {
        // Only release if the processor is actually asleep, otherwise the count keeps climbing
        if (_semaphore.CurrentCount == 0)
        {
            _semaphore.Release();
        }
    }

    public Task WaitAsync(CancellationToken cancellationToken) => _semaphore.WaitAsync(cancellationToken);
}
