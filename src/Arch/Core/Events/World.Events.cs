using Arch.Core.Events;
using Arch.Core.Extensions;
using Arch.Core.Utils;

// ReSharper disable once CheckNamespace
namespace Arch.Core;

public partial class World
{
    // A note on multithreading:
    // This area is the trickiest part of World in terms of thread-safety.
    // It is currently thread-safe, but it relies on an important fact: No list of handlers can ever shrink or be disposed.
    // i.e. no handlers can ever be unsubscribed.
    // So don't try to write any unsubscribe methods without refactoring the thread-safety!

    /// <summary>
    ///     The initial capacity for the <see cref="_compEvents"/> array.
    /// </summary>
    private const int InitialCapacity = 128;

    /// <summary>
    ///     Global sequence counter for handler registration ordering.
    ///     Ensures typed and non-typed handlers fire in registration order.
    /// </summary>
    private long _handlerSequence;

    /// <summary>
    ///     All <see cref="EntityCreatedHandler"/>s in a <see cref="List{T}"/> which will be called upon entity creation.
    /// </summary>
    private readonly List<EntityCreatedHandler> _entityCreatedHandlers = new(InitialCapacity);

    /// <summary>
    ///     All <see cref="EntityDestroyedHandler"/>s in a <see cref="List{T}"/> which will be called after entity destruction.
    /// </summary>
    private readonly List<EntityDestroyedHandler> _entityDestroyedHandlers = new(InitialCapacity);

    /// <summary>
    ///     All <see cref="ComponentTypeAddedHandler"/>s in a <see cref="List{T}"/> which will be called after Component added.
    /// </summary>
    private readonly List<Sequenced<ComponentTypeAddedHandler>> _componentTypeAddedHandlers = [];

    /// <summary>
    ///     All <see cref="ComponentTypeAddedHandler"/>s in a <see cref="List{T}"/> which will be called after Component set.
    /// </summary>
    private readonly List<Sequenced<ComponentTypeSetHandler>> _componentTypeSetHandlers = [];

    /// <summary>
    ///     All <see cref="ComponentTypeAddedHandler"/>s in a <see cref="List{T}"/> which will be called after Component removed.
    /// </summary>
    private readonly List<Sequenced<ComponentTypeRemovedHandler>> _componentTypeRemovedHandlers = [];

    /// <summary>
    ///     All <see cref="Events"/> in an array which will be acessed for add, remove or set operations.
    /// </summary>
    private Events.Events[] _compEvents = new Events.Events[InitialCapacity];

    /// <summary>
    ///     Adds a delegate to be called when an entity is created.
    /// </summary>
    /// <param name="handler">The delegate to call.</param>
    public void SubscribeEntityCreated(EntityCreatedHandler handler)
    {
#if EVENTS
        lock (_entityCreatedHandlers)
        {
            _entityCreatedHandlers.Add(handler);
        }
#endif
    }

    /// <summary>
    ///     Adds a delegate to be called after an entity is destroyed.
    /// </summary>
    /// <param name="handler">The delegate to call.</param>
    public void SubscribeEntityDestroyed(EntityDestroyedHandler handler)
    {
#if EVENTS
        lock (_entityDestroyedHandlers)
        {
            _entityDestroyedHandlers.Add(handler);
        }
#endif
    }

    /// <summary>
    ///     Adds a delegate to be called when a component of type <typeparamref name="T"/> is added to an entity.
    ///     <see cref="Add"/>
    /// </summary>
    /// <param name="handler">The delegate to call.</param>
    /// <typeparam name="T">The component type.</typeparam>
    public void SubscribeComponentAdded<T>(ComponentAddedHandler<T> handler)
    {
#if EVENTS
        long seq = Interlocked.Increment(ref _handlerSequence);

        ref readonly var events = ref GetEvents<T>();
        lock (events.ComponentAddedGenericHandlers)
        {
            events.ComponentAddedGenericHandlers.Add(new(handler, seq));
        }

        lock (events.ComponentAddedHandlers)
        {
            events.ComponentAddedHandlers.Add(new((in Entity entity) =>
            {
                ref var compGeneric = ref Get<T>(entity);
                handler(entity, ref compGeneric);
            }, seq));
        }
#endif
    }

