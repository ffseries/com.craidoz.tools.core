using System;
using Craidoz.Tools.CodeAssist;
using UnityEditor;
using UnityEngine;

namespace Craidoz.Tools.CodeAssist.Editor
{
    [CustomPropertyDrawer(typeof(ShowIfAttribute), true)]
    public sealed class ShowIfDrawer : PropertyDrawer
    {
        private const float HelpBoxHeight = 36f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var showIf = (ShowIfAttribute)attribute;
            if (string.IsNullOrWhiteSpace(showIf.ComparedPropertyName))
            {
                return GetErrorHeight(property, label);
            }

            var compared = FindComparedProperty(property, showIf.ComparedPropertyName);
            if (compared == null)
            {
                return GetErrorHeight(property, label);
            }

            if (!IsConditionMet(compared, showIf, out var error))
            {
                return string.IsNullOrEmpty(error)
                    ? 0f
                    : GetErrorHeight(property, label);
            }

            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var showIf = (ShowIfAttribute)attribute;
            if (string.IsNullOrWhiteSpace(showIf.ComparedPropertyName))
            {
                DrawError(position, property, label, "ShowIf: Compared property name is empty.");
                return;
            }

            var compared = FindComparedProperty(property, showIf.ComparedPropertyName);
            if (compared == null)
            {
                DrawError(position, property, label, $"ShowIf: Could not find '{showIf.ComparedPropertyName}'.");
                return;
            }

            if (!IsConditionMet(compared, showIf, out var error))
            {
                if (!string.IsNullOrEmpty(error))
                {
                    DrawError(position, property, label, error);
                }
                return;
            }

            EditorGUI.PropertyField(position, property, label, true);
        }

        private static SerializedProperty FindComparedProperty(SerializedProperty property, string comparedPropertyName)
        {
            var compared = property.serializedObject.FindProperty(comparedPropertyName);
            if (compared != null)
            {
                return compared;
            }

            var path = property.propertyPath;
            var lastDot = path.LastIndexOf('.');
            if (lastDot < 0)
            {
                return null;
            }

            var siblingPath = $"{path.Substring(0, lastDot + 1)}{comparedPropertyName}";
            return property.serializedObject.FindProperty(siblingPath);
        }

