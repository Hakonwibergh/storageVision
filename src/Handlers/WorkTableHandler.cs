using System.Collections.Generic;
using System.Text;
using StorageVision.Core;
using UnityEngine;

namespace StorageVision.Handlers
{
    /// <summary>
    /// Online-order packing tables. Data lives on the OrderPackaging singleton, indexed by table (0-3):
    ///   currentOrderIn[i] = "orderNumber|customerName|npcId|p_p_p" (items not yet boxed; "" = no order)
    ///   itemsData0..3     = "p|p|p" (items placed on the table from trays)
    /// Both are synced to clients (SyncVar + ClientRpc), so this works for host and clients.
    /// </summary>
    internal sealed class WorkTableHandler : ILookHandler
    {
        private const string Muted = "#B0B0B0";
        private const float PayoutMinFactor = 3.25f; // NPC_Manager: Random.Range(3.25f, 3.5f) per item
        private const float PayoutMaxFactor = 3.5f;
        private const float BadWeatherFactor = 3f;
        private const int BadWeatherIndex = 4;        // GameData.CalculateTodaysWeather: weather >= 4 => badWeatherDay
        private const int BadWeatherPerk = 43;        // UpgradesManager perk 43 => orderingExtraCrashOnBadWeather
        private const float ProximityRadius = 1.3f;
        private const int MaxHierarchyDepth = 8;

        private readonly StringBuilder _sb = new StringBuilder(512);
        private readonly List<int> _orderItems = new List<int>();
        private readonly List<int> _distinct = new List<int>();
        private readonly Dictionary<int, int> _needed = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _onTable = new Dictionary<int, int>();

        // Cache key
        private int _lastTable = -1;
        private string _lastOrder, _lastTableItems, _lastText;
        private bool _lastActive;
        private int _lastWaiting = -1, _lastMaxLines = -1;
        private float _lastWeatherFactor = -1f;
        private OrderValueMode _lastMode;

        public string Name => "WorkTable";

        public bool Enabled => ModConfig.WorkTableEnabled.Value;

        public bool TryHandle(LookContext context, out string text, out GameObject highlightTarget)
        {
            text = null;
            highlightTarget = null;
            OrderPackaging packaging = OrderPackaging.Instance;
            if (packaging == null || packaging.currentOrderIn == null)
            {
                return false;
            }

            int table = FindLookedAtTable(context, packaging);
            if (table < 0 || table >= packaging.currentOrderIn.Length)
            {
                return false;
            }

            string order = packaging.currentOrderIn[table] ?? "";
            string tableItems = TableItemsData(packaging, table) ?? "";
            bool active = packaging.isOrderDepartmentActivated;
            int waiting = CountWaitingOrders(packaging);
            float weatherFactor = WeatherFactor();
            OrderValueMode mode = ModConfig.WorkTableValueMode.Value;
            int maxLines = ModConfig.WorkTableMaxItemLines.Value;

            if (_lastText != null && table == _lastTable && order == _lastOrder && tableItems == _lastTableItems &&
                active == _lastActive && waiting == _lastWaiting && weatherFactor == _lastWeatherFactor &&
                mode == _lastMode && maxLines == _lastMaxLines)
            {
                text = _lastText;
                return true;
            }

            _sb.Length = 0;
            _sb.Append("<b>Packing table #").Append(table + 1).Append("</b>\n");
            if (!active)
            {
                _sb.Append("<color=").Append(Muted).Append(">Online order department is closed</color>\n");
            }

            string[] parts = order.Split('|');
            if (order.Length == 0 || parts.Length < 4)
            {
                _sb.Append("<color=#FF7070>No order assigned</color>");
                _sb.Append("\n<size=85%><color=").Append(Muted).Append(">Orders waiting: ").Append(waiting).Append("</color></size>");
            }
            else
            {
                BuildOrderText(parts, tableItems, mode, maxLines, weatherFactor);
            }

            _lastTable = table;
            _lastOrder = order;
            _lastTableItems = tableItems;
            _lastActive = active;
            _lastWaiting = waiting;
            _lastWeatherFactor = weatherFactor;
            _lastMode = mode;
            _lastMaxLines = maxLines;
            _lastText = _sb.ToString();
            text = _lastText;
            return true;
        }

