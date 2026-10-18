using StarterAssets;
using UnityEngine;

namespace StorageVision.Core
{
    /// <summary>
    /// What the local player is looking at this frame. Rebuilt once per frame by <see cref="LookAtService"/>.
    /// </summary>
    internal sealed class LookContext
    {
        /// <summary>Hit from the same raycast the game uses for its product panel (PlayerNetwork.interactableMask, 4 m).</summary>
        public bool HasInteractableHit;
        public RaycastHit InteractableHit;

        /// <summary>All hits along the view ray (all layers, triggers included), sorted by distance.</summary>
        public readonly RaycastHit[] AllHits = new RaycastHit[16];
        public int AllHitCount;

        public Camera Camera;
        public PlayerNetwork LocalPlayer;

        /// <summary>True when the game's product panel is visible and moved to the center (avoid duplicate text).</summary>
        public bool GamePanelVisible;
    }

    /// <summary>
    /// Finds what the player looks at. Mirrors the raycast in PlayerNetwork.Update (camera forward, 4 m, interactableMask),
    /// so the result matches the game's own product panel. A second, unfiltered ray is used for objects outside that mask
    /// (e.g. packing tables).
    /// </summary>
    internal sealed class LookAtService
    {
        public const float MaxDistance = 4f;

        private readonly LookContext _context = new LookContext();
        private PlayerNetwork _cachedPlayer;
        private FirstPersonController _cachedController;

        /// <summary>Returns null when there is no local player / camera, or the player is in a menu.</summary>
        public LookContext Update()
        {
            FirstPersonController controller = FirstPersonController.Instance;
            if (controller == null)
            {
                _cachedPlayer = null;
                return null;
            }
            if (controller != _cachedController || _cachedPlayer == null)
            {
                _cachedController = controller;
                _cachedPlayer = controller.GetComponent<PlayerNetwork>();
            }
            Camera cam = Camera.main;
            // Keep showing while our own settings menu has paused input, so changes can be previewed.
            if (_cachedPlayer == null || cam == null || (!controller.allowPlayerInput && !ConfigMenu.IsOpen))
            {
                return null;
            }

            Transform camTransform = cam.transform;
            Vector3 origin = camTransform.position;
            Vector3 direction = camTransform.forward;

            _context.Camera = cam;
            _context.LocalPlayer = _cachedPlayer;
            _context.HasInteractableHit = Physics.Raycast(origin, direction, out _context.InteractableHit, MaxDistance,
                _cachedPlayer.interactableMask);

            int count = Physics.RaycastNonAlloc(origin, direction, _context.AllHits, MaxDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            SortByDistance(_context.AllHits, count);
            _context.AllHitCount = count;
            return _context;
        }

        private static void SortByDistance(RaycastHit[] hits, int count)
        {
            // Insertion sort: tiny arrays, no allocations.
            for (int i = 1; i < count; i++)
            {
                RaycastHit key = hits[i];
                int j = i - 1;
                while (j >= 0 && hits[j].distance > key.distance)
                {
                    hits[j + 1] = hits[j];
                    j--;
                }
                hits[j + 1] = key;
            }
        }
    }
}
