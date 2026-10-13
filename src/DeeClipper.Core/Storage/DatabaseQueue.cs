using System.Threading.Channels;
using Microsoft.Data.Sqlite;

namespace DeeClipper.Core.Storage;

/// <summary>
/// Dedicated background write queue ensuring all SQLite database write operations
/// execute sequentially on a single thread and connection, eliminating database locked errors.
/// </summary>
public sealed class DatabaseQueue : IAsyncDisposable
{
    private readonly Channel<Func<SqliteConnection, Task>> _queue;
    private readonly Task _workerTask;
    private readonly CancellationTokenSource _cts = new();
    private readonly string _connectionString;

    public DatabaseQueue(string connectionString)
    {
        _connectionString = connectionString;
        _queue = Channel.CreateUnbounded<Func<SqliteConnection, Task>>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _workerTask = Task.Run(ProcessQueueAsync);
    }

    private async Task ProcessQueueAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(_cts.Token);

        // Configure WAL mode and performance pragmas on the dedicated writer connection
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;
                PRAGMA foreign_keys = ON;
            ";
            await cmd.ExecuteNonQueryAsync(_cts.Token);
        }

        var reader = _queue.Reader;
        while (await reader.WaitToReadAsync(_cts.Token))
        {
            while (reader.TryRead(out var workItem))
            {
                try
                {
                    await workItem(connection);
                }
                catch
                {
                    // Exceptions are observed and forwarded via TaskCompletionSource in EnqueueAsync
                }
            }
        }
    }

    /// <summary>
    /// Enqueues a write operation to execute sequentially on the writer thread.
    /// Returns the result or propagates any thrown exception.
    /// </summary>
    public Task<T> EnqueueAsync<T>(Func<SqliteConnection, Task<T>> writeFunc)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        var posted = _queue.Writer.TryWrite(async conn =>
        {
            try
            {
                var result = await writeFunc(conn);
                tcs.TrySetResult(result);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        if (!posted)
        {
            tcs.TrySetException(new InvalidOperationException("The database write queue is closed."));
        }

        return tcs.Task;
    }

    /// <summary>
    /// Enqueues a write operation that does not return a value.
    /// </summary>
    public Task EnqueueAsync(Func<SqliteConnection, Task> writeAction)
    {
        return EnqueueAsync<bool>(async conn =>
        {
            await writeAction(conn);
            return true;
        });
    }

    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete();
        _cts.Cancel();
        try
        {
            await _workerTask;
        }
        catch (OperationCanceledException)
        {
            // Worker canceled cleanly
        }
        _cts.Dispose();
    }
}