        private void BuildOrderText(string[] parts, string tableItems, OrderValueMode mode, int maxLines, float weatherFactor)
        {
            _sb.Append("<color=#7CFC7C>Order #").Append(parts[0]).Append("</color>  ").Append(parts[1]).Append('\n');

            ParseIds(parts[3], '_', _orderItems);
            _needed.Clear();
            _distinct.Clear();
            float market = 0f, shelf = 0f;
            foreach (int id in _orderItems)
            {
                if (!_needed.ContainsKey(id))
                {
                    _needed[id] = 0;
                    _distinct.Add(id);
                }
                _needed[id]++;
                market += GameInfo.MarketPrice(id);
                shelf += GameInfo.ShelfPrice(id);
            }

            _onTable.Clear();
            ParseIds(tableItems, '|', _orderItems);
            int onTableUseful = 0;
            foreach (int id in _orderItems)
            {
                _onTable.TryGetValue(id, out int n);
                _onTable[id] = n + 1;
            }
            foreach (int id in _distinct)
            {
                if (_onTable.TryGetValue(id, out int n))
                {
                    onTableUseful += Mathf.Min(n, _needed[id]);
                }
            }

            int left = 0;
            foreach (int id in _distinct)
            {
                left += _needed[id];
            }
            if (left == 0)
            {
                _sb.Append("All items packed");
                return;
            }

            _sb.Append("Items left: <b>").Append(left).Append("</b>");
            if (onTableUseful > 0)
            {
                _sb.Append("  <color=").Append(Muted).Append(">(").Append(onTableUseful).Append(" on table)</color>");
            }
            _sb.Append('\n');

            if (mode == OrderValueMode.Payout || mode == OrderValueMode.All)
            {
                float min = market * PayoutMinFactor * weatherFactor;
                float max = market * PayoutMaxFactor * weatherFactor;
                _sb.Append("Value: <b><color=#FFD700>").Append(GameInfo.Price(min)).Append(" - ").Append(GameInfo.Price(max)).Append("</color></b>");
                if (weatherFactor > 1f)
                {
                    _sb.Append(" <size=80%>(bad weather x3)</size>");
                }
                _sb.Append('\n');
            }
            if (mode == OrderValueMode.Market || mode == OrderValueMode.All)
            {
                _sb.Append("Market value: ").Append(GameInfo.Price(market)).Append('\n');
            }
            if (mode == OrderValueMode.ShelfPrice || mode == OrderValueMode.All)
            {
                _sb.Append("At your prices: ").Append(GameInfo.Price(shelf)).Append('\n');
            }

            if (maxLines > 0)
            {
                _sb.Append("<size=85%>");
                int shown = 0;
                foreach (int id in _distinct)
                {
                    if (shown >= maxLines)
                    {
                        break;
                    }
                    _sb.Append(_needed[id]).Append("x ").Append(GameInfo.ProductName(id));
                    if (_onTable.TryGetValue(id, out int n) && n > 0)
                    {
                        _sb.Append(" <color=#7CFC7C>(").Append(Mathf.Min(n, _needed[id])).Append(" on table)</color>");
                    }
                    _sb.Append('\n');
                    shown++;
                }
                if (_distinct.Count > shown)
                {
                    _sb.Append("<color=").Append(Muted).Append(">+").Append(_distinct.Count - shown).Append(" more products</color>\n");
                }
                _sb.Append("</size>");
            }

            // Trim trailing newline.
            if (_sb.Length > 0 && _sb[_sb.Length - 1] == '\n')
            {
                _sb.Length--;
            }
        }

        private static void ParseIds(string data, char separator, List<int> result)
        {
            result.Clear();
            if (string.IsNullOrEmpty(data))
            {
                return;
            }
            foreach (string s in data.Split(separator))
            {
                if (int.TryParse(s, out int id) && GameInfo.IsValidProduct(id))
                {
                    result.Add(id);
                }
            }
        }