    /// <summary>
    ///     Adds a delegate to be called when a component of type <typeparamref name="T"/> is added to an entity.
    ///     <see cref="Add"/>
    /// </summary>
    /// <param name="handler">The delegate to call.</param>
    /// <typeparam name="T">The component type.</typeparam>
    public void SubscribeComponentAdded(ComponentTypeAddedHandler handler)
    {
#if EVENTS
        long seq = Interlocked.Increment(ref _handlerSequence);
        lock (_componentTypeAddedHandlers)
        {
            _componentTypeAddedHandlers.Add(new(handler, seq));
        }
#endif
    }

    /// <summary>
    ///     Adds a delegate to be called when any is set on an entity.
    ///     <see cref="Set"/>
    /// </summary>
    /// <param name="handler">The delegate to call.</param>
    /// <typeparam name="T">The component type.</typeparam>
    public void SubscribeComponentSet(ComponentTypeSetHandler handler)
    {
#if EVENTS
        long seq = Interlocked.Increment(ref _handlerSequence);
        lock (_componentTypeSetHandlers)
        {
            _componentTypeSetHandlers.Add(new(handler, seq));
        }
#endif
    }

    /// <summary>
    ///     Adds a delegate to be called when a component of type <typeparamref name="T"/> is set on an entity.
    ///     <see cref="Set"/>
    /// </summary>
    /// <param name="handler">The delegate to call.</param>
    /// <typeparam name="T">The component type.</typeparam>
    public void SubscribeComponentSet<T>(ComponentSetHandler<T> handler)
    {
#if EVENTS
        long seq = Interlocked.Increment(ref _handlerSequence);

        ref readonly var events = ref GetEvents<T>();
        lock (events.ComponentSetGenericHandlers)
        {
            events.ComponentSetGenericHandlers.Add(new(handler, seq));
        }

        lock (events.ComponentSetHandlers)
        {
            events.ComponentSetHandlers.Add(new((in Entity entity) =>
            {
                ref var compGeneric = ref Get<T>(entity);
                handler(entity, ref compGeneric);
            }, seq));
        }
#endif
    }

    /// <summary>
    ///     Adds a delegate to be called when a component of type <typeparamref name="T"/> is removed from an entity.
    ///     <see cref="Remove"/>
    /// </summary>
    /// <param name="handler">The delegate to call.</param>
    /// <typeparam name="T">The component type.</typeparam>
    public void SubscribeComponentRemoved<T>(ComponentRemovedHandler<T> handler)
    {
#if EVENTS
        long seq = Interlocked.Increment(ref _handlerSequence);

        ref readonly var events = ref GetEvents<T>();
        lock (events.ComponentRemovedGenericHandlers)
        {
            events.ComponentRemovedGenericHandlers.Add(new(handler, seq));
        }

        lock (events.ComponentRemovedHandlers)
        {
            events.ComponentRemovedHandlers.Add(new((in Entity entity) =>
            {
                ref var compGeneric = ref Get<T>(entity);
                handler(entity, ref compGeneric);
            }, seq));
        }
#endif
    }

    /// <summary>
    ///     Adds a delegate to be called when a component of type <typeparamref name="T"/> is removed from an entity.
    ///     <see cref="Remove"/>
    /// </summary>
    /// <param name="handler">The delegate to call.</param>
    /// <typeparam name="T">The component type.</typeparam>
    public void SubscribeComponentRemoved(ComponentTypeRemovedHandler handler)
    {
#if EVENTS
        long seq = Interlocked.Increment(ref _handlerSequence);
        lock (_componentTypeRemovedHandlers)
        {
            _componentTypeRemovedHandlers.Add(new(handler, seq));
        }
#endif
    }

    /// <summary>
    ///     Calls all handlers subscribed to entity creation.
    /// </summary>
    /// <param name="entity">The entity that got created.</param>

    public void OnEntityCreated(Entity entity)
    {
#if EVENTS
        int count;
        lock (_entityCreatedHandlers)
        {
            count = _entityCreatedHandlers.Count;
        }

        for (var i = 0; i < count; i++)
        {
            EntityCreatedHandler handler;
            lock (_entityCreatedHandlers)
            {
                handler = _entityCreatedHandlers[i];
            }

            handler.Invoke(in entity);
        }
#endif
    }

