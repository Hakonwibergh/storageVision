using System;
using System.Collections.Generic;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using Rewired;
using StarterAssets;
using UnityEngine;

namespace StorageVision.Core
{
    /// <summary>
    /// In-game settings window (IMGUI), toggled with ModConfig.MenuKey (F8). While open, the cursor is freed and the
    /// game's input is paused locally (Rewired maps + FirstPersonController.allowPlayerInput) and restored on close.
    /// Draws every entry in ModConfig.MenuEntries generically by type.
    /// </summary>
    internal sealed class ConfigMenu : MonoBehaviour
    {
        private const int WindowId = 0x5356; // "SV"
        private const float OffsetRange = 800f;
        private const float WindowWidth = 580f;
        private const float WindowHeight = 820f;
        private const float ScrollHeight = 720f;
        // Content width inside the scroll view: window padding (2 x 12) and vertical scrollbar (~20) subtracted.
        private const float ContentWidth = WindowWidth - 24f - 24f;
        private const float LabelWidth = 190f;
        private const float ValueWidth = 46f;
        private const float ControlWidth = ContentWidth - LabelWidth - 8f;

        private static readonly KeyValuePair<string, Color>[] Swatches =
        {
            new KeyValuePair<string, Color>("Green", Color.green),
            new KeyValuePair<string, Color>("Yellow", Color.yellow),
            new KeyValuePair<string, Color>("Cyan", Color.cyan),
            new KeyValuePair<string, Color>("Magenta", Color.magenta),
            new KeyValuePair<string, Color>("Red", Color.red),
            new KeyValuePair<string, Color>("Orange", new Color(1f, 0.55f, 0f)),
            new KeyValuePair<string, Color>("White", Color.white)
        };

        public static bool IsOpen { get; private set; }

        private Rect _window = new Rect(40f, 40f, WindowWidth, WindowHeight);
        private Vector2 _scroll;
        private readonly StringBuilder _sb = new StringBuilder();

        // Saved state to restore on close.
        private CursorLockMode _prevLockMode;
        private bool _prevCursorVisible;
        private FirstPersonController _blockedController;
        private readonly List<ControllerMap> _mapBuffer = new List<ControllerMap>();
        private readonly List<ControllerMap> _disabledMaps = new List<ControllerMap>();
        private bool _prevSaveOnSet = true;

        private Texture2D _windowTexture;
        private GUIStyle _windowStyle, _headerStyle, _descriptionStyle, _labelStyle;

        private void Update()
        {
            try
            {
                if (ModConfig.MenuKey.Value.IsDown())
                {
                    if (IsOpen) Close(); else Open();
                }
                else if (IsOpen && UnityInput.Current.GetKeyDown(KeyCode.Escape))
                {
                    Close();
                }

                if (IsOpen)
                {
                    // The game may re-lock the cursor; keep it free while the menu is open.
                    if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
                    if (!Cursor.visible) Cursor.visible = true;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"ConfigMenu.Update failed: {e}");
                if (IsOpen) Close();
            }
        }

