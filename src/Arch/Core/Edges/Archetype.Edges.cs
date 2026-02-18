using System.Buffers;
using Arch.Core.Extensions.Internal;
using Arch.Core.Utils;
using Arch.LowLevel.Jagged;

namespace Arch.Core;

public partial class Archetype
{
    /// <summary>
    ///     The bucket size of each bucket inside the <see cref="_addEdges"/>.
    /// </summary>
    private const int BucketSize = 16;

    /// <summary>
    ///     Dedicated lock object for synchronizing access to <see cref="_addEdges"/> and <see cref="_removeEdges"/>.
    ///     Ensures thread safety when multiple worlds (on different threads) trigger archetype edge operations
    ///     that may resize the underlying <see cref="SparseJaggedArray{T}"/> concurrently.
    /// </summary>
    private readonly Lock _edgeLock = new();

    /// <summary>
    ///     Caches other <see cref="Archetype"/>s indexed by the
    ///     <see cref="ComponentType.Id"/> that needs to be added in order to reach them.
    /// </summary>
    /// <remarks>The index used is <see cref="ComponentType.Id"/> minus one.</remarks>
    private readonly SparseJaggedArray<Archetype> _addEdges;

    /// <summary>
    ///     Caches other <see cref="Archetype"/>s indexed by the
    ///     <see cref="ComponentType.Id"/> that needs to be added in order to reach them.
    /// </summary>
    /// <remarks>The index used is <see cref="ComponentType.Id"/> minus one.</remarks>
    private readonly SparseJaggedArray<Archetype> _removeEdges;

    /// <summary>
    ///     Adds an add edge.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="archetype">The <see cref="Archetype"/>.</param>

    internal void AddAddEdge(int index, Archetype archetype)
    {
        lock (_edgeLock)
        {
            _addEdges.EnsureCapacity(index);
            _addEdges.Add(index, archetype);
        }
    }

    /// <summary>
    ///     Adds an remove edge.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="archetype">The <see cref="Archetype"/>.</param>

    internal void AddRemoveEdge(int index, Archetype archetype)
    {
        lock (_edgeLock)
        {
            _removeEdges.EnsureCapacity(index);
            _removeEdges.Add(index, archetype);
        }
    }

    /// <summary>
    ///     Checks if an edge exists.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>True or false.</returns>

    internal bool HasAddEdge(int index)
    {
        lock (_edgeLock)
        {
            return _addEdges.ContainsKey(index);
        }
    }

    /// <summary>
    ///     Checks if an edge exists.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>True or false.</returns>

    internal bool HasRemoveEdge(int index)
    {
        lock (_edgeLock)
        {
            return _removeEdges.ContainsKey(index);
        }
    }

    /// <summary>
    ///     Tries to get a cached archetype that is reached through adding a component
    ///     type to this archetype.
    /// </summary>
    /// <param name="index">
    ///     The index of the archetype in the cache, <see cref="ComponentType.Id"/> - 1
    /// </param>
    /// <returns>The cached archetype if it exists, null otherwise.</returns>

    internal Archetype GetAddEdge(int index)
    {
        lock (_edgeLock)
        {
            return _addEdges[index];
        }
    }

    /// <summary>
    ///     Tries to get a cached archetype that is reached through adding a component
    ///     type to this archetype.
    /// </summary>
    /// <param name="index">
    ///     The index of the archetype in the cache, <see cref="ComponentType.Id"/> - 1
    /// </param>
    /// <returns>The cached archetype if it exists, null otherwise.</returns>

    internal Archetype GetRemoveEdge(int index)
    {
        lock (_edgeLock)
        {
            return _removeEdges[index];
        }
    }

    /// <summary>
    ///     Removes an edge for a certain <see cref="Archetype"/>.
    /// </summary>
    /// <param name="archetype">The <see cref="Archetype"/> to remove edges for.</param>

    internal void RemoveEdge(Archetype archetype)
    {
        lock (_edgeLock)
        {
            for (var index = 0; index < _addEdges.Buckets; index++)
            {
                // Skip empty buckets
                ref var bucket = ref _addEdges.GetBucket(index);
                if (bucket.IsEmpty)
                {
                    continue;
                }

                // Search bucket for edge and remove it if found
                for (var itemIndex = 0; itemIndex < bucket.Capacity; itemIndex++)
                {
                    var edge = bucket[itemIndex];
                    if (edge != archetype)
                    {
                        continue;
                    }

                    // Remove from bucket and if the removal caused it being trimmed, break the search and continue with the next
                    _addEdges.Remove((index * BucketSize) + itemIndex);
                    if (bucket.IsEmpty)
                    {
                        break;
                    }
                }
            }

            for (var index = 0; index < _removeEdges.Buckets; index++)
            {
                // Skip empty buckets
                ref var bucket = ref _removeEdges.GetBucket(index);
                if (bucket.IsEmpty)
                {
                    continue;
                }

                // Search bucket for edge and remove it if found
                for (var itemIndex = 0; itemIndex < bucket.Capacity; itemIndex++)
                {
                    var edge = bucket[itemIndex];
                    if (edge != archetype)
                    {
                        continue;
                    }

                    // Remove from bucket and if the removal caused it being trimmed, break the search and continue with the next
                    _removeEdges.Remove((index * BucketSize) + itemIndex);
                    if (bucket.IsEmpty)
                    {
                        break;
                    }
                }
            }
        }
    }
}