        private static bool IsConditionMet(
            SerializedProperty compared,
            ShowIfAttribute showIf,
            out string error)
        {
            error = string.Empty;
            switch (showIf.ValueType)
            {
                case ShowIfValueType.Bool:
                    if (compared.propertyType != SerializedPropertyType.Boolean)
                    {
                        error = $"ShowIf: Expected bool on '{compared.name}'.";
                        return false;
                    }
                    return EvaluateBool(compared.boolValue, showIf.Comparison, showIf.ExpectedBool, out error);
                case ShowIfValueType.Int:
                    if (compared.propertyType == SerializedPropertyType.Enum)
                    {
                        if (showIf.ExpectedInts == null || showIf.ExpectedInts.Length == 0)
                        {
                            error = $"ShowIf: No expected values provided for '{compared.name}'.";
                            return false;
                        }

                        return EvaluateInt(compared.enumValueIndex, showIf.Comparison, showIf.ExpectedInts, out error);
                    }
                    if (compared.propertyType != SerializedPropertyType.Integer)
                    {
                        error = $"ShowIf: Expected int on '{compared.name}'.";
                        return false;
                    }
                    if (showIf.ExpectedInts == null || showIf.ExpectedInts.Length == 0)
                    {
                        error = $"ShowIf: No expected values provided for '{compared.name}'.";
                        return false;
                    }
                    return EvaluateInt(compared.intValue, showIf.Comparison, showIf.ExpectedInts, out error);
                case ShowIfValueType.Float:
                    if (compared.propertyType != SerializedPropertyType.Float)
                    {
                        error = $"ShowIf: Expected float on '{compared.name}'.";
                        return false;
                    }
                    if (showIf.ExpectedFloats == null || showIf.ExpectedFloats.Length == 0)
                    {
                        error = $"ShowIf: No expected values provided for '{compared.name}'.";
                        return false;
                    }
                    return EvaluateFloat(compared.floatValue, showIf.Comparison, showIf.ExpectedFloats, out error);
                case ShowIfValueType.String:
                {
                    if (compared.propertyType == SerializedPropertyType.String)
                    {
                        if (showIf.ExpectedStrings == null || showIf.ExpectedStrings.Length == 0)
                        {
                            error = $"ShowIf: No expected values provided for '{compared.name}'.";
                            return false;
                        }
                        return EvaluateString(compared.stringValue, showIf.Comparison, showIf.ExpectedStrings, out error);
                    }

                    if (compared.propertyType == SerializedPropertyType.Enum)
                    {
                        if (showIf.ExpectedStrings == null || showIf.ExpectedStrings.Length == 0)
                        {
                            error = $"ShowIf: Enum value name is empty on '{compared.name}'.";
                            return false;
                        }

                        var enumNames = compared.enumNames;
                        if (enumNames == null || enumNames.Length == 0)
                        {
                            error = $"ShowIf: Enum on '{compared.name}' has no names.";
                            return false;
                        }

                        return EvaluateEnumNames(compared.enumValueIndex, enumNames, showIf.Comparison, showIf.ExpectedStrings, out error);
                    }

                    error = $"ShowIf: Expected string on '{compared.name}'.";
                    return false;
                }
                case ShowIfValueType.Enum:
                {
                    if (compared.propertyType != SerializedPropertyType.Enum)
                    {
                        error = $"ShowIf: Expected enum on '{compared.name}'.";
                        return false;
                    }

                    var enumNamesEnum = compared.enumNames;
                    if (enumNamesEnum == null || enumNamesEnum.Length == 0)
                    {
                        error = $"ShowIf: Enum on '{compared.name}' has no names.";
                        return false;
                    }

                    if (showIf.ExpectedEnumNames != null && showIf.ExpectedEnumNames.Length > 0)
                    {
                        return EvaluateEnumNames(compared.enumValueIndex, enumNamesEnum, showIf.Comparison, showIf.ExpectedEnumNames, out error);
                    }

                    if (showIf.ExpectedEnumIndices != null && showIf.ExpectedEnumIndices.Length > 0)
                    {
                        return EvaluateInt(compared.enumValueIndex, showIf.Comparison, showIf.ExpectedEnumIndices, out error);
                    }

                    error = $"ShowIf: No expected enum values provided for '{compared.name}'.";
                    return false;
                }
                default:
                    error = "ShowIf: Unsupported value type.";
                    return false;
            }
        }

        private static float GetErrorHeight(SerializedProperty property, GUIContent label)
        {
            var fieldHeight = EditorGUI.GetPropertyHeight(property, label, true);
            return HelpBoxHeight + EditorGUIUtility.standardVerticalSpacing + fieldHeight;
        }

        private static bool EvaluateBool(bool value, ShowIfComparison comparison, bool expected, out string error)
        {
            error = string.Empty;
            switch (comparison)
            {
                case ShowIfComparison.Equals:
                    return value == expected;
                case ShowIfComparison.NotEquals:
                    return value != expected;
                default:
                    error = "ShowIf: Unsupported comparison for bool.";
                    return false;
            }
        }

        private static bool EvaluateInt(int value, ShowIfComparison comparison, int[] expectedValues, out string error)
        {
            error = string.Empty;
            if (expectedValues == null || expectedValues.Length == 0)
            {
                error = "ShowIf: No expected values provided.";
                return false;
            }

            switch (comparison)
            {
                case ShowIfComparison.Equals:
                    return AnyIntMatch(value, expectedValues);
                case ShowIfComparison.NotEquals:
                    return !AnyIntMatch(value, expectedValues);
                case ShowIfComparison.Greater:
                    return value > expectedValues[0];
                case ShowIfComparison.GreaterOrEqual:
                    return value >= expectedValues[0];
                case ShowIfComparison.Less:
                    return value < expectedValues[0];
                case ShowIfComparison.LessOrEqual:
                    return value <= expectedValues[0];
                default:
                    error = "ShowIf: Unsupported comparison for int.";
                    return false;
            }
        }

