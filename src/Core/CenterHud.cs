using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StorageVision.Core
{
    /// <summary>
    /// One shared text box under the crosshair, plus a dark backdrop that also covers the game's centered product
    /// panel. Lives under the game's GameCanvas so it uses the game's canvas scaling.
    /// Recreated automatically when the game canvas is replaced (scene load).
    /// </summary>
    internal sealed class CenterHud
    {
        private Transform _canvasRoot;
        private GameObject _root;
        private RectTransform _rootRect;
        private Image _background;
        private VerticalLayoutGroup _layout;
        private TextMeshProUGUI _text;
        private string _currentText;

        private GameObject _backdrop;
        private RectTransform _backdropRect;
        private Image _backdropImage;

        /// <param name="text">Text to show, or null for none.</param>
        /// <param name="topY">Top edge of the text box, relative to the screen center.</param>
        /// <param name="gamePanel">The game's centered panel (root) to put behind the shared backdrop, or null.</param>
        /// <param name="gamePanelBounds">Bounds of that panel, relative to the screen center.</param>
        public void Show(Transform gameCanvas, string text, float topY, Transform gamePanel, Rect gamePanelBounds)
        {
            try
            {
                bool hasText = !string.IsNullOrEmpty(text);
                if (!hasText && gamePanel == null)
                {
                    Hide();
                    return;
                }
                if (!EnsureCreated(gameCanvas))
                {
                    return;
                }

                float opacity = ModConfig.HudBackgroundOpacity.Value;
                bool useBackdrop = gamePanel != null && opacity > 0.001f;

                if (hasText)
                {
                    if (text != _currentText)
                    {
                        _text.text = text;
                        _currentText = text;
                    }
                    ApplyStyle(useBackdrop ? 0f : opacity);
                    // With the shared backdrop, the backdrop provides the padding; standalone, the text box does.
                    ApplyPadding(useBackdrop ? 0f : ModConfig.HudBackgroundPadding.Value);

                    Vector2 position = new Vector2(ModConfig.HudOffsetX.Value, topY);
                    if (_rootRect.anchoredPosition != position)
                    {
                        _rootRect.anchoredPosition = position;
                    }
                    SetActive(_root, true);
                    if (_root.transform.GetSiblingIndex() != _root.transform.parent.childCount - 1)
                    {
                        _root.transform.SetAsLastSibling();
                    }
                }
                else
                {
                    SetActive(_root, false);
                }

                if (useBackdrop)
                {
                    Rect area = gamePanelBounds;
                    if (hasText)
                    {
                        area = Union(area, TextBounds());
                    }
                    PlaceBackdrop(area, opacity, gamePanel);
                }
                else
                {
                    SetActive(_backdrop, false);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"CenterHud.Show failed: {e}");
                Destroy();
            }
        }

        public void Hide()
        {
            SetActive(_root, false);
            SetActive(_backdrop, false);
        }

        public void Destroy()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
            }
            if (_backdrop != null)
            {
                UnityEngine.Object.Destroy(_backdrop);
            }
            _root = null;
            _backdrop = null;
            _currentText = null;
        }

        /// <summary>Current text box rect relative to the screen center (size is from the last layout pass).</summary>
        private Rect TextBounds()
        {
            Vector2 size = _rootRect.rect.size;
            Vector2 pos = _rootRect.anchoredPosition;
            return new Rect(pos.x - size.x * 0.5f, pos.y - size.y, size.x, size.y);
        }

        private void ApplyPadding(float padding)
        {
            int p = Mathf.RoundToInt(padding);
            RectOffset current = _layout.padding;
            if (current.left != p || current.top != p)
            {
                _layout.padding = new RectOffset(p, p, p, p);
            }
        }

        private void PlaceBackdrop(Rect area, float opacity, Transform gamePanel)
        {
            float pad = ModConfig.HudBackgroundPadding.Value;
            area = Rect.MinMaxRect(area.xMin - pad, area.yMin - pad, area.xMax + pad, area.yMax + pad);
            if (_backdropRect.anchoredPosition != area.center)
            {
                _backdropRect.anchoredPosition = area.center;
            }
            if (_backdropRect.sizeDelta != area.size)
            {
                _backdropRect.sizeDelta = area.size;
            }
            Color color = new Color(0f, 0f, 0f, opacity);
            if (_backdropImage.color != color)
            {
                _backdropImage.color = color;
            }

            // Draw behind the game's panel: siblings render in order, so sit right before it.
            if (gamePanel.parent == _backdrop.transform.parent)
            {
                int panelIndex = gamePanel.GetSiblingIndex();
                if (_backdrop.transform.GetSiblingIndex() > panelIndex)
                {
                    _backdrop.transform.SetSiblingIndex(panelIndex);
                }
            }
            SetActive(_backdrop, true);
        }

        private bool EnsureCreated(Transform gameCanvas)
        {
            if (_root != null && _backdrop != null && _canvasRoot == gameCanvas)
            {
                return true;
            }
            Destroy();
            if (gameCanvas == null)
            {
                return false;
            }
            _canvasRoot = gameCanvas;

            _root = new GameObject("StorageVision_CenterHud", typeof(RectTransform));
            _root.layer = gameCanvas.gameObject.layer;
            _rootRect = (RectTransform)_root.transform;
            _rootRect.SetParent(gameCanvas, false);
            _rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            _rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            _rootRect.pivot = new Vector2(0.5f, 1f);

            _background = _root.AddComponent<Image>();
            _background.raycastTarget = false;

            _layout = _root.AddComponent<VerticalLayoutGroup>();
            _layout.padding = new RectOffset(0, 0, 0, 0);
            _layout.childAlignment = TextAnchor.UpperCenter;
            _layout.childControlWidth = true;
            _layout.childControlHeight = true;
            _layout.childForceExpandWidth = false;
            _layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = _root.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.layer = _root.layer;
            textObject.transform.SetParent(_rootRect, false);
            _text = textObject.AddComponent<TextMeshProUGUI>();
            _text.raycastTarget = false;
            _text.richText = true;
            _text.alignment = TextAlignmentOptions.Center;
            _text.textWrappingMode = TextWrappingModes.NoWrap;

            // Reuse the game's font so the HUD matches the rest of the UI.
            TMP_Text gameText = gameCanvas.Find("ProductShow/Container/ProductName")?.GetComponent<TMP_Text>();
            if (gameText != null && gameText.font != null)
            {
                _text.font = gameText.font;
            }

            _backdrop = new GameObject("StorageVision_Backdrop", typeof(RectTransform));
            _backdrop.layer = gameCanvas.gameObject.layer;
            _backdropRect = (RectTransform)_backdrop.transform;
            _backdropRect.SetParent(gameCanvas, false);
            _backdropRect.anchorMin = new Vector2(0.5f, 0.5f);
            _backdropRect.anchorMax = new Vector2(0.5f, 0.5f);
            _backdropRect.pivot = new Vector2(0.5f, 0.5f);
            _backdropImage = _backdrop.AddComponent<Image>();
            _backdropImage.raycastTarget = false;

            _currentText = null;
            _root.SetActive(false);
            _backdrop.SetActive(false);
            return true;
        }

        private void ApplyStyle(float backgroundOpacity)
        {
            float size = ModConfig.HudFontSize.Value;
            if (!Mathf.Approximately(_text.fontSize, size))
            {
                _text.fontSize = size;
            }
            Color textColor = ModConfig.HudTextColor.Value;
            if (_text.color != textColor)
            {
                _text.color = textColor;
            }
            Color bg = new Color(0f, 0f, 0f, backgroundOpacity);
            if (_background.color != bg)
            {
                _background.color = bg;
            }
            bool showBg = bg.a > 0.001f;
            if (_background.enabled != showBg)
            {
                _background.enabled = showBg;
            }
        }

        private static Rect Union(Rect a, Rect b)
        {
            return Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
                Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
            {
                go.SetActive(active);
            }
        }
    }
}
