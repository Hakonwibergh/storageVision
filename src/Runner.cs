using System;
using System.Collections.Generic;
using StorageVision.Core;
using StorageVision.Handlers;
using UnityEngine;

namespace StorageVision
{
    /// <summary>
    /// Per-frame driver: look-at -> handlers -> center HUD + highlight. Runs in LateUpdate, after the game's own
    /// Update has refreshed its panel, shelves and boxes.
    /// </summary>
    internal sealed class Runner : MonoBehaviour
    {
        private const int MaxLoggedErrors = 10;

        private readonly LookAtService _lookAt = new LookAtService();
        private readonly CenterHud _hud = new CenterHud();
        private readonly GamePanelController _gamePanel = new GamePanelController();
        private readonly Highlighter _highlighter = new Highlighter();
        private readonly OrderTargetFinder _orderTarget = new OrderTargetFinder();
        private readonly List<ILookHandler> _handlers = new List<ILookHandler>
        {
            new StorageShelfHandler(),
            new WorkTableHandler()
        };

        private int _loggedErrors;

        private void LateUpdate()
        {
            try
            {
                Tick();
            }
            catch (Exception e)
            {
                LogError("Runner", e);
                _hud.Hide();
                _highlighter.Clear();
            }
        }

        private void Tick()
        {
            Transform gameCanvas = GameCanvas.Instance != null ? GameCanvas.Instance.transform : null;
            _gamePanel.LateUpdate(gameCanvas);

            LookContext context = gameCanvas != null ? _lookAt.Update() : null;
            if (context == null)
            {
                _hud.Hide();
                _highlighter.Clear();
                return;
            }
            context.GamePanelVisible = _gamePanel.IsCenteredAndVisible;

            string text = null;
            GameObject lookTarget = null;
            foreach (ILookHandler handler in _handlers)
            {
                if (!handler.Enabled)
                {
                    continue;
                }
                try
                {
                    if (handler.TryHandle(context, out text, out lookTarget))
                    {
                        break;
                    }
                }
                catch (Exception e)
                {
                    LogError(handler.Name + " handler", e);
                    text = null;
                    lookTarget = null;
                }
            }

            if (ModConfig.HudEnabled.Value)
            {
                bool panel = _gamePanel.IsCenteredAndVisible;
                float top = panel ? _gamePanel.CenteredBottomY : ModConfig.HudOffsetY.Value;
                _hud.Show(gameCanvas, text, top, panel ? _gamePanel.PanelRoot.transform : null, _gamePanel.CenteredBounds);
            }
            else
            {
                _hud.Hide();
            }

            _highlighter.Set(ChooseHighlight(context, lookTarget));
        }

        private GameObject ChooseHighlight(LookContext context, GameObject lookTarget)
        {
            if (!ModConfig.HighlightEnabled.Value)
            {
                return null;
            }
            switch (ModConfig.HighlightModeEntry.Value)
            {
                case HighlightMode.OrderTarget:
                    return _orderTarget.Find(context.LocalPlayer, context.Camera.transform.position);
                case HighlightMode.Both:
                    // While picking an order, only the box the order needs is outlined.
                    GameObject orderTarget = _orderTarget.Find(context.LocalPlayer, context.Camera.transform.position);
                    return orderTarget != null ? orderTarget : lookTarget;
                default:
                    return lookTarget;
            }
        }

        private void OnDestroy()
        {
            _highlighter.Clear();
            _hud.Destroy();
        }

        private void LogError(string where, Exception e)
        {
            if (_loggedErrors >= MaxLoggedErrors)
            {
                return;
            }
            _loggedErrors++;
            Plugin.Log.LogError($"{where} failed: {e}" + (_loggedErrors == MaxLoggedErrors ? " (further errors suppressed)" : ""));
        }
    }
}
