using Arch.Core.Extensions.Internal;
using Arch.Core.Utils;

namespace Arch.Core;

public partial class World
{

    /// <summary>
    ///     Creates or returns an <see cref="Archetype"/> based on the old one with one additional component.
    ///     Automatically creates a link between them for quick access.
    ///     Thread-safe: acquires <see cref="_structuralLock"/> to make the check-then-act on edge + archetype creation atomic.
    /// </summary>
    /// <param name="type">The new <see cref="ComponentType"/> that additionally forms a new <see cref="Archetype"/> with the old components of the old archetype.</param>
    /// <param name="oldArchetype">The old <see cref="Archetype"/>.</param>
    /// <returns>The cached or newly created <see cref="Archetype"/> with that additional component.</returns>
    private Archetype GetOrCreateArchetypeByAddEdge(in ComponentType type, Archetype oldArchetype)
    {
        lock (_structuralLock)
        {
            Archetype archetype;
            var edgeIndex = type.Id - 1;

            if (!oldArchetype.HasAddEdge(edgeIndex))
            {
                var newSignature = Signature.Add(oldArchetype.Signature, type);
                archetype = GetOrCreateCore(newSignature);
                oldArchetype.AddAddEdge(edgeIndex, archetype);
            }
            else
            {
                archetype = oldArchetype.GetAddEdge(edgeIndex);
            }

            return archetype;
        }
    }

    /// <summary>
    ///     Creates or returns an <see cref="Archetype"/> based on the old one with one additional component.
    ///     Automatically creates a link between them for quick access.
    ///     Thread-safe: acquires <see cref="_structuralLock"/> to make the check-then-act on edge + archetype creation atomic.
    /// </summary>
    /// <param name="type">The new <see cref="ComponentType"/> that additionally forms a new <see cref="Archetype"/> with the old components of the old archetype.</param>
    /// <param name="oldArchetype">The old <see cref="Archetype"/>.</param>
    /// <returns>The cached or newly created <see cref="Archetype"/> with that additional component.</returns>
    private Archetype GetOrCreateArchetypeByRemoveEdge(in ComponentType type, Archetype oldArchetype)
    {
        lock (_structuralLock)
        {
            Archetype archetype;
            var edgeIndex = type.Id - 1;

            if (!oldArchetype.HasRemoveEdge(edgeIndex))
            {
                var newSignature = Signature.Remove(oldArchetype.Signature, type);
                archetype = GetOrCreateCore(newSignature);
                oldArchetype.AddRemoveEdge(edgeIndex, archetype);
            }
            else
            {
                archetype = oldArchetype.GetRemoveEdge(edgeIndex);
            }

            return archetype;
        }
    }
}
