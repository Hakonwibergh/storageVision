using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StorageVision.Core
{
    /// <summary>
    /// Moves or hides the game's own product panel (GameCanvas/ProductShow, shown top right by PlayerNetwork.Update).
    /// The game keeps toggling the panel's active state; we only change its position/scale, the size of its
    /// name/brand text, and a CanvasGroup alpha. Everything is restored in Keep mode.
    /// </summary>
    internal sealed class GamePanelController
    {
        private const float Spacing = 6f;

        /// <summary>Remembers a TMP text's original sizing so it can be scaled and restored.</summary>
        private sealed class ScaledText
        {
            public TMP_Text Text;
            public float FontSize, FontSizeMin, FontSizeMax;
            public TextWrappingModes Wrapping;
            public TextOverflowModes Overflow;
            public float AppliedScale = 1f;
        }

        private Transform _canvasRoot;
        private GameObject _panelRoot;
        private RectTransform _panelRect;
        private CanvasGroup _canvasGroup;
        private bool _loggedLayout;
        private readonly List<ScaledText> _texts = new List<ScaledText>();
        private readonly List<Graphic> _graphics = new List<Graphic>();
        private readonly Vector3[] _corners = new Vector3[4];

        private Vector2 _origAnchorMin, _origAnchorMax, _origPivot, _origPosition;
        private Vector3 _origScale;
        private float _origAlpha = 1f;

        /// <summary>True when the panel is visible in the center this frame.</summary>
        public bool IsCenteredAndVisible { get; private set; }

        /// <summary>Bottom edge (canvas units, relative to screen center) of the centered panel, incl. spacing.</summary>
        public float CenteredBottomY { get; private set; }

        /// <summary>Visible bounds of the centered panel (canvas units, relative to screen center).</summary>
        public Rect CenteredBounds { get; private set; }

        public GameObject PanelRoot => _panelRoot;

        public void LateUpdate(Transform gameCanvas)
        {
            IsCenteredAndVisible = false;
            try
            {
                if (gameCanvas == null)
                {
                    return;
                }
                if (_panelRoot == null || _canvasRoot != gameCanvas)
                {
                    if (!Find(gameCanvas))
                    {
                        return;
                    }
                }

                OriginalPanelMode mode = ModConfig.HudEnabled.Value ? ModConfig.OriginalPanel.Value : OriginalPanelMode.Keep;
                switch (mode)
                {
                    case OriginalPanelMode.Hide:
                        RestoreLayout();
                        ScaleTexts(1f);
                        SetAlpha(0f);
                        break;
                    case OriginalPanelMode.MoveToCenter:
                        SetAlpha(_origAlpha);
                        ApplyCenterLayout();
                        ScaleTexts(ModConfig.ProductTextScale.Value);
                        if (_panelRoot.activeInHierarchy)
                        {
                            IsCenteredAndVisible = true;
                            CenteredBounds = MeasureBounds(gameCanvas);
                            CenteredBottomY = CenteredBounds.yMin - Spacing;
                        }
                        break;
                    default:
                        RestoreLayout();
                        ScaleTexts(1f);
                        SetAlpha(_origAlpha);
                        break;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"GamePanelController failed, leaving panel as is: {e}");
                _panelRoot = null;
            }
        }

        private bool Find(Transform gameCanvas)
        {
            _canvasRoot = gameCanvas;
            Transform show = gameCanvas.Find("ProductShow");
            if (show == null)
            {
                return false;
            }
            _panelRoot = show.gameObject;

            // If ProductShow is a stretched full-screen holder, the visible box is its "Container" child.
            RectTransform rootRect = show as RectTransform;
            RectTransform target = rootRect;
            if (rootRect != null && rootRect.anchorMin != rootRect.anchorMax)
            {
                RectTransform container = show.Find("Container") as RectTransform;
                if (container != null)
                {
                    target = container;
                }
            }
            if (target == null)
            {
                _panelRoot = null;
                return false;
            }
            _panelRect = target;
            _origAnchorMin = target.anchorMin;
            _origAnchorMax = target.anchorMax;
            _origPivot = target.pivot;
            _origPosition = target.anchoredPosition;
            _origScale = target.localScale;

            _canvasGroup = show.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = show.gameObject.AddComponent<CanvasGroup>();
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
                _origAlpha = 1f;
            }
            else
            {
                _origAlpha = _canvasGroup.alpha;
            }

            _texts.Clear();
            AddScaledText(show, "Container/ProductName");
            AddScaledText(show, "Container/ProductBrand");

            if (!_loggedLayout)
            {
                _loggedLayout = true;
                Plugin.Log.LogInfo($"Found game product panel '{target.name}': anchors {_origAnchorMin}-{_origAnchorMax}, " +
                                   $"pivot {_origPivot}, pos {_origPosition}, size {target.rect.size}, scalable texts {_texts.Count}");
            }
            return true;
        }

        private void AddScaledText(Transform root, string path)
        {
            TMP_Text text = root.Find(path)?.GetComponent<TMP_Text>();
            if (text == null)
            {
                return;
            }
            _texts.Add(new ScaledText
            {
                Text = text,
                FontSize = text.fontSize,
                FontSizeMin = text.fontSizeMin,
                FontSizeMax = text.fontSizeMax,
                Wrapping = text.textWrappingMode,
                Overflow = text.overflowMode
            });
        }

        private void ScaleTexts(float scale)
        {
            foreach (ScaledText s in _texts)
            {
                if (s.Text == null || Mathf.Approximately(s.AppliedScale, scale))
                {
                    continue;
                }
                s.Text.fontSize = s.FontSize * scale;
                s.Text.fontSizeMin = s.FontSizeMin * scale;
                s.Text.fontSizeMax = s.FontSizeMax * scale;
                bool original = Mathf.Approximately(scale, 1f);
                // Larger text must not wrap or get cut off by its rect.
                s.Text.textWrappingMode = original ? s.Wrapping : TextWrappingModes.NoWrap;
                s.Text.overflowMode = original ? s.Overflow : TextOverflowModes.Overflow;
                s.AppliedScale = scale;
            }
        }

        /// <summary>
        /// Bounds of what is actually drawn: enabled, visible graphics only, and the real text extents for TMP texts
        /// (their rects are much wider than the text). Relative to the canvas center, i.e. the same space as a
        /// center-anchored anchoredPosition.
        /// </summary>
        private Rect MeasureBounds(Transform gameCanvas)
        {
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            bool any = false;

            _graphics.Clear();
            _panelRoot.GetComponentsInChildren(false, _graphics);
            foreach (Graphic g in _graphics)
            {
                if (!g.enabled || g.color.a <= 0.01f)
                {
                    continue;
                }
                RectTransform rt = g.rectTransform;
                if (g is TMP_Text text)
                {
                    if (string.IsNullOrEmpty(text.text))
                    {
                        continue;
                    }
                    Bounds tb = text.textBounds;
                    if (tb.size.x <= 0f || tb.size.y <= 0f)
                    {
                        continue;
                    }
                    _corners[0] = rt.TransformPoint(new Vector3(tb.min.x, tb.min.y));
                    _corners[1] = rt.TransformPoint(new Vector3(tb.max.x, tb.max.y));
                    Encapsulate(gameCanvas, 2, ref xMin, ref yMin, ref xMax, ref yMax);
                }
                else
                {
                    rt.GetWorldCorners(_corners);
                    Encapsulate(gameCanvas, 4, ref xMin, ref yMin, ref xMax, ref yMax);
                }
                any = true;
            }

            if (!any)
            {
                Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(gameCanvas, _panelRoot.transform);
                xMin = b.min.x; yMin = b.min.y; xMax = b.max.x; yMax = b.max.y;
            }

            Vector2 center = gameCanvas is RectTransform canvasRect ? canvasRect.rect.center : Vector2.zero;
            return Rect.MinMaxRect(xMin - center.x, yMin - center.y, xMax - center.x, yMax - center.y);
        }

        private void Encapsulate(Transform gameCanvas, int count, ref float xMin, ref float yMin, ref float xMax, ref float yMax)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 p = gameCanvas.InverseTransformPoint(_corners[i]);
                if (p.x < xMin) xMin = p.x;
                if (p.y < yMin) yMin = p.y;
                if (p.x > xMax) xMax = p.x;
                if (p.y > yMax) yMax = p.y;
            }
        }

        private void ApplyCenterLayout()
        {
            Vector2 center = new Vector2(0.5f, 0.5f);
            Vector2 pivot = new Vector2(0.5f, 1f);
            Vector2 position = new Vector2(ModConfig.HudOffsetX.Value, ModConfig.HudOffsetY.Value);
            float s = ModConfig.OriginalPanelScale.Value;
            Vector3 scale = new Vector3(_origScale.x * s, _origScale.y * s, _origScale.z);
            SetLayout(center, center, pivot, position, scale);
        }

        private void RestoreLayout()
        {
            SetLayout(_origAnchorMin, _origAnchorMax, _origPivot, _origPosition, _origScale);
        }

        // Only write when different, so the canvas is not rebuilt every frame.
        private void SetLayout(Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector3 scale)
        {
            if (_panelRect.anchorMin != anchorMin) _panelRect.anchorMin = anchorMin;
            if (_panelRect.anchorMax != anchorMax) _panelRect.anchorMax = anchorMax;
            if (_panelRect.pivot != pivot) _panelRect.pivot = pivot;
            if (_panelRect.anchoredPosition != position) _panelRect.anchoredPosition = position;
            if (_panelRect.localScale != scale) _panelRect.localScale = scale;
        }

        private void SetAlpha(float alpha)
        {
            if (_canvasGroup != null && !Mathf.Approximately(_canvasGroup.alpha, alpha))
            {
                _canvasGroup.alpha = alpha;
            }
        }
    }
}