        private static bool EvaluateFloat(float value, ShowIfComparison comparison, float[] expectedValues, out string error)
        {
            error = string.Empty;
            if (expectedValues == null || expectedValues.Length == 0)
            {
                error = "ShowIf: No expected values provided.";
                return false;
            }

            switch (comparison)
            {
                case ShowIfComparison.Equals:
                    return AnyFloatMatch(value, expectedValues);
                case ShowIfComparison.NotEquals:
                    return !AnyFloatMatch(value, expectedValues);
                case ShowIfComparison.Greater:
                    return value > expectedValues[0];
                case ShowIfComparison.GreaterOrEqual:
                    return value >= expectedValues[0];
                case ShowIfComparison.Less:
                    return value < expectedValues[0];
                case ShowIfComparison.LessOrEqual:
                    return value <= expectedValues[0];
                default:
                    error = "ShowIf: Unsupported comparison for float.";
                    return false;
            }
        }

        private static bool EvaluateString(string value, ShowIfComparison comparison, string[] expectedValues, out string error)
        {
            error = string.Empty;
            if (expectedValues == null || expectedValues.Length == 0)
            {
                error = "ShowIf: No expected values provided.";
                return false;
            }

            switch (comparison)
            {
                case ShowIfComparison.Equals:
                    return AnyStringMatch(value, expectedValues);
                case ShowIfComparison.NotEquals:
                    return !AnyStringMatch(value, expectedValues);
                default:
                    error = "ShowIf: Unsupported comparison for string.";
                    return false;
            }
        }

        private static bool EvaluateEnumNames(
            int enumValueIndex,
            string[] enumNames,
            ShowIfComparison comparison,
            string[] expectedNames,
            out string error)
        {
            error = string.Empty;
            if (expectedNames == null || expectedNames.Length == 0)
            {
                error = "ShowIf: No expected enum names provided.";
                return false;
            }

            switch (comparison)
            {
                case ShowIfComparison.Equals:
                    return AnyEnumNameMatch(enumValueIndex, enumNames, expectedNames, out error);
                case ShowIfComparison.NotEquals:
                    return !AnyEnumNameMatch(enumValueIndex, enumNames, expectedNames, out error);
                default:
                    error = "ShowIf: Unsupported comparison for enum names.";
                    return false;
            }
        }

        private static bool AnyIntMatch(int value, int[] expectedValues)
        {
            for (var i = 0; i < expectedValues.Length; i++)
            {
                if (expectedValues[i] == value)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool AnyFloatMatch(float value, float[] expectedValues)
        {
            for (var i = 0; i < expectedValues.Length; i++)
            {
                if (Mathf.Approximately(expectedValues[i], value))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool AnyStringMatch(string value, string[] expectedValues)
        {
            for (var i = 0; i < expectedValues.Length; i++)
            {
                if (string.Equals(expectedValues[i], value, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool AnyEnumNameMatch(
            int enumValueIndex,
            string[] enumNames,
            string[] expectedNames,
            out string error)
        {
            error = string.Empty;
            var foundAny = false;
            for (var i = 0; i < expectedNames.Length; i++)
            {
                var expected = expectedNames[i] ?? string.Empty;
                for (var nameIndex = 0; nameIndex < enumNames.Length; nameIndex++)
                {
                    if (!string.Equals(enumNames[nameIndex], expected, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    foundAny = true;
                    if (enumValueIndex == nameIndex)
                    {
                        return true;
                    }
                }
            }

            if (!foundAny)
            {
                error = $"ShowIf: Enum value(s) '{string.Join(", ", expectedNames)}' not found.";
            }

            return false;
        }

        private static void DrawError(Rect position, SerializedProperty property, GUIContent label, string message)
        {
            var helpRect = new Rect(position.x, position.y, position.width, HelpBoxHeight);
            EditorGUI.HelpBox(helpRect, message, MessageType.Warning);

            var fieldRect = new Rect(
                position.x,
                helpRect.yMax + EditorGUIUtility.standardVerticalSpacing,
                position.width,
                EditorGUI.GetPropertyHeight(property, label, true));

            EditorGUI.PropertyField(fieldRect, property, label, true);
        }
    }
}
