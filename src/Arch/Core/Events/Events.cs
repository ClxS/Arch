namespace Arch.Core.Events;

/// <summary>
///     Pairs a handler with a global sequence number for ordered dispatch across handler lists.
/// </summary>
internal readonly record struct Sequenced<T>(T Handler, long Sequence);

/// <summary>
///     The <see cref="Events"/> class
///     acts as a storage for all registered event handlers and stores them properly in lists.
/// </summary>
internal class Events
{
    internal readonly List<Sequenced<ComponentAddedHandler>> ComponentAddedHandlers = [];
    internal readonly List<Sequenced<ComponentSetHandler>> ComponentSetHandlers = [];
    internal readonly List<Sequenced<ComponentRemovedHandler>> ComponentRemovedHandlers = [];
}

/// <summary>
///     The <see cref="Events{T}"/> class
///     acts as a storage for generic events and stores them in specified lists.
/// </summary>
/// <typeparam name="T"></typeparam>
internal class Events<T> : Events
{
    internal readonly List<Sequenced<ComponentAddedHandler<T>>> ComponentAddedGenericHandlers = [];
    internal readonly List<Sequenced<ComponentSetHandler<T>>> ComponentSetGenericHandlers = [];
    internal readonly List<Sequenced<ComponentRemovedHandler<T>>> ComponentRemovedGenericHandlers = [];
}
