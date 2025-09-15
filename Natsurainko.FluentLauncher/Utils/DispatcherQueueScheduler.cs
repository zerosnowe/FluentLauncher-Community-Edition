using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Threading;

namespace Natsurainko.FluentLauncher.Utils;

public partial class DispatcherQueueScheduler(DispatcherQueue dispatcherQueue) : LocalScheduler
{
    private readonly DispatcherQueue _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));

    public override IDisposable Schedule<TState>(TState state, Func<IScheduler, TState, IDisposable> action)
    {
        var d = new CancellationDisposable();

        _dispatcherQueue.EnqueueAsync(() =>
        {
            try
            {
                if (!d.Token.IsCancellationRequested)
                {
                    action(this, state);
                }
            }
            catch (Exception ex)
            {

            }
        });

        return d;
    }

    public override IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action)
    {
        var d = new CancellationDisposable();

        var timer = new Timer(_ =>
        {
            _dispatcherQueue.EnqueueAsync(() =>
            {
                if (!d.Token.IsCancellationRequested)
                {
                    action(this, state);
                }
            });
        }, null, dueTime, Timeout.InfiniteTimeSpan);

        return new CompositeDisposable(d, timer);
    }
}