    /// <summary>
    ///     Calls all handlers subscribed to entity deletion.
    /// </summary>
    /// <param name="entity">The entity that got destroyed.</param>

    public void OnEntityDestroyed(Entity entity)
    {
#if EVENTS
        int count;
        lock (_entityDestroyedHandlers)
        {
            count = _entityDestroyedHandlers.Count;
        }

        for (var i = 0; i < _entityDestroyedHandlers.Count; i++)
        {
            EntityDestroyedHandler handler;
            lock (_entityDestroyedHandlers)
            {
                handler = _entityDestroyedHandlers[i];
            }

            handler.Invoke(in entity);
        }
#endif
    }

    /// <summary>
    ///     Calls all generic handlers subscribed to component addition of this type.
    ///     Handlers are dispatched in registration order across both typed and non-typed lists.
    /// </summary>
    /// <param name="entity">The entity that the component was added to.</param>
    /// <typeparam name="T">The type of component that got added.</typeparam>

    public void OnComponentAdded<T>(Entity entity)
    {
#if EVENTS
        ref readonly var events = ref GetEvents<T>();
        ref var added = ref Get<T>(entity);

        int globalCount, typedCount;
        lock (_componentTypeAddedHandlers) { globalCount = _componentTypeAddedHandlers.Count; }
        lock (events.ComponentAddedGenericHandlers) { typedCount = events.ComponentAddedGenericHandlers.Count; }

        int gi = 0, ti = 0;
        while (gi < globalCount || ti < typedCount)
        {
            long globalSeq = gi < globalCount ? _componentTypeAddedHandlers[gi].Sequence : long.MaxValue;
            long typedSeq = ti < typedCount ? events.ComponentAddedGenericHandlers[ti].Sequence : long.MaxValue;

            if (globalSeq <= typedSeq)
            {
                _componentTypeAddedHandlers[gi].Handler.Invoke(in entity, Component.GetComponentType(typeof(T)));
                gi++;
            }
            else
            {
                events.ComponentAddedGenericHandlers[ti].Handler(in entity, ref added);
                ti++;
            }
        }
#endif
    }

    /// <summary>
    ///     Calls all generic handlers subscribed to component setting of this type.
    ///     Handlers are dispatched in registration order across both typed and non-typed lists.
    /// </summary>
    /// <param name="entity">The entity that the component was set on.</param>
    /// <typeparam name="T">The type of component that got set.</typeparam>

    public void OnComponentSet<T>(Entity entity)
    {
#if EVENTS
        ref readonly var events = ref GetEvents<T>();
        ref var set = ref Get<T>(entity);

        int globalCount, typedCount;
        lock (_componentTypeSetHandlers) { globalCount = _componentTypeSetHandlers.Count; }
        lock (events.ComponentSetGenericHandlers) { typedCount = events.ComponentSetGenericHandlers.Count; }

        int gi = 0, ti = 0;
        while (gi < globalCount || ti < typedCount)
        {
            long globalSeq = gi < globalCount ? _componentTypeSetHandlers[gi].Sequence : long.MaxValue;
            long typedSeq = ti < typedCount ? events.ComponentSetGenericHandlers[ti].Sequence : long.MaxValue;

            if (globalSeq <= typedSeq)
            {
                _componentTypeSetHandlers[gi].Handler.Invoke(in entity, Component.GetComponentType(typeof(T)));
                gi++;
            }
            else
            {
                events.ComponentSetGenericHandlers[ti].Handler(in entity, ref set);
                ti++;
            }
        }
#endif
    }

    /// <summary>
    ///     Calls all generic handlers subscribed to component removal.
    ///     Handlers are dispatched in registration order across both typed and non-typed lists.
    /// </summary>
    /// <param name="entity">The entity that the component was removed from.</param>
    /// <typeparam name="T">The type of component that got removed.</typeparam>

