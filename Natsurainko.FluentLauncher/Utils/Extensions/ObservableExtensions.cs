using Microsoft.UI.Dispatching;
using System;
using System.Reactive.Linq;

namespace Natsurainko.FluentLauncher.Utils.Extensions;

public static class ObservableExtensions
{
    public static IObservable<T> SubscribeOnDispatcherQueue<T>(this IObservable<T> source, DispatcherQueue dispatcherQueue)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(dispatcherQueue);

        return source.SubscribeOn(new DispatcherQueueScheduler(dispatcherQueue));
    }

    public static IObservable<T> ObserveOnDispatcherQueue<T>(this IObservable<T> source, DispatcherQueue dispatcherQueue)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(dispatcherQueue);

        return source.ObserveOn(new DispatcherQueueScheduler(dispatcherQueue));
    }
}
