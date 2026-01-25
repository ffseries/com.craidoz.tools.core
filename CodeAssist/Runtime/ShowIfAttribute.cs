using System;
using UnityEngine;

namespace Craidoz.Tools.CodeAssist
{
    public enum ShowIfValueType
    {
        Bool,
        Int,
        Float,
        String,
        Enum
    }

    public enum ShowIfComparison
    {
        Equals,
        NotEquals,
        Greater,
        GreaterOrEqual,
        Less,
        LessOrEqual
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public class ShowIfAttribute : PropertyAttribute
    {
        public string ComparedPropertyName { get; }
        public ShowIfValueType ValueType { get; }
        public ShowIfComparison Comparison { get; }
        public bool ExpectedBool { get; }
        public int ExpectedInt { get; }
        public float ExpectedFloat { get; }
        public string ExpectedString { get; }
        public string ExpectedEnumName { get; }
        public int ExpectedEnumIndex { get; }
        public int[] ExpectedInts { get; }
        public float[] ExpectedFloats { get; }
        public string[] ExpectedStrings { get; }
        public string[] ExpectedEnumNames { get; }
        public int[] ExpectedEnumIndices { get; }

        protected ShowIfAttribute(string comparedPropertyName, ShowIfValueType valueType, ShowIfComparison comparison)
        {
            ComparedPropertyName = comparedPropertyName;
            ValueType = valueType;
            Comparison = comparison;
            ExpectedBool = false;
            ExpectedInt = 0;
            ExpectedFloat = 0f;
            ExpectedString = string.Empty;
            ExpectedEnumName = string.Empty;
            ExpectedEnumIndex = -1;
            ExpectedInts = Array.Empty<int>();
            ExpectedFloats = Array.Empty<float>();
            ExpectedStrings = Array.Empty<string>();
            ExpectedEnumNames = Array.Empty<string>();
            ExpectedEnumIndices = Array.Empty<int>();
        }

        public ShowIfAttribute(string comparedPropertyName)
            : this(comparedPropertyName, ShowIfValueType.Bool, ShowIfComparison.Equals)
        {
            ExpectedBool = true;
        }

        public ShowIfAttribute(string comparedPropertyName, bool expectedValue)
            : this(comparedPropertyName, ShowIfValueType.Bool, ShowIfComparison.Equals)
        {
            ExpectedBool = expectedValue;
        }

        public ShowIfAttribute(string comparedPropertyName, int expectedValue)
            : this(comparedPropertyName, ShowIfValueType.Int, ShowIfComparison.Equals)
        {
            ExpectedInt = expectedValue;
            ExpectedInts = new[] { expectedValue };
        }

        public ShowIfAttribute(string comparedPropertyName, params int[] expectedValues)
            : this(comparedPropertyName, ShowIfValueType.Int, ShowIfComparison.Equals)
        {
            ExpectedInts = expectedValues ?? Array.Empty<int>();
            ExpectedInt = ExpectedInts.Length > 0 ? ExpectedInts[0] : 0;
        }

        public ShowIfAttribute(string comparedPropertyName, float expectedValue)
            : this(comparedPropertyName, ShowIfValueType.Float, ShowIfComparison.Equals)
        {
            ExpectedFloat = expectedValue;
            ExpectedFloats = new[] { expectedValue };
        }

        public ShowIfAttribute(string comparedPropertyName, params float[] expectedValues)
            : this(comparedPropertyName, ShowIfValueType.Float, ShowIfComparison.Equals)
        {
            ExpectedFloats = expectedValues ?? Array.Empty<float>();
            ExpectedFloat = ExpectedFloats.Length > 0 ? ExpectedFloats[0] : 0f;
        }

        public ShowIfAttribute(string comparedPropertyName, params string[] expectedValues)
            : this(comparedPropertyName, ShowIfValueType.String, ShowIfComparison.Equals)
        {
            ExpectedStrings = expectedValues ?? Array.Empty<string>();
            ExpectedString = ExpectedStrings.Length > 0 ? ExpectedStrings[0] ?? string.Empty : string.Empty;
        }

        public ShowIfAttribute(string comparedPropertyName, string expectedValue, ShowIfValueType valueType)
            : this(comparedPropertyName, valueType, ShowIfComparison.Equals)
        {
            if (valueType == ShowIfValueType.Enum)
            {
                ExpectedEnumName = expectedValue ?? string.Empty;
                ExpectedEnumNames = new[] { ExpectedEnumName };
                ExpectedEnumIndex = -1;
                ExpectedEnumIndices = Array.Empty<int>();
            }
            else
            {
                ExpectedString = expectedValue ?? string.Empty;
                ExpectedStrings = new[] { ExpectedString };
            }
        }

        public ShowIfAttribute(string comparedPropertyName, int expectedValue, ShowIfValueType valueType)
            : this(comparedPropertyName, valueType, ShowIfComparison.Equals)
        {
            if (valueType == ShowIfValueType.Enum)
            {
                ExpectedEnumIndex = expectedValue;
                ExpectedEnumName = string.Empty;
                ExpectedEnumIndices = new[] { expectedValue };
                ExpectedEnumNames = Array.Empty<string>();
            }
            else
            {
                ExpectedInt = expectedValue;
                ExpectedInts = new[] { expectedValue };
            }
        }

        protected ShowIfAttribute(string comparedPropertyName, ShowIfComparison comparison, bool expectedValue)
            : this(comparedPropertyName, ShowIfValueType.Bool, comparison)
        {
            ExpectedBool = expectedValue;
        }

        protected ShowIfAttribute(string comparedPropertyName, ShowIfComparison comparison, int expectedValue)
            : this(comparedPropertyName, ShowIfValueType.Int, comparison)
        {
            ExpectedInt = expectedValue;
            ExpectedInts = new[] { expectedValue };
        }

        protected ShowIfAttribute(string comparedPropertyName, ShowIfComparison comparison, params int[] expectedValues)
            : this(comparedPropertyName, ShowIfValueType.Int, comparison)
        {
            ExpectedInts = expectedValues ?? Array.Empty<int>();
            ExpectedInt = ExpectedInts.Length > 0 ? ExpectedInts[0] : 0;
        }

        protected ShowIfAttribute(string comparedPropertyName, ShowIfComparison comparison, float expectedValue)
            : this(comparedPropertyName, ShowIfValueType.Float, comparison)
        {
            ExpectedFloat = expectedValue;
            ExpectedFloats = new[] { expectedValue };
        }

        protected ShowIfAttribute(string comparedPropertyName, ShowIfComparison comparison, params float[] expectedValues)
            : this(comparedPropertyName, ShowIfValueType.Float, comparison)
        {
            ExpectedFloats = expectedValues ?? Array.Empty<float>();
            ExpectedFloat = ExpectedFloats.Length > 0 ? ExpectedFloats[0] : 0f;
        }

        protected ShowIfAttribute(string comparedPropertyName, ShowIfComparison comparison, params string[] expectedValues)
            : this(comparedPropertyName, ShowIfValueType.String, comparison)
        {
            ExpectedStrings = expectedValues ?? Array.Empty<string>();
            ExpectedString = ExpectedStrings.Length > 0 ? ExpectedStrings[0] ?? string.Empty : string.Empty;
        }

        protected ShowIfAttribute(
            string comparedPropertyName,
            ShowIfComparison comparison,
            ShowIfValueType valueType,
            string expectedValue)
            : this(comparedPropertyName, valueType, comparison)
        {
            if (valueType == ShowIfValueType.Enum)
            {
                ExpectedEnumName = expectedValue ?? string.Empty;
                ExpectedEnumNames = new[] { ExpectedEnumName };
                ExpectedEnumIndex = -1;
                ExpectedEnumIndices = Array.Empty<int>();
            }
            else
            {
                ExpectedString = expectedValue ?? string.Empty;
                ExpectedStrings = new[] { ExpectedString };
            }
        }

        protected ShowIfAttribute(
            string comparedPropertyName,
            ShowIfComparison comparison,
            ShowIfValueType valueType,
            int expectedValue)
            : this(comparedPropertyName, valueType, comparison)
        {
            if (valueType == ShowIfValueType.Enum)
            {
                ExpectedEnumIndex = expectedValue;
                ExpectedEnumName = string.Empty;
                ExpectedEnumIndices = new[] { expectedValue };
                ExpectedEnumNames = Array.Empty<string>();
            }
            else
            {
                ExpectedInt = expectedValue;
                ExpectedInts = new[] { expectedValue };
            }
        }
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public sealed class ShowIfNotAttribute : ShowIfAttribute
    {
        public ShowIfNotAttribute(string comparedPropertyName, bool expectedValue)
            : base(comparedPropertyName, ShowIfComparison.NotEquals, expectedValue)
        {
        }

        public ShowIfNotAttribute(string comparedPropertyName, int expectedValue)
            : base(comparedPropertyName, ShowIfComparison.NotEquals, expectedValue)
        {
        }

        public ShowIfNotAttribute(string comparedPropertyName, params int[] expectedValues)
            : base(comparedPropertyName, ShowIfComparison.NotEquals, expectedValues)
        {
        }

        public ShowIfNotAttribute(string comparedPropertyName, float expectedValue)
            : base(comparedPropertyName, ShowIfComparison.NotEquals, expectedValue)
        {
        }

        public ShowIfNotAttribute(string comparedPropertyName, params float[] expectedValues)
            : base(comparedPropertyName, ShowIfComparison.NotEquals, expectedValues)
        {
        }

        public ShowIfNotAttribute(string comparedPropertyName, params string[] expectedValues)
            : base(comparedPropertyName, ShowIfComparison.NotEquals, expectedValues)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public sealed class ShowIfGreaterAttribute : ShowIfAttribute
    {
        public ShowIfGreaterAttribute(string comparedPropertyName, int expectedValue)
            : base(comparedPropertyName, ShowIfComparison.Greater, expectedValue)
        {
        }

        public ShowIfGreaterAttribute(string comparedPropertyName, float expectedValue)
            : base(comparedPropertyName, ShowIfComparison.Greater, expectedValue)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public sealed class ShowIfGreaterOrEqualAttribute : ShowIfAttribute
    {
        public ShowIfGreaterOrEqualAttribute(string comparedPropertyName, int expectedValue)
            : base(comparedPropertyName, ShowIfComparison.GreaterOrEqual, expectedValue)
        {
        }

        public ShowIfGreaterOrEqualAttribute(string comparedPropertyName, float expectedValue)
            : base(comparedPropertyName, ShowIfComparison.GreaterOrEqual, expectedValue)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public sealed class ShowIfLessAttribute : ShowIfAttribute
    {
        public ShowIfLessAttribute(string comparedPropertyName, int expectedValue)
            : base(comparedPropertyName, ShowIfComparison.Less, expectedValue)
        {
        }

        public ShowIfLessAttribute(string comparedPropertyName, float expectedValue)
            : base(comparedPropertyName, ShowIfComparison.Less, expectedValue)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public sealed class ShowIfLessOrEqualAttribute : ShowIfAttribute
    {
        public ShowIfLessOrEqualAttribute(string comparedPropertyName, int expectedValue)
            : base(comparedPropertyName, ShowIfComparison.LessOrEqual, expectedValue)
        {
        }

        public ShowIfLessOrEqualAttribute(string comparedPropertyName, float expectedValue)
            : base(comparedPropertyName, ShowIfComparison.LessOrEqual, expectedValue)
        {
        }
    }
}
