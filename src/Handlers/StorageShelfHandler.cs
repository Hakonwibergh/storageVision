using System.Text;
using StorageVision.Core;
using UnityEngine;

namespace StorageVision.Handlers
{
    /// <summary>
    /// Storage shelf slots. Data: Data_Container.productInfoArray holds [productID, amount] per slot
    /// (amount -1 = no box). Visual box: Data_Container/BoxContainer/slot[i]/box (spawned by Data_Container.BoxSpawner).
    /// The raycast hits the slot itself (it carries InteractableContainer).
    /// </summary>
    internal sealed class StorageShelfHandler : ILookHandler
    {
        private static readonly string Muted = "#B0B0B0";

        private readonly StringBuilder _sb = new StringBuilder(256);

        // Cache: rebuild the text only when something visible changes.
        private Data_Container _lastContainer;
        private int _lastSlot = -1, _lastProduct = int.MinValue, _lastAmount = int.MinValue;
        private float _lastShelfPrice = -1f, _lastMarketPrice = -1f;
        private bool _lastPanelVisible, _lastShowPrices;
        private string _lastText;

        public string Name => "Storage";

        // Also runs with the text off, so the looked-at box can still be outlined.
        public bool Enabled => ModConfig.StorageEnabled.Value || ModConfig.HighlightEnabled.Value;

        public bool TryHandle(LookContext context, out string text, out GameObject highlightTarget)
        {
            text = null;
            highlightTarget = null;
            if (!context.HasInteractableHit)
            {
                return false;
            }

            Transform slot = context.InteractableHit.transform;
            InteractableContainer interactable = slot.GetComponent<InteractableContainer>();
            if (interactable == null || !interactable.isStorageShelf || interactable.isManufacturing || slot.parent == null || slot.parent.parent == null)
            {
                return false;
            }
            Data_Container container = slot.parent.parent.GetComponent<Data_Container>();
            if (container == null || container.productInfoArray == null)
            {
                return false;
            }

            int slotIndex = slot.GetSiblingIndex();
            int[] info = container.productInfoArray;
            if (slotIndex * 2 + 1 >= info.Length)
            {
                return false;
            }
            int productId = info[slotIndex * 2];
            int amount = info[slotIndex * 2 + 1];

            if (slot.childCount > 0)
            {
                highlightTarget = slot.GetChild(0).gameObject;
            }
            if (!ModConfig.StorageEnabled.Value)
            {
                return true;
            }

            bool validProduct = GameInfo.IsValidProduct(productId);
            float shelfPrice = validProduct ? GameInfo.ShelfPrice(productId) : 0f;
            float marketPrice = validProduct ? GameInfo.MarketPrice(productId) : 0f;
            bool showPrices = ModConfig.StorageShowPrices.Value;

            if (_lastText != null && container == _lastContainer && slotIndex == _lastSlot && productId == _lastProduct &&
                amount == _lastAmount && shelfPrice == _lastShelfPrice && marketPrice == _lastMarketPrice &&
                context.GamePanelVisible == _lastPanelVisible && showPrices == _lastShowPrices)
            {
                text = _lastText;
                return true;
            }

            _sb.Length = 0;
            if (!validProduct)
            {
                _sb.Append("<color=").Append(Muted).Append(">Empty storage slot</color>");
            }
            else
            {
                // The game's own panel already shows name/brand/image when it is centered.
                if (!context.GamePanelVisible)
                {
                    _sb.Append("<b>").Append(GameInfo.ProductName(productId)).Append("</b>");
                    string brand = GameInfo.ProductBrand(productId);
                    if (!string.IsNullOrEmpty(brand))
                    {
                        _sb.Append("  <size=80%><color=").Append(Muted).Append('>').Append(brand).Append("</color></size>");
                    }
                    _sb.Append('\n');
                }

                if (amount < 0)
                {
                    _sb.Append("<color=").Append(Muted).Append(">No box (slot labeled for this product)</color>");
                }
                else
                {
                    int max = GameInfo.MaxItemsPerBox(productId);
                    string amountColor = amount == 0 ? "#FF7070" : (max > 0 && amount >= max ? "#7CFC7C" : "#FFFFFF");
                    _sb.Append("Box: <b><color=").Append(amountColor).Append('>').Append(amount).Append("</color></b>");
                    if (max > 0)
                    {
                        _sb.Append(" / ").Append(max);
                    }
                }

                if (showPrices)
                {
                    _sb.Append("\n<size=85%>Your price ").Append(GameInfo.Price(shelfPrice))
                       .Append("  <color=").Append(Muted).Append(">Market ").Append(GameInfo.Price(marketPrice)).Append("</color></size>");
                }
            }

            _lastContainer = container;
            _lastSlot = slotIndex;
            _lastProduct = productId;
            _lastAmount = amount;
            _lastShelfPrice = shelfPrice;
            _lastMarketPrice = marketPrice;
            _lastPanelVisible = context.GamePanelVisible;
            _lastShowPrices = showPrices;
            _lastText = _sb.ToString();
            text = _lastText;
            return true;
        }
    }
}
