using StorageVision.Core;
using UnityEngine;

namespace StorageVision.Handlers
{
    /// <summary>
    /// Handles one kind of looked-at object. Handlers are asked in order; the first one that returns true wins.
    /// To support a new object type, implement this and register it in <see cref="Runner"/>.
    /// </summary>
    internal interface ILookHandler
    {
        string Name { get; }

        bool Enabled { get; }

        /// <param name="text">Rich text for the center HUD, or null for no text.</param>
        /// <param name="highlightTarget">Object to outline in LookedAt mode, or null.</param>
        bool TryHandle(LookContext context, out string text, out GameObject highlightTarget);
    }
}
