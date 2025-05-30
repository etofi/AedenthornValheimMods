﻿// Based on code made by MarC0 / ManlyMarco
// Copyright 2018 GNU General Public License v3.0

using BepInEx.Configuration;
using ConfigurationManager.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace ConfigurationManager
{
    public class SettingFieldDrawer
    {
        public static readonly IEnumerable<KeyCode> _keysToCheck = BepInEx.Configuration.KeyboardShortcut.AllKeyCodes.Except(new[] { KeyCode.Mouse0, KeyCode.None }).ToArray();

        public static Dictionary<Type, Action<SettingEntryBase>> SettingDrawHandlers { get; }

        public static readonly Dictionary<SettingEntryBase, ComboBox> _comboBoxCache = new Dictionary<SettingEntryBase, ComboBox>();
        public static readonly Dictionary<SettingEntryBase, ColorCacheEntry> _colorCache = new Dictionary<SettingEntryBase, ColorCacheEntry>();

        public readonly BepInExPlugin _instance;

        public static SettingEntryBase _currentKeyboardShortcutToSet;
        public static bool SettingKeyboardShortcut => _currentKeyboardShortcutToSet != null;

        public static SettingFieldDrawer()
        {
            SettingDrawHandlers = new Dictionary<Type, Action<SettingEntryBase>>
            {
                {typeof(bool), DrawBoolField},
                {typeof(BepInEx.Configuration.KeyboardShortcut), DrawKeyboardShortcut},
                {typeof(KeyCode), DrawKeyboardShortcut},
                {typeof(Color), DrawColor },
                {typeof(Vector2), DrawVector2 },
                {typeof(Vector3), DrawVector3 },
                {typeof(Vector4), DrawVector4 },
                {typeof(Quaternion), DrawQuaternion },
            };
        }

        public SettingFieldDrawer(BepInExPlugin instance)
        {
            _instance = instance;
        }

        public void DrawSettingValue(SettingEntryBase setting)
        {
            GUI.backgroundColor = BepInExPlugin._widgetBackgroundColor.Value;

            if (setting.CustomDrawer != null)
                setting.CustomDrawer(setting is ConfigSettingEntry newSetting ? newSetting.Entry : null);
            else if (setting.ShowRangeAsPercent != null && setting.AcceptableValueRange.Key != null)
                DrawRangeField(setting);
            else if (setting.AcceptableValues != null)
                DrawListField(setting);
            else if (setting.SettingType.IsEnum)
            {
                if (setting.SettingType.GetCustomAttributes(typeof(FlagsAttribute), false).Any())
                    DrawFlagsField(setting, Enum.GetValues(setting.SettingType), _instance.RightColumnWidth);
                else
                    DrawComboboxField(setting, Enum.GetValues(setting.SettingType), _instance.DefaultWindowRect.yMax);
            }
            else
                DrawFieldBasedOnValueType(setting);
        }

        public static void ClearCache()
        {
            _comboBoxCache.Clear();

            foreach (var tex in _colorCache)
                UnityEngine.Object.Destroy(tex.Value.Tex);
            _colorCache.Clear();
        }

        public static void DrawCenteredLabel(string text, params GUILayoutOption[] options)
        {
            GUILayout.BeginHorizontal(options);
            GUILayout.FlexibleSpace();
            GUILayout.Label(text, BepInExPlugin.labelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        public static void DrawCategoryHeader(string text)
        {
            GUILayout.Label(text, BepInExPlugin.categoryHeaderSkin);
        }

        public static bool DrawPluginHeader(GUIContent content, bool isCollapsed)
        {

            //if (isCollapsed) content.text += "\n...";
            return GUILayout.Button(content, BepInExPlugin.pluginHeaderSkin, GUILayout.ExpandWidth(true));
        }

        public static bool DrawCurrentDropdown()
        {
            if (ComboBox.CurrentDropdownDrawer != null)
            {
                ComboBox.CurrentDropdownDrawer.Invoke();
                ComboBox.CurrentDropdownDrawer = null;
                return true;
            }
            return false;
        }

        public void DrawListField(SettingEntryBase setting)
        {
            var acceptableValues = setting.AcceptableValues;
            if (acceptableValues.Length == 0)
                throw new ArgumentException("AcceptableValueListAttribute returned an empty list of acceptable values. You need to supply at least 1 option.");

            if (!setting.SettingType.IsInstanceOfType(acceptableValues.FirstOrDefault(x => x != null)))
                throw new ArgumentException("AcceptableValueListAttribute returned a list with items of type other than the settng type itself.");

            DrawComboboxField(setting, acceptableValues, _instance.DefaultWindowRect.yMax);
        }

        public void DrawFieldBasedOnValueType(SettingEntryBase setting)
        {
            if (SettingDrawHandlers.TryGetValue(setting.SettingType, out var drawMethod))
                drawMethod(setting);
            else
                DrawUnknownField(setting, _instance.RightColumnWidth);
        }

        public static void DrawBoolField(SettingEntryBase setting)
        {
            GUI.backgroundColor = BepInExPlugin._widgetBackgroundColor.Value;
            var boolVal = (bool)setting.Get();
            var result = GUILayout.Toggle(boolVal, boolVal ? "Enabled" : "Disabled", BepInExPlugin.toggleStyle, GUILayout.ExpandWidth(true));
            if (result != boolVal)
                setting.Set(result);
        }

        public static void DrawFlagsField(SettingEntryBase setting, IList enumValues, int maxWidth)
        {
            var currentValue = Convert.ToInt64(setting.Get());
            var allValues = enumValues.Cast<Enum>().Select(x => new { name = x.ToString(), val = Convert.ToInt64(x) }).ToArray();

            // Vertically stack Horizontal groups of the options to deal with the options taking more width than is available in the window
            GUILayout.BeginVertical(GUILayout.MaxWidth(maxWidth));
            {
                for (var index = 0; index < allValues.Length;)
                {
                    GUILayout.BeginHorizontal();
                    {
                        var currentWidth = 0;
                        for (; index < allValues.Length; index++)
                        {
                            var value = allValues[index];

                            // Skip the 0 / none enum value, just uncheck everything to get 0
                            if (value.val != 0)
                            {
                                // Make sure this horizontal group doesn't extend over window width, if it does then start a new horiz group below
                                var textDimension = (int)GUI.skin.toggle.CalcSize(new GUIContent(value.name)).x;
                                currentWidth += textDimension;
                                if (currentWidth > maxWidth)
                                    break;

                                GUI.changed = false;


                                var newVal = GUILayout.Toggle((currentValue & value.val) == value.val, value.name, BepInExPlugin.toggleStyle,
                                    GUILayout.ExpandWidth(false));
                                if (GUI.changed)
                                {
                                    var newValue = newVal ? currentValue | value.val : currentValue & ~value.val;
                                    setting.Set(Enum.ToObject(setting.SettingType, newValue));
                                }
                            }
                        }
                    }
                    GUILayout.EndHorizontal();
                }

                GUI.changed = false;
            }
            GUILayout.EndVertical();
            // Make sure the reset button is properly spaced
            GUILayout.FlexibleSpace();
        }

        public static void DrawComboboxField(SettingEntryBase setting, IList list, float windowYmax)
        {
            var buttonText = ObjectToGuiContent(setting.Get());
            var dispRect = GUILayoutUtility.GetRect(buttonText, GUI.skin.button, GUILayout.ExpandWidth(true));

            if (!_comboBoxCache.TryGetValue(setting, out var box))
            {
                box = new ComboBox(dispRect, buttonText, list.Cast<object>().Select(ObjectToGuiContent).ToArray(), BepInExPlugin.boxStyle, windowYmax);
                _comboBoxCache[setting] = box;
            }
            else
            {
                box.Rect = dispRect;
                box.ButtonContent = buttonText;
            }

            box.Show(id =>
            {
                if (id >= 0 && id < list.Count)
                    setting.Set(list[id]);
            });
        }

        public static GUIContent ObjectToGuiContent(object x)
        {
            if (x is Enum)
            {
                var enumType = x.GetType();
                var enumMember = enumType.GetMember(x.ToString()).FirstOrDefault();
                var attr = enumMember?.GetCustomAttributes(typeof(DescriptionAttribute), false).Cast<DescriptionAttribute>().FirstOrDefault();
                if (attr != null)
                    return new GUIContent(attr.Description);
                return new GUIContent(x.ToString().ToProperCase());
            }
            return new GUIContent(x.ToString());
        }

        public static void DrawRangeField(SettingEntryBase setting)
        {
            var value = setting.Get();
            var converted = (float)Convert.ToDouble(value, CultureInfo.InvariantCulture);
            var leftValue = (float)Convert.ToDouble(setting.AcceptableValueRange.Key, CultureInfo.InvariantCulture);
            var rightValue = (float)Convert.ToDouble(setting.AcceptableValueRange.Value, CultureInfo.InvariantCulture);

            var result = GUILayout.HorizontalSlider(converted, leftValue, rightValue, BepInExPlugin.sliderStyle, BepInExPlugin.thumbStyle, GUILayout.ExpandWidth(true));
            if (Math.Abs(result - converted) > Mathf.Abs(rightValue - leftValue) / 1000)
            {
                var newValue = Convert.ChangeType(result, setting.SettingType, CultureInfo.InvariantCulture);
                setting.Set(newValue);
            }

            if (setting.ShowRangeAsPercent == true)
            {
                DrawCenteredLabel(
                    Mathf.Round(100 * Mathf.Abs(result - leftValue) / Mathf.Abs(rightValue - leftValue)) + "%",
                    GUILayout.Width(50));
            }
            else
            {
                var strVal = value.ToString().AppendZeroIfFloat(setting.SettingType);
                var strResult = GUILayout.TextField(strVal, GUILayout.Width(50));
                if (strResult != strVal)
                {
                    try
                    {
                        var resultVal = (float)Convert.ToDouble(strResult, CultureInfo.InvariantCulture);
                        var clampedResultVal = Mathf.Clamp(resultVal, leftValue, rightValue);
                        setting.Set(Convert.ChangeType(clampedResultVal, setting.SettingType, CultureInfo.InvariantCulture));
                    }
                    catch (FormatException)
                    {
                        // Ignore user typing in bad data
                    }
                }
            }
        }

        public void DrawUnknownField(SettingEntryBase setting, int rightColumnWidth)
        {
            // Try to use user-supplied converters
            if (setting.ObjToStr != null && setting.StrToObj != null)
            {
                var text = setting.ObjToStr(setting.Get()).AppendZeroIfFloat(setting.SettingType);
                var result = GUILayout.TextField(text, BepInExPlugin.textStyle, GUILayout.MaxWidth(rightColumnWidth));
                if (result != text)
                    setting.Set(setting.StrToObj(result));
            }
            else
            {
                // Fall back to slow/less reliable method
                var rawValue = setting.Get();
                var value = rawValue == null ? "NULL" : rawValue.ToString().AppendZeroIfFloat(setting.SettingType);
                if (CanCovert(value, setting.SettingType))
                {
                    var result = GUILayout.TextField(value, BepInExPlugin.textStyle, GUILayout.MaxWidth(rightColumnWidth));
                    if (result != value)
                        setting.Set(Convert.ChangeType(result, setting.SettingType, CultureInfo.InvariantCulture));
                }
                else
                {
                    GUILayout.TextArea(value, BepInExPlugin.textStyle, GUILayout.MaxWidth(rightColumnWidth));
                }
            }

            // When using MaxWidth the width will always be less than full window size, use this to fill this gap and push the Reset button to the right edge
            GUILayout.FlexibleSpace();
        }

        public readonly Dictionary<Type, bool> _canCovertCache = new Dictionary<Type, bool>();
        public bool CanCovert(string value, Type type)
        {
            if (_canCovertCache.ContainsKey(type))
                return _canCovertCache[type];

            try
            {
                var _ = Convert.ChangeType(value, type);
                _canCovertCache[type] = true;
                return true;
            }
            catch
            {
                _canCovertCache[type] = false;
                return false;
            }
        }

        public static void DrawKeyboardShortcut(SettingEntryBase setting)
        {
            var value = setting.Get();
            if (value.GetType() == typeof(KeyCode)){
                value = new KeyboardShortcut((KeyCode)value);
            }

            if (_currentKeyboardShortcutToSet == setting)
            {
                GUILayout.Label("Press any key combination", BepInExPlugin.labelStyle, GUILayout.ExpandWidth(true));
                GUIUtility.keyboardControl = -1;

                foreach (var key in _keysToCheck)
                {
                    if (Input.GetKeyUp(key))
                    {
                        setting.Set(new BepInEx.Configuration.KeyboardShortcut(key, _keysToCheck.Where(Input.GetKey).ToArray()));
                        _currentKeyboardShortcutToSet = null;
                        break;
                    }
                }

                if (GUILayout.Button("Cancel", BepInExPlugin.buttonStyle, GUILayout.ExpandWidth(false)))
                    _currentKeyboardShortcutToSet = null;
            }
            else
            {
                if (GUILayout.Button(value.ToString(), GUILayout.ExpandWidth(true)))
                    _currentKeyboardShortcutToSet = setting;

                if (GUILayout.Button("Clear", BepInExPlugin.buttonStyle, GUILayout.ExpandWidth(false)))
                {
                    setting.Set(BepInEx.Configuration.KeyboardShortcut.Empty);
                    _currentKeyboardShortcutToSet = null;
                }
            }
        }

        public static void DrawVector2(SettingEntryBase obj)
        {
            var setting = (Vector2)obj.Get();
            var copy = setting;
            setting.x = DrawSingleVectorSlider(setting.x, "X");
            setting.y = DrawSingleVectorSlider(setting.y, "Y");
            if (setting != copy) obj.Set(setting);
        }

        public static void DrawVector3(SettingEntryBase obj)
        {
            var setting = (Vector3)obj.Get();
            var copy = setting;
            setting.x = DrawSingleVectorSlider(setting.x, "X");
            setting.y = DrawSingleVectorSlider(setting.y, "Y");
            setting.z = DrawSingleVectorSlider(setting.z, "Z");
            if (setting != copy) obj.Set(setting);
        }

        public static void DrawVector4(SettingEntryBase obj)
        {
            var setting = (Vector4)obj.Get();
            var copy = setting;
            setting.x = DrawSingleVectorSlider(setting.x, "X");
            setting.y = DrawSingleVectorSlider(setting.y, "Y");
            setting.z = DrawSingleVectorSlider(setting.z, "Z");
            setting.w = DrawSingleVectorSlider(setting.w, "W");
            if (setting != copy) obj.Set(setting);
        }

        public static void DrawQuaternion(SettingEntryBase obj)
        {
            var setting = (Quaternion)obj.Get();
            var copy = setting;
            setting.x = DrawSingleVectorSlider(setting.x, "X");
            setting.y = DrawSingleVectorSlider(setting.y, "Y");
            setting.z = DrawSingleVectorSlider(setting.z, "Z");
            setting.w = DrawSingleVectorSlider(setting.w, "W");
            if (setting != copy) obj.Set(setting);
        }

        public static float DrawSingleVectorSlider(float setting, string label)
        {
            GUILayout.Label(label, BepInExPlugin.labelStyle, GUILayout.ExpandWidth(false));
            float.TryParse(GUILayout.TextField(setting.ToString("F", CultureInfo.InvariantCulture), GUILayout.ExpandWidth(true)), NumberStyles.Any, CultureInfo.InvariantCulture, out var x);
            return x;
        }

        public static void DrawColor(SettingEntryBase obj)
        {
            var setting = (Color)obj.Get();

            if (!_colorCache.TryGetValue(obj, out var cacheEntry))
            {
                cacheEntry = new ColorCacheEntry { Tex = new Texture2D(40, 10, TextureFormat.ARGB32, false), Last = setting };
                cacheEntry.Tex.FillTexture(setting);
                _colorCache[obj] = cacheEntry;
            }

            GUILayout.Label("R", BepInExPlugin.labelStyle, GUILayout.ExpandWidth(false));
            setting.r = GUILayout.HorizontalSlider(setting.r, 0f, 1f, GUILayout.ExpandWidth(true));
            GUILayout.Label("G", BepInExPlugin.labelStyle, GUILayout.ExpandWidth(false));
            setting.g = GUILayout.HorizontalSlider(setting.g, 0f, 1f, GUILayout.ExpandWidth(true));
            GUILayout.Label("B", BepInExPlugin.labelStyle, GUILayout.ExpandWidth(false));
            setting.b = GUILayout.HorizontalSlider(setting.b, 0f, 1f, GUILayout.ExpandWidth(true));
            GUILayout.Label("A", BepInExPlugin.labelStyle, GUILayout.ExpandWidth(false));
            setting.a = GUILayout.HorizontalSlider(setting.a, 0f, 1f, GUILayout.ExpandWidth(true));

            GUILayout.Space(4);

            if (setting != cacheEntry.Last)
            {
                obj.Set(setting);
                cacheEntry.Tex.FillTexture(setting);
                cacheEntry.Last = setting;
            }

            GUILayout.Label(cacheEntry.Tex, BepInExPlugin.labelStyle, GUILayout.ExpandWidth(false));
        }

        public sealed class ColorCacheEntry
        {
            public Color Last;
            public Texture2D Tex;
        }
    }
}