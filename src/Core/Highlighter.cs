using System;
using HighlightPlus;
using UnityEngine;

namespace StorageVision.Core
{
    /// <summary>
    /// Outlines exactly one GameObject at a time using the game's own HighlightPlus system.
    /// If the target already has a HighlightEffect, its settings are saved and restored; otherwise a component is added
    /// and destroyed again (HighlightEffect.OnDestroy frees its materials).
    /// </summary>
    internal sealed class Highlighter
    {
        private struct SavedSettings
        {
            public bool Enabled;
            public bool Highlighted;
            public float Outline;
            public Color OutlineColor;
            public float OutlineWidth;
            public Visibility OutlineVisibility;
            public float Glow;
            public float Overlay;
            public float InnerGlow;
        }

        private GameObject _target;
        private HighlightEffect _effect;
        private bool _addedByUs;
        private SavedSettings _saved;

        private Color _appliedColor;
        private float _appliedWidth;
        private bool _appliedOnTop;

        public GameObject Current => _target;

        public void Set(GameObject target)
        {
            try
            {
                // Target destroyed (e.g. box picked up): Unity null check also covers destroyed objects.
                if (_target == null && !ReferenceEquals(_target, null))
                {
                    Forget();
                }

                if (target == _target)
                {
                    if (_effect != null)
                    {
                        ApplyStyleIfChanged(false);
                    }
                    return;
                }

                Clear();
                if (target == null)
                {
                    return;
                }

                _target = target;
                _effect = target.GetComponent<HighlightEffect>();
                if (_effect != null)
                {
                    _addedByUs = false;
                    _saved = new SavedSettings
                    {
                        Enabled = _effect.enabled,
                        Highlighted = _effect.highlighted,
                        Outline = _effect.outline,
                        OutlineColor = _effect.outlineColor,
                        OutlineWidth = _effect.outlineWidth,
                        OutlineVisibility = _effect.outlineVisibility,
                        Glow = _effect.glow,
                        Overlay = _effect.overlay,
                        InnerGlow = _effect.innerGlow
                    };
                    _effect.glow = 0f;
                    _effect.overlay = 0f;
                    _effect.innerGlow = 0f;
                }
                else
                {
                    _addedByUs = true;
                    _effect = target.AddComponent<HighlightEffect>();
                    _effect.outlineQuality = HighlightPlus.QualityLevel.High;
                }

                _effect.enabled = true;
                _effect.outline = 1f;
                ApplyStyleIfChanged(true);
                _effect.highlighted = true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Highlighter.Set failed: {e}");
                Clear();
            }
        }

        public void Clear()
        {
            try
            {
                if (_effect != null)
                {
                    if (_addedByUs)
                    {
                        _effect.highlighted = false;
                        UnityEngine.Object.Destroy(_effect);
                    }
                    else
                    {
                        _effect.outline = _saved.Outline;
                        _effect.outlineColor = _saved.OutlineColor;
                        _effect.outlineWidth = _saved.OutlineWidth;
                        _effect.outlineVisibility = _saved.OutlineVisibility;
                        _effect.glow = _saved.Glow;
                        _effect.overlay = _saved.Overlay;
                        _effect.innerGlow = _saved.InnerGlow;
                        _effect.highlighted = _saved.Highlighted;
                        _effect.enabled = _saved.Enabled;
                        _effect.UpdateMaterialProperties();
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Highlighter.Clear failed: {e}");
            }
            Forget();
        }

        private void Forget()
        {
            _target = null;
            _effect = null;
            _addedByUs = false;
        }

        private void ApplyStyleIfChanged(bool force)
        {
            Color color = ModConfig.HighlightColor.Value;
            float width = ModConfig.HighlightWidth.Value;
            bool onTop = ModConfig.HighlightAlwaysOnTop.Value;
            if (!force && color == _appliedColor && Mathf.Approximately(width, _appliedWidth) && onTop == _appliedOnTop)
            {
                return;
            }
            _effect.outlineColor = color;
            _effect.outlineWidth = width;
            _effect.outlineVisibility = onTop ? Visibility.AlwaysOnTop : Visibility.Normal;
            _effect.UpdateMaterialProperties();
            _appliedColor = color;
            _appliedWidth = width;
            _appliedOnTop = onTop;
        }
    }
}