    public void OnComponentRemoved<T>(Entity entity)
    {
#if EVENTS
        ref readonly var events = ref GetEvents<T>();
        ref var removed = ref Get<T>(entity);

        int globalCount, typedCount;
        lock (_componentTypeRemovedHandlers) { globalCount = _componentTypeRemovedHandlers.Count; }
        lock (events.ComponentRemovedGenericHandlers) { typedCount = events.ComponentRemovedGenericHandlers.Count; }

        int gi = 0, ti = 0;
        while (gi < globalCount || ti < typedCount)
        {
            long globalSeq = gi < globalCount ? _componentTypeRemovedHandlers[gi].Sequence : long.MaxValue;
            long typedSeq = ti < typedCount ? events.ComponentRemovedGenericHandlers[ti].Sequence : long.MaxValue;

            if (globalSeq <= typedSeq)
            {
                _componentTypeRemovedHandlers[gi].Handler.Invoke(in entity, Component.GetComponentType(typeof(T)));
                gi++;
            }
            else
            {
                events.ComponentRemovedGenericHandlers[ti].Handler(in entity, ref removed);
                ti++;
            }
        }
#endif
    }

    /// <summary>
    ///     Calls all handlers subscribed to component addition of this type.
    /// </summary>
    /// <param name="entity">The entity that the component was added to.</param>
    /// <param name="compType">The type of component that got added.</param>

    public void OnComponentAdded(Entity entity, ComponentType compType)
    {
#if EVENTS
        var events = GetEvents(compType);

        int globalCount, perTypeCount = 0;
        lock (_componentTypeAddedHandlers) { globalCount = _componentTypeAddedHandlers.Count; }
        if (events != null) { lock (events.ComponentAddedHandlers) { perTypeCount = events.ComponentAddedHandlers.Count; } }

        int gi = 0, ti = 0;
        while (gi < globalCount || ti < perTypeCount)
        {
            long globalSeq = gi < globalCount ? _componentTypeAddedHandlers[gi].Sequence : long.MaxValue;
            long typedSeq = ti < perTypeCount ? events!.ComponentAddedHandlers[ti].Sequence : long.MaxValue;

            if (globalSeq <= typedSeq)
            {
                _componentTypeAddedHandlers[gi].Handler.Invoke(in entity, compType);
                gi++;
            }
            else
            {
                events!.ComponentAddedHandlers[ti].Handler(in entity);
                ti++;
            }
        }
#endif
    }

    /// <summary>
    ///     Calls all handlers subscribed to component setting of this type.
    /// </summary>
    /// <param name="entity">The entity that the component was set on.</param>
    /// <param name="comp">The component instance that got set.</param>

    public void OnComponentSet(Entity entity, object comp)
    {
#if EVENTS
        var compType = Component.GetComponentType(comp.GetType());
        var events = GetEvents(comp.GetType());

        int globalCount, perTypeCount = 0;
        lock (_componentTypeSetHandlers) { globalCount = _componentTypeSetHandlers.Count; }
        if (events != null) { lock (events.ComponentSetHandlers) { perTypeCount = events.ComponentSetHandlers.Count; } }

        int gi = 0, ti = 0;
        while (gi < globalCount || ti < perTypeCount)
        {
            long globalSeq = gi < globalCount ? _componentTypeSetHandlers[gi].Sequence : long.MaxValue;
            long typedSeq = ti < perTypeCount ? events!.ComponentSetHandlers[ti].Sequence : long.MaxValue;

            if (globalSeq <= typedSeq)
            {
                _componentTypeSetHandlers[gi].Handler.Invoke(in entity, compType);
                gi++;
            }
            else
            {
                events!.ComponentSetHandlers[ti].Handler(in entity);
                ti++;
            }
        }
#endif
    }

    /// <summary>
    ///     Calls all handlers subscribed to component removal.
    /// </summary>
    /// <param name="entity">The entity that the component was removed from.</param>
    /// <param name="compType">The type of component that got removed.</param>