        private static string TableItemsData(OrderPackaging packaging, int table)
        {
            switch (table)
            {
                case 0: return packaging.itemsData0;
                case 1: return packaging.itemsData1;
                case 2: return packaging.itemsData2;
                case 3: return packaging.itemsData3;
                default: return "";
            }
        }

        private static int CountWaitingOrders(OrderPackaging packaging)
        {
            int count = 0;
            if (packaging.ordersData != null)
            {
                foreach (string o in packaging.ordersData)
                {
                    if (!string.IsNullOrEmpty(o))
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// The pickup payout is tripled on bad-weather days with perk 43. NPC_Manager.badWeatherDay is host-only,
        /// so we derive it from the synced GameData.weatherIndex instead.
        /// </summary>
        private static float WeatherFactor()
        {
            GameData data = GameData.Instance;
            if (data == null || data.weatherIndex < BadWeatherIndex)
            {
                return 1f;
            }
            bool perk = NPC_Manager.Instance != null && NPC_Manager.Instance.orderingExtraCrashOnBadWeather;
            if (!perk && data.GetComponent<UpgradesManager>() is UpgradesManager upgrades &&
                upgrades.extraUpgrades != null && upgrades.extraUpgrades.Length > BadWeatherPerk)
            {
                perk = upgrades.extraUpgrades[BadWeatherPerk];
            }
            return perk ? BadWeatherFactor : 1f;
        }

        /// <summary>
        /// Walks the view ray: triggers are checked by hierarchy only; the first solid hit is also matched by
        /// proximity to a table's item spawn pivot, then the search stops (the ray is blocked).
        /// </summary>
        private static int FindLookedAtTable(LookContext context, OrderPackaging packaging)
        {
            Transform player = context.LocalPlayer.transform;
            for (int i = 0; i < context.AllHitCount; i++)
            {
                RaycastHit hit = context.AllHits[i];
                // Skip empty slots and the player's own colliders (held box/tray).
                if (hit.collider == null || hit.transform.IsChildOf(player))
                {
                    continue;
                }
                int index = TableFromHierarchy(hit.transform, packaging);
                if (index >= 0)
                {
                    return index;
                }
                if (!hit.collider.isTrigger)
                {
                    return TableFromProximity(hit.point, packaging);
                }
            }
            return -1;
        }

        private static int TableFromHierarchy(Transform t, OrderPackaging packaging)
        {
            Transform cameras = packaging.interactableCamerasParentOBJ != null ? packaging.interactableCamerasParentOBJ.transform : null;
            Transform boxes = packaging.boxContainersParentOBJ != null ? packaging.boxContainersParentOBJ.transform : null;
            Transform pivots = packaging.itemSpawnPivotsParentOBJ != null ? packaging.itemSpawnPivotsParentOBJ.transform : null;
            Transform canvases = packaging.tablesCanvasesParentOBJ != null ? packaging.tablesCanvasesParentOBJ.transform : null;

            for (int depth = 0; t != null && depth < MaxHierarchyDepth; depth++, t = t.parent)
            {
                if (t.GetComponent<OrderPackagingReference>() != null)
                {
                    return t.GetSiblingIndex();
                }
                Transform parent = t.parent;
                if (parent != null && (parent == cameras || parent == boxes || parent == pivots || parent == canvases))
                {
                    return t.GetSiblingIndex();
                }
            }
            return -1;
        }

        private static int TableFromProximity(Vector3 point, OrderPackaging packaging)
        {
            if (packaging.itemSpawnPivotsParentOBJ == null)
            {
                return -1;
            }
            Transform pivots = packaging.itemSpawnPivotsParentOBJ.transform;
            int best = -1;
            float bestDistance = ProximityRadius * ProximityRadius;
            for (int i = 0; i < pivots.childCount; i++)
            {
                Vector3 delta = pivots.GetChild(i).position - point;
                delta.y = 0f; // tables are at floor level; compare horizontally
                float d = delta.sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }
    }
}