        private void Open()
        {
            IsOpen = true;
            _prevLockMode = Cursor.lockState;
            _prevCursorVisible = Cursor.visible;

            FirstPersonController controller = FirstPersonController.Instance;
            if (controller != null && controller.allowPlayerInput)
            {
                controller.allowPlayerInput = false;
                _blockedController = controller;
            }

            _disabledMaps.Clear();
            try
            {
                if (ReInput.isReady)
                {
                    Player player = ReInput.players.GetPlayer(0);
                    _mapBuffer.Clear();
                    player.controllers.maps.GetAllMaps(_mapBuffer);
                    foreach (ControllerMap map in _mapBuffer)
                    {
                        if (map != null && map.enabled)
                        {
                            map.enabled = false;
                            _disabledMaps.Add(map);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not pause game input for the menu: {e.Message}");
            }

            // Save once on close instead of on every slider tick.
            _prevSaveOnSet = ModConfig.File.SaveOnConfigSet;
            ModConfig.File.SaveOnConfigSet = false;
        }

        private void Close()
        {
            IsOpen = false;
            foreach (ControllerMap map in _disabledMaps)
            {
                try
                {
                    if (map != null) map.enabled = true;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Could not restore an input map: {e.Message}");
                }
            }
            _disabledMaps.Clear();

            if (_blockedController != null && !_blockedController.allowPlayerInput)
            {
                _blockedController.allowPlayerInput = true;
            }
            _blockedController = null;

            Cursor.lockState = _prevLockMode;
            Cursor.visible = _prevCursorVisible;

            try
            {
                ModConfig.File.Save();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Could not save config: {e.Message}");
            }
            ModConfig.File.SaveOnConfigSet = _prevSaveOnSet;
        }

        private void OnDestroy()
        {
            if (IsOpen) Close();
            if (_windowTexture != null) Destroy(_windowTexture);
        }

        private void OnGUI()
        {
            if (!IsOpen)
            {
                return;
            }
            try
            {
                EnsureStyles();
                float scale = Mathf.Clamp(Screen.height / 1080f, 0.75f, 2.5f);
                Matrix4x4 previous = GUI.matrix;
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
                _window = GUILayout.Window(WindowId, _window, DrawWindow, "Storage Vision  -  " + ModConfig.MenuKey.Value + " / Esc to close", _windowStyle);
                GUI.matrix = previous;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"ConfigMenu.OnGUI failed: {e}");
                Close();
            }
        }

        private void EnsureStyles()
        {
            if (_windowStyle != null)
            {
                return;
            }
            _windowTexture = new Texture2D(1, 1);
            _windowTexture.SetPixel(0, 0, new Color(0.08f, 0.08f, 0.1f, 0.96f));
            _windowTexture.Apply();
            _windowTexture.hideFlags = HideFlags.HideAndDontSave;

            _windowStyle = new GUIStyle(GUI.skin.window) { padding = new RectOffset(12, 12, 28, 12) };
            _windowStyle.normal.background = _windowTexture;
            _windowStyle.onNormal.background = _windowTexture;
            _windowStyle.fontSize = 15;
            _windowStyle.fontStyle = FontStyle.Bold;

            _headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            _headerStyle.normal.textColor = new Color(0.45f, 1f, 0.45f);

            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            _labelStyle.normal.textColor = Color.white;

            _descriptionStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 12 };
            _descriptionStyle.normal.textColor = new Color(0.68f, 0.68f, 0.7f);
            _descriptionStyle.margin = new RectOffset(4, 4, 0, 6);
        }

        private void DrawWindow(int id)
        {
            // No horizontal scrollbar: content has a fixed width that fits the window.
            _scroll = GUILayout.BeginScrollView(_scroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar,
                GUILayout.Height(ScrollHeight));
            GUILayout.BeginVertical(GUILayout.Width(ContentWidth));
            string section = null;
            foreach (ConfigEntryBase entry in ModConfig.MenuEntries)
            {
                if (entry.Definition.Section != section)
                {
                    section = entry.Definition.Section;
                    GUILayout.Space(section == ModConfig.MenuEntries[0].Definition.Section ? 0f : 10f);
                    GUILayout.Label(StripNumber(section), _headerStyle);
                }
                try
                {
                    DrawEntry(entry);
                }
                catch (Exception e)
                {
                    GUILayout.Label($"{entry.Definition.Key}: {e.Message}");
                }
            }
            GUILayout.EndVertical();
            GUILayout.EndScrollView();

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset to defaults"))
            {
                foreach (ConfigEntryBase entry in ModConfig.MenuEntries)
                {
                    entry.BoxedValue = entry.DefaultValue;
                }
            }
            if (GUILayout.Button("Close"))
            {
                Close();
            }
            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }

        private void DrawEntry(ConfigEntryBase entry)
        {
            string label = ModConfig.DisplayName(entry);
            Type type = entry.SettingType;

            if (type == typeof(bool))
            {
                bool value = (bool)entry.BoxedValue;
                bool next = GUILayout.Toggle(value, " " + label, GUILayout.Width(ContentWidth));
                if (next != value) entry.BoxedValue = next;
                DrawDescription(entry);
                return;
            }

            GUILayout.BeginHorizontal(GUILayout.Width(ContentWidth));
            GUILayout.Label(label, _labelStyle, GUILayout.Width(LabelWidth));

            if (type == typeof(float))
            {
                float value = (float)entry.BoxedValue;
                float min = -OffsetRange, max = OffsetRange;
                if (entry.Description.AcceptableValues is AcceptableValueRange<float> range)
                {
                    min = range.MinValue;
                    max = range.MaxValue;
                }
                float next = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(ControlWidth - ValueWidth));
                // Whole units for big ranges (offsets, font size), 2 decimals for small ones.
                next = max - min > 20f ? Mathf.Round(next) : Mathf.Round(next * 100f) / 100f;
                GUILayout.Label(next.ToString(max - min > 20f ? "0" : "0.00"), GUILayout.Width(ValueWidth));
                if (!Mathf.Approximately(next, value)) entry.BoxedValue = next;
            }
            else if (type == typeof(int))
            {
                int value = (int)entry.BoxedValue;
                int min = 0, max = 100;
                if (entry.Description.AcceptableValues is AcceptableValueRange<int> range)
                {
                    min = range.MinValue;
                    max = range.MaxValue;
                }
                int next = Mathf.RoundToInt(GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(ControlWidth - ValueWidth)));
                GUILayout.Label(next.ToString(), GUILayout.Width(ValueWidth));
                if (next != value) entry.BoxedValue = next;
            }
            else if (type.IsEnum)
            {
                Array values = Enum.GetValues(type);
                string[] names = new string[values.Length];
                int current = 0;
                for (int i = 0; i < values.Length; i++)
                {
                    names[i] = Nicify(values.GetValue(i).ToString());
                    if (Equals(values.GetValue(i), entry.BoxedValue)) current = i;
                }
                int next = GUILayout.Toolbar(current, names, GUILayout.Width(ControlWidth));
                if (next != current) entry.BoxedValue = values.GetValue(next);
            }
            else if (type == typeof(Color))
            {
                Color value = (Color)entry.BoxedValue;
                Color previousBg = GUI.backgroundColor;
                foreach (KeyValuePair<string, Color> swatch in Swatches)
                {
                    bool selected = ApproximatelyEqual(value, swatch.Value);
                    GUI.backgroundColor = swatch.Value;
                    if (GUILayout.Button(new GUIContent(selected ? "X" : "", swatch.Key), GUILayout.Width(30f), GUILayout.Height(20f)))
                    {
                        entry.BoxedValue = swatch.Value;
                    }
                }
                GUI.backgroundColor = previousBg;
            }
            else
            {
                GUILayout.Label(entry.GetSerializedValue());
            }
            GUILayout.EndHorizontal();
            DrawDescription(entry);
        }

        private void DrawDescription(ConfigEntryBase entry)
        {
            string description = entry.Description?.Description;
            if (!string.IsNullOrEmpty(description))
            {
                GUILayout.Label(description, _descriptionStyle, GUILayout.Width(ContentWidth));
            }
        }

        private static bool ApproximatelyEqual(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f && Mathf.Abs(a.b - b.b) < 0.02f;
        }

        private static string StripNumber(string section)
        {
            int dot = section.IndexOf(". ", StringComparison.Ordinal);
            return dot >= 0 && dot <= 2 ? section.Substring(dot + 2) : section;
        }

        /// <summary>"MoveToCenter" -> "Move To Center".</summary>
        private string Nicify(string name)
        {
            _sb.Length = 0;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
                {
                    _sb.Append(' ');
                }
                _sb.Append(c);
            }
            return _sb.ToString();
        }
    }
}