    public void OnComponentRemoved(Entity entity, ComponentType compType)
    {
#if EVENTS
        var events = GetEvents(compType);

        int globalCount, perTypeCount = 0;
        lock (_componentTypeRemovedHandlers) { globalCount = _componentTypeRemovedHandlers.Count; }
        if (events != null) { lock (events.ComponentRemovedHandlers) { perTypeCount = events.ComponentRemovedHandlers.Count; } }

        int gi = 0, ti = 0;
        while (gi < globalCount || ti < perTypeCount)
        {
            long globalSeq = gi < globalCount ? _componentTypeRemovedHandlers[gi].Sequence : long.MaxValue;
            long typedSeq = ti < perTypeCount ? events!.ComponentRemovedHandlers[ti].Sequence : long.MaxValue;

            if (globalSeq <= typedSeq)
            {
                _componentTypeRemovedHandlers[gi].Handler.Invoke(in entity, compType);
                gi++;
            }
            else
            {
                events!.ComponentRemovedHandlers[ti].Handler(in entity);
                ti++;
            }
        }
#endif
    }

    /// <summary>
    ///     Calls all handlers subscribed to component addition of this type for entities in a archetype range.
    /// </summary>
    /// <param name="archetype">The <see cref="Archetype"/>.</param>
    /// <typeparam name="T">The component type.</typeparam>

    internal void OnComponentAdded<T>(Archetype archetype)
    {
#if EVENTS
        // Set the added component, start from the last slot and move down
        foreach (ref var chunk in archetype)
        {
            ref var firstEntity = ref chunk.Entity(0);
            foreach (var index in chunk)
            {
                var entity = Unsafe.Add(ref firstEntity, index);
                OnComponentAdded<T>(entity);
            }
        }
#endif
    }

    /// <summary>
    ///     Calls all handlers subscribed to component removal of this type for entities in a archetype range.
    /// </summary>
    /// <param name="archetype">The <see cref="Archetype"/>.</param>
    /// <typeparam name="T">The component type.</typeparam>

    internal void OnComponentRemoved<T>(Archetype archetype)
    {
#if EVENTS
        // Set the added component, start from the last slot and move down
        foreach (ref var chunk in archetype)
        {
            ref var firstEntity = ref chunk.Entity(0);
            foreach (var index in chunk)
            {
                var entity = Unsafe.Add(ref firstEntity, index);
                OnComponentRemoved<T>(entity);
            }
        }
#endif
    }

    /// <summary>
    ///     Gets all generic event handlers for a certain component type.
    /// </summary>
    /// <typeparam name="T">The type of component to get handlers for.</typeparam>
    /// <returns>All handlers for the given component type.</returns>

    private ref readonly Events<T> GetEvents<T>()
    {
        var index = EventType<T>.Id;
        lock (_compEvents)
        {
            if (index >= _compEvents.Length)
            {
                Array.Resize(ref _compEvents, (index * 2) + 1);
            }
            // This must be in lock so we get a current(ish) reference
            ref var events = ref _compEvents[index];
            // This must be in lock along with Resize in case we resize in a different thread before assigning a new Events
            // to the old array.
            // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            events ??= new Events<T>();

            // Thread safety: Here, even though it's a reference, if the array was/will be resized, our Events will still be valid.
            // So any callers can use it in peace, knowing that it won't get GC'd out of existence, until they're done.
            // Of note, once created, an Events<T> can never change index or go invalid, just get copied to new arrays.
            return ref Unsafe.As<Events.Events, Events<T>>(ref events);
        }
    }

    /// TODO : Remove creating by activator. Instead we should probably keep two lists. One for object based calls, one for generics.
    /// <summary>
    ///     Gets all event handlers for a certain component type.
    /// </summary>
    /// <param name="compType">The type of component to get handlers for.</param>
    /// <returns>All handlers for the given component type, or null if there are none.</returns>

    private Events.Events? GetEvents(ComponentType compType)
    {
        // Try to get the event from the registry, otherwise return a null ref since there's none
        // This is thread-safe due to ConcurrentDictionary.
        if (!EventTypeRegistry.EventIds.TryGetValue(compType, out var index))
        {
            return null;
        }

        lock (_compEvents)
        {
            if (index >= _compEvents.Length)
            {
                Array.Resize(ref _compEvents, (index * 2) + 1);
            }

            ref var events = ref _compEvents[index];
            // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            // Better hope it is not null
            events ??= (Events.Events?)Activator.CreateInstance(typeof(Events<>).MakeGenericType(compType))!;
            return events;
        }
    }
}
