using UnityEngine;

namespace StorageVision.Handlers
{
    /// <summary>
    /// Finds the nearest box holding the product the game's order arrows point at
    /// (PinpointShelfFromTray.currentProductID, set by OrderingTray while the tray is held).
    /// Storage boxes are preferred; a product shelf row is used as fallback. Re-scans a few times per second only.
    /// </summary>
    internal sealed class OrderTargetFinder
    {
        private const float RescanInterval = 0.25f;
        private const int TrayItem = 9;

        private float _nextScan;
        private GameObject _cached;
        private NetworkSpawner _spawner;

        public GameObject Find(PlayerNetwork player, Vector3 viewerPosition)
        {
            if (player == null || player.equippedItem != TrayItem)
            {
                _cached = null;
                return null;
            }
            OrderPackaging packaging = OrderPackaging.Instance;
            PinpointShelfFromTray pinpointer = packaging != null ? packaging.pinpointerComponent : null;
            if (pinpointer == null || pinpointer.currentProductID < 0)
            {
                _cached = null;
                return null;
            }

            if (Time.unscaledTime < _nextScan && _cached != null)
            {
                return _cached;
            }
            _nextScan = Time.unscaledTime + RescanInterval;
            _cached = Scan(pinpointer.currentProductID, viewerPosition);
            return _cached;
        }

        private GameObject Scan(int productId, Vector3 viewerPosition)
        {
            if (_spawner == null)
            {
                if (GameData.Instance == null)
                {
                    return null;
                }
                _spawner = GameData.Instance.GetComponent<NetworkSpawner>();
            }
            if (_spawner == null || _spawner.levelPropsOBJ == null || _spawner.levelPropsOBJ.transform.childCount < 2)
            {
                return null;
            }
            Transform props = _spawner.levelPropsOBJ.transform;

            // Same parents the game's PinpointShelfFromTray uses: child 0 = product shelves, child 1 = storage.
            GameObject best = ScanParent(props.GetChild(1), "BoxContainer", productId, viewerPosition, true);
            if (best == null)
            {
                best = ScanParent(props.GetChild(0), "ProductContainer", productId, viewerPosition, false);
            }
            return best;
        }

        private static GameObject ScanParent(Transform parent, string slotsName, int productId, Vector3 viewer, bool storage)
        {
            GameObject best = null;
            float bestDistance = float.MaxValue;
            foreach (Transform shelf in parent)
            {
                Data_Container container = shelf.GetComponent<Data_Container>();
                if (container == null || container.productInfoArray == null)
                {
                    continue;
                }
                Transform slots = shelf.Find(slotsName);
                if (slots == null)
                {
                    continue;
                }
                int[] info = container.productInfoArray;
                int count = Mathf.Min(info.Length / 2, slots.childCount);
                for (int i = 0; i < count; i++)
                {
                    if (info[i * 2] != productId || info[i * 2 + 1] <= 0)
                    {
                        continue;
                    }
                    Transform slot = slots.GetChild(i);
                    if (slot.childCount == 0)
                    {
                        continue;
                    }
                    // Storage: the box object. Product shelf: the whole row of products.
                    GameObject candidate = storage ? slot.GetChild(0).gameObject : slot.gameObject;
                    float distance = (candidate.transform.position - viewer).sqrMagnitude;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = candidate;
                    }
                }
            }
            return best;
        }
    }
}
