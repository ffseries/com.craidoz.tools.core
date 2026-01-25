using UnityEditor;
using UnityEngine;

namespace Craidoz.Tools.Core.Editor
{
    internal class PlayerPrefManagementWindow : EditorWindow
    {
        private enum ValueType
        {
            String,
            Int,
            Float,
            Bool
        }

        private string modifyKey = string.Empty;
        private string modifyString = string.Empty;
        private int modifyInt;
        private float modifyFloat;
        private bool modifyBool;
        private ValueType modifyType = ValueType.String;
        private string lastLoadedRaw = string.Empty;
        private bool hasLoadedValue;

        private string deleteKey = string.Empty;
        private string statusMessage = string.Empty;
        private MessageType statusType = MessageType.Info;

        private GUIStyle headerStyle;
        private GUIStyle sectionStyle;
        private const int BodyFontSize = 12;

        [MenuItem("CraidoZ Tools/PlayerPref Management")]
        public static void ShowWindow()
        {
            var window = GetWindow<PlayerPrefManagementWindow>("PlayerPref Management");
            window.minSize = new Vector2(640f, 360f);
        }

        private void OnEnable()
        {
            InitStyles();
        }

        private void InitStyles()
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14
            };

            sectionStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(12, 12, 10, 12)
            };
        }

        private void OnGUI()
        {
            if (headerStyle == null || sectionStyle == null)
            {
                InitStyles();
            }

            var fontSnapshot = ApplyFontSizes(BodyFontSize);
            try
            {
                EditorGUILayout.Space(8f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawModifyPanel();
                    GUILayout.Space(10f);
                    DrawDeletePanel();
                }

                if (!string.IsNullOrWhiteSpace(statusMessage))
                {
                    EditorGUILayout.Space(8f);
                    EditorGUILayout.HelpBox(statusMessage, statusType);
                }
            }
            finally
            {
                RestoreFontSizes(fontSnapshot);
            }
        }

        private void DrawModifyPanel()
        {
            using (new EditorGUILayout.VerticalScope(sectionStyle, GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("Modify PlayerPref", headerStyle);
                EditorGUILayout.Space(4f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    modifyKey = EditorGUILayout.TextField("Key", modifyKey);
                    if (GUILayout.Button("Clear Key", GUILayout.Width(90f)))
                    {
                        modifyKey = string.Empty;
                        hasLoadedValue = false;
                        lastLoadedRaw = string.Empty;
                    }
                }
                modifyType = (ValueType)EditorGUILayout.EnumPopup("Type", modifyType);

                DrawValueField();
                DrawRawPreview();

                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Set / Save", GUILayout.Height(24f)))
                    {
                        SetPref();
                    }

                    if (GUILayout.Button("Load", GUILayout.Height(24f)))
                    {
                        LoadPref();
                    }
                }

                EditorGUILayout.Space(6f);
                EditorGUILayout.HelpBox("PlayerPrefs does not store type metadata. Loading a key with a different type may coerce or default values. Bool values are stored as 0/1.", MessageType.None);
            }
        }

        private void DrawValueField()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                switch (modifyType)
                {
                    case ValueType.String:
                        modifyString = EditorGUILayout.TextField("Value", modifyString);
                        break;
                    case ValueType.Int:
                        modifyInt = EditorGUILayout.IntField("Value", modifyInt);
                        break;
                    case ValueType.Float:
                        modifyFloat = EditorGUILayout.FloatField("Value", modifyFloat);
                        break;
                    case ValueType.Bool:
                        modifyBool = EditorGUILayout.Toggle("Value", modifyBool);
                        break;
                }

                if (GUILayout.Button("Clear Value", GUILayout.Width(90f)))
                {
                    ClearModifyValue();
                }
            }
        }

        private void DrawDeletePanel()
        {
            using (new EditorGUILayout.VerticalScope(sectionStyle, GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("Delete PlayerPref", headerStyle);
                EditorGUILayout.Space(4f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    deleteKey = EditorGUILayout.TextField("Key", deleteKey);
                    if (GUILayout.Button("Clear Key", GUILayout.Width(90f)))
                    {
                        deleteKey = string.Empty;
                    }
                }

                EditorGUILayout.Space(8f);
                if (GUILayout.Button("Delete Key", GUILayout.Height(24f)))
                {
                    DeleteKey();
                }

                EditorGUILayout.Space(12f);
                EditorGUILayout.LabelField("Danger Zone", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Delete All removes every PlayerPref for the current project/user.", MessageType.Warning);

                var originalColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.92f, 0.42f, 0.42f);
                if (GUILayout.Button("Delete All", GUILayout.Height(28f)))
                {
                    DeleteAllKeys();
                }
                GUI.backgroundColor = originalColor;
            }
        }

        private void SetPref()
        {
            if (string.IsNullOrWhiteSpace(modifyKey))
            {
                SetStatus("Please provide a key before saving.", MessageType.Warning);
                return;
            }

            switch (modifyType)
            {
                case ValueType.String:
                    PlayerPrefs.SetString(modifyKey, modifyString ?? string.Empty);
                    break;
                case ValueType.Int:
                    PlayerPrefs.SetInt(modifyKey, modifyInt);
                    break;
                case ValueType.Float:
                    PlayerPrefs.SetFloat(modifyKey, modifyFloat);
                    break;
                case ValueType.Bool:
                    PlayerPrefs.SetInt(modifyKey, modifyBool ? 1 : 0);
                    break;
            }

            PlayerPrefs.Save();
            lastLoadedRaw = GetSavedValueString(modifyKey, modifyType);
            hasLoadedValue = true;
            SetStatus($"Saved '{modifyKey}' as {modifyType}. Value: {FormatValueForStatus()}", MessageType.Info);
        }

        private void LoadPref()
        {
            if (string.IsNullOrWhiteSpace(modifyKey))
            {
                SetStatus("Please provide a key before loading.", MessageType.Warning);
                return;
            }

            if (!PlayerPrefs.HasKey(modifyKey))
            {
                hasLoadedValue = false;
                lastLoadedRaw = string.Empty;
                SetStatus($"Key '{modifyKey}' not found.", MessageType.Info);
                return;
            }

            lastLoadedRaw = GetSavedValueString(modifyKey, modifyType);
            hasLoadedValue = true;

            switch (modifyType)
            {
                case ValueType.String:
                    modifyString = PlayerPrefs.GetString(modifyKey);
                    break;
                case ValueType.Int:
                    modifyInt = PlayerPrefs.GetInt(modifyKey);
                    break;
                case ValueType.Float:
                    modifyFloat = PlayerPrefs.GetFloat(modifyKey);
                    break;
                case ValueType.Bool:
                    modifyBool = PlayerPrefs.GetInt(modifyKey) != 0;
                    break;
            }

            var detectedType = DetectStoredType(modifyKey);
            if (IsTypeMismatch(modifyType, detectedType))
            {
                SetStatus(
                    $"(!) Type mismatch. Selected: {modifyType}. Detected: {detectedType}. Saved Value: {lastLoadedRaw}",
                    MessageType.Warning);
                return;
            }

            SetStatus($"Loaded '{modifyKey}'. Value: {FormatValueForStatus()}", MessageType.Info);
        }

        private void DeleteKey()
        {
            if (string.IsNullOrWhiteSpace(deleteKey))
            {
                SetStatus("Please provide a key before deleting.", MessageType.Warning);
                return;
            }

            if (!PlayerPrefs.HasKey(deleteKey))
            {
                SetStatus($"Key '{deleteKey}' not found.", MessageType.Info);
                return;
            }

            PlayerPrefs.DeleteKey(deleteKey);
            PlayerPrefs.Save();
            if (deleteKey == modifyKey)
            {
                hasLoadedValue = false;
                lastLoadedRaw = string.Empty;
            }
            SetStatus($"Deleted '{deleteKey}'.", MessageType.Info);
        }

        private void DeleteAllKeys()
        {
            if (!EditorUtility.DisplayDialog(
                    "Delete All PlayerPrefs",
                    "This will delete every PlayerPref for the current project/user.\nAre you sure?",
                    "Delete All",
                    "Cancel"))
            {
                return;
            }

            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            hasLoadedValue = false;
            lastLoadedRaw = string.Empty;
            SetStatus("Deleted all PlayerPrefs.", MessageType.Warning);
        }

        private void SetStatus(string message, MessageType type)
        {
            statusMessage = message;
            statusType = type;
            Repaint();
        }

        private void DrawRawPreview()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Saved Value", hasLoadedValue ? lastLoadedRaw : string.Empty);
            }
        }

        private string FormatValueForStatus()
        {
            switch (modifyType)
            {
                case ValueType.String:
                    return modifyString ?? string.Empty;
                case ValueType.Int:
                    return modifyInt.ToString();
                case ValueType.Float:
                    return modifyFloat.ToString();
                case ValueType.Bool:
                    return modifyBool ? "true" : "false";
                default:
                    return string.Empty;
            }
        }

        private void ClearModifyValue()
        {
            switch (modifyType)
            {
                case ValueType.String:
                    modifyString = string.Empty;
                    break;
                case ValueType.Int:
                    modifyInt = 0;
                    break;
                case ValueType.Float:
                    modifyFloat = 0f;
                    break;
                case ValueType.Bool:
                    modifyBool = false;
                    break;
            }
        }

        private static string GetSavedValueString(string key, ValueType type)
        {
            switch (type)
            {
                case ValueType.String:
                    return PlayerPrefs.GetString(key, string.Empty);
                case ValueType.Int:
                    return PlayerPrefs.GetInt(key, 0).ToString();
                case ValueType.Float:
                    return PlayerPrefs.GetFloat(key, 0f).ToString("0.#####");
                case ValueType.Bool:
                    return PlayerPrefs.GetInt(key, 0) == 0 ? "false" : "true";
                default:
                    return string.Empty;
            }
        }

        private static string DetectStoredType(string key)
        {
            const string sentinel = "__CRAIDOZ_SENTINEL__";
            var stringValue = PlayerPrefs.GetString(key, sentinel);
            if (stringValue != sentinel)
            {
                return "String";
            }

            var floatValue = PlayerPrefs.GetFloat(key, float.NaN);
            if (!float.IsNaN(floatValue))
            {
                return "Float";
            }

            var intValue = PlayerPrefs.GetInt(key, int.MinValue);
            if (intValue != int.MinValue)
            {
                return intValue == 0 || intValue == 1 ? "Bool" : "Int";
            }

            return "Unknown";
        }

        private static bool IsTypeMismatch(ValueType selectedType, string detectedType)
        {
            switch (selectedType)
            {
                case ValueType.String:
                    return detectedType != "String";
                case ValueType.Int:
                    return detectedType != "Int" && detectedType != "Bool";
                case ValueType.Float:
                    return detectedType != "Float";
                case ValueType.Bool:
                    return detectedType != "Bool";
                default:
                    return false;
            }
        }

        private struct FontSizeSnapshot
        {
            public int Label;
            public int TextField;
            public int NumberField;
            public int Popup;
            public int Toggle;
            public int HelpBox;
            public int BoldLabel;
        }

        private FontSizeSnapshot ApplyFontSizes(int size)
        {
            var snapshot = new FontSizeSnapshot
            {
                Label = EditorStyles.label.fontSize,
                TextField = EditorStyles.textField.fontSize,
                NumberField = EditorStyles.numberField.fontSize,
                Popup = EditorStyles.popup.fontSize,
                Toggle = EditorStyles.toggle.fontSize,
                HelpBox = EditorStyles.helpBox.fontSize,
                BoldLabel = EditorStyles.boldLabel.fontSize
            };

            EditorStyles.label.fontSize = size;
            EditorStyles.textField.fontSize = size;
            EditorStyles.numberField.fontSize = size;
            EditorStyles.popup.fontSize = size;
            EditorStyles.toggle.fontSize = size;
            EditorStyles.helpBox.fontSize = size;
            EditorStyles.boldLabel.fontSize = size;

            return snapshot;
        }

        private void RestoreFontSizes(FontSizeSnapshot snapshot)
        {
            EditorStyles.label.fontSize = snapshot.Label;
            EditorStyles.textField.fontSize = snapshot.TextField;
            EditorStyles.numberField.fontSize = snapshot.NumberField;
            EditorStyles.popup.fontSize = snapshot.Popup;
            EditorStyles.toggle.fontSize = snapshot.Toggle;
            EditorStyles.helpBox.fontSize = snapshot.HelpBox;
            EditorStyles.boldLabel.fontSize = snapshot.BoldLabel;
        }
    }
}
