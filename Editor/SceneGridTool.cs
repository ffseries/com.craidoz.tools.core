using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Craidoz.Tools.Core.Editor
{
    internal enum SceneGridPlane
    {
        XY = 0,
        XZ = 1,
        YZ = 2
    }

    [InitializeOnLoad]
    internal static class SceneGridTool
    {
        private const string MenuRoot = "CraidoZ Tools/Scene Grid/";
        private const string MenuShowSceneButton = MenuRoot + "Show Scene View Button";
        private const string MenuEnableGrid = MenuRoot + "Enable Grid";
        private const string MenuOpenSettings = MenuRoot + "Settings";
        private const int MenuPriorityShowSceneButton = 1000;
        private const int MenuPriorityEnableGrid = 1001;
        private const int MenuPrioritySettings = 1002;

        private const float ToolbarWidth = 108f;
        private const float ToolbarHeight = 20f;
        private const float ToolbarRightPadding = 210f;
        private const float ToolbarTopPadding = 2f;

        static SceneGridTool()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.delayCall += SyncMenuCheckStates;
        }

        [MenuItem(MenuEnableGrid, false, MenuPriorityEnableGrid)]
        private static void ToggleGrid()
        {
            SceneGridPrefs.GridEnabled = !SceneGridPrefs.GridEnabled;
            SyncMenuCheckStates();
            SceneView.RepaintAll();
        }

        [MenuItem(MenuEnableGrid, true, MenuPriorityEnableGrid)]
        private static bool ValidateToggleGrid()
        {
            Menu.SetChecked(MenuEnableGrid, SceneGridPrefs.GridEnabled);
            return true;
        }

        [MenuItem(MenuShowSceneButton, false, MenuPriorityShowSceneButton)]
        private static void ToggleShowSceneButton()
        {
            SceneGridPrefs.ShowSceneButton = !SceneGridPrefs.ShowSceneButton;
            SyncMenuCheckStates();
            SceneView.RepaintAll();
        }

        [MenuItem(MenuShowSceneButton, true, MenuPriorityShowSceneButton)]
        private static bool ValidateShowSceneButton()
        {
            Menu.SetChecked(MenuShowSceneButton, SceneGridPrefs.ShowSceneButton);
            return true;
        }

        [MenuItem(MenuOpenSettings, false, MenuPrioritySettings)]
        private static void OpenSettings()
        {
            SceneGridSettingsWindow.ShowWindow();
        }

        private static void SyncMenuCheckStates()
        {
            Menu.SetChecked(MenuEnableGrid, SceneGridPrefs.GridEnabled);
            Menu.SetChecked(MenuShowSceneButton, SceneGridPrefs.ShowSceneButton);
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (SceneGridPrefs.GridEnabled)
            {
                DrawGrid();
            }

            if (!SceneGridPrefs.ShowSceneButton)
            {
                return;
            }

            DrawToolbar(sceneView);
        }

        private static void DrawToolbar(SceneView sceneView)
        {
            Handles.BeginGUI();

            var area = new Rect(
                sceneView.position.width - ToolbarWidth - ToolbarRightPadding,
                ToolbarTopPadding,
                ToolbarWidth,
                ToolbarHeight);

            GUILayout.BeginArea(area, EditorStyles.toolbar);
            GUILayout.BeginHorizontal();

            var buttonLabel = "Grid " + SceneGridPrefs.Plane.ToString();
            var nextToggle = GUILayout.Toggle(
                SceneGridPrefs.GridEnabled,
                buttonLabel,
                EditorStyles.toolbarButton,
                GUILayout.Width(82f));

            if (nextToggle != SceneGridPrefs.GridEnabled)
            {
                SceneGridPrefs.GridEnabled = nextToggle;
                SyncMenuCheckStates();
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("v", EditorStyles.toolbarDropDown, GUILayout.Width(22f)))
            {
                var buttonRect = GUILayoutUtility.GetLastRect();
                buttonRect.y += buttonRect.height + 15f;
                SceneGridSettingsWindow.ShowDropdown(buttonRect);
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            Handles.EndGUI();
        }

        private static void DrawGrid()
        {
            var plane = SceneGridPrefs.Plane;
            var stepA = Mathf.Max(0.01f, SceneGridPrefs.CellSizeA);
            var stepB = Mathf.Max(0.01f, SceneGridPrefs.CellSizeB);
            var rangeA = Mathf.Max(1, SceneGridPrefs.RangeA);
            var rangeB = Mathf.Max(1, SceneGridPrefs.RangeB);

            var minA = -rangeA * stepA;
            var maxA = rangeA * stepA;
            var minB = -rangeB * stepB;
            var maxB = rangeB * stepB;

            var prevZTest = Handles.zTest;
            Handles.zTest = CompareFunction.LessEqual;

            var lineColor = SceneGridPrefs.LineColor;
            Handles.color = lineColor;
            for (var a = -rangeA; a <= rangeA; a++)
            {
                var valueA = a * stepA;
                Handles.DrawLine(PlanePoint(plane, valueA, minB), PlanePoint(plane, valueA, maxB));
            }

            for (var b = -rangeB; b <= rangeB; b++)
            {
                var valueB = b * stepB;
                Handles.DrawLine(PlanePoint(plane, minA, valueB), PlanePoint(plane, maxA, valueB));
            }

            var axisColor = new Color(
                Mathf.Clamp01(lineColor.r + 0.15f),
                Mathf.Clamp01(lineColor.g + 0.15f),
                Mathf.Clamp01(lineColor.b + 0.15f),
                1f);
            Handles.color = axisColor;
            Handles.DrawLine(PlanePoint(plane, minA, 0f), PlanePoint(plane, maxA, 0f));
            Handles.DrawLine(PlanePoint(plane, 0f, minB), PlanePoint(plane, 0f, maxB));

            if (SceneGridPrefs.ShowCellIndices)
            {
                DrawCellIndices(plane, stepA, stepB, rangeA, rangeB);
            }

            Handles.zTest = prevZTest;
        }

        private static Vector3 PlanePoint(SceneGridPlane plane, float a, float b)
        {
            switch (plane)
            {
                case SceneGridPlane.XY:
                    return new Vector3(a, b, 0f);
                case SceneGridPlane.XZ:
                    return new Vector3(a, 0f, b);
                case SceneGridPlane.YZ:
                    return new Vector3(0f, a, b);
                default:
                    return new Vector3(a, b, 0f);
            }
        }

        private static void DrawCellIndices(SceneGridPlane plane, float stepA, float stepB, int rangeA, int rangeB)
        {
            var cells = rangeA * 2 * rangeB * 2;
            if (cells > 900)
            {
                return;
            }

            var style = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = SceneGridPrefs.TextColor;

            for (var a = -rangeA; a < rangeA; a++)
            {
                for (var b = -rangeB; b < rangeB; b++)
                {
                    var pos = PlanePoint(plane, (a + 0.5f) * stepA, (b + 0.5f) * stepB);
                    Handles.Label(pos, a + "," + b, style);
                }
            }
        }
    }

    internal static class SceneGridPrefs
    {
        private const string KeyGridEnabled = "Craidoz.Tools.Core.SceneGrid.Enabled";
        private const string KeyShowSceneButton = "Craidoz.Tools.Core.SceneGrid.ShowSceneButton";
        private const string KeyPlane = "Craidoz.Tools.Core.SceneGrid.Plane";
        private const string KeyCellSizeA = "Craidoz.Tools.Core.SceneGrid.CellSizeA";
        private const string KeyCellSizeB = "Craidoz.Tools.Core.SceneGrid.CellSizeB";
        private const string KeyRangeA = "Craidoz.Tools.Core.SceneGrid.RangeA";
        private const string KeyRangeB = "Craidoz.Tools.Core.SceneGrid.RangeB";
        private const string KeyShowCellIndices = "Craidoz.Tools.Core.SceneGrid.ShowCellIndices";
        private const string KeyLineColor = "Craidoz.Tools.Core.SceneGrid.LineColor";
        private const string KeyTextColor = "Craidoz.Tools.Core.SceneGrid.TextColor";

        internal static bool GridEnabled
        {
            get => EditorPrefs.GetBool(KeyGridEnabled, false);
            set => EditorPrefs.SetBool(KeyGridEnabled, value);
        }

        internal static bool ShowSceneButton
        {
            get => EditorPrefs.GetBool(KeyShowSceneButton, true);
            set => EditorPrefs.SetBool(KeyShowSceneButton, value);
        }

        internal static SceneGridPlane Plane
        {
            get => (SceneGridPlane)Mathf.Clamp(EditorPrefs.GetInt(KeyPlane, (int)SceneGridPlane.XY), 0, 2);
            set => EditorPrefs.SetInt(KeyPlane, (int)value);
        }

        internal static float CellSizeA
        {
            get => EditorPrefs.GetFloat(KeyCellSizeA, 1f);
            set => EditorPrefs.SetFloat(KeyCellSizeA, Mathf.Max(0.01f, value));
        }

        internal static float CellSizeB
        {
            get => EditorPrefs.GetFloat(KeyCellSizeB, 1f);
            set => EditorPrefs.SetFloat(KeyCellSizeB, Mathf.Max(0.01f, value));
        }

        internal static int RangeA
        {
            get => EditorPrefs.GetInt(KeyRangeA, 10);
            set => EditorPrefs.SetInt(KeyRangeA, Mathf.Max(1, value));
        }

        internal static int RangeB
        {
            get => EditorPrefs.GetInt(KeyRangeB, 10);
            set => EditorPrefs.SetInt(KeyRangeB, Mathf.Max(1, value));
        }

        internal static bool ShowCellIndices
        {
            get => EditorPrefs.GetBool(KeyShowCellIndices, false);
            set => EditorPrefs.SetBool(KeyShowCellIndices, value);
        }

        internal static Color LineColor
        {
            get => GetColor(KeyLineColor, new Color(0.20f, 0.80f, 0.95f, 0.55f));
            set => SetColor(KeyLineColor, value);
        }

        internal static Color TextColor
        {
            get => GetColor(KeyTextColor, new Color(1f, 1f, 1f, 0.9f));
            set => SetColor(KeyTextColor, value);
        }

        private static Color GetColor(string key, Color fallback)
        {
            var raw = EditorPrefs.GetString(key, string.Empty);
            if (!string.IsNullOrEmpty(raw) && ColorUtility.TryParseHtmlString(raw, out var color))
            {
                return color;
            }

            return fallback;
        }

        private static void SetColor(string key, Color color)
        {
            EditorPrefs.SetString(key, "#" + ColorUtility.ToHtmlStringRGBA(color));
        }
    }

    internal sealed class SceneGridSettingsWindow : EditorWindow
    {
        internal static void ShowWindow()
        {
            var window = GetWindow<SceneGridSettingsWindow>("Scene Grid Settings");
            window.minSize = new Vector2(320f, 240f);
        }

        internal static void ShowDropdown(Rect guiRect)
        {
            var topLeft = GUIUtility.GUIToScreenPoint(new Vector2(guiRect.x, guiRect.y));
            var screen = new Rect(topLeft.x, topLeft.y, guiRect.width, guiRect.height);
            var window = CreateInstance<SceneGridSettingsWindow>();
            window.ShowAsDropDown(screen, new Vector2(300f, 225f));
        }

        private void OnGUI()
        {
            DrawSettingsContent(drawCloseButton: true, closeAction: Close);
        }

        internal static void DrawSettingsContent(bool drawCloseButton, System.Action closeAction)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Scene Grid Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(6f);

            DrawPlaneSelector();
            EditorGUILayout.Space(4f);

            GetAxisLabels(SceneGridPrefs.Plane, out var axisA, out var axisB);

            var cellA = EditorGUILayout.FloatField("Cell Size " + axisA, SceneGridPrefs.CellSizeA);
            var cellB = EditorGUILayout.FloatField("Cell Size " + axisB, SceneGridPrefs.CellSizeB);
            var rangeA = EditorGUILayout.IntField("Range " + axisA + " (cells)", SceneGridPrefs.RangeA);
            var rangeB = EditorGUILayout.IntField("Range " + axisB + " (cells)", SceneGridPrefs.RangeB);
            var showIndices = EditorGUILayout.Toggle("Show Cell Indices", SceneGridPrefs.ShowCellIndices);
            var lineColor = EditorGUILayout.ColorField("Line Color", SceneGridPrefs.LineColor);
            var textColor = EditorGUILayout.ColorField("Text Color", SceneGridPrefs.TextColor);

            if (drawCloseButton)
            {
                EditorGUILayout.Space(6f);
                if (GUILayout.Button("Close", GUILayout.Height(22f)))
                {
                    closeAction?.Invoke();
                }
            }

            SceneGridPrefs.CellSizeA = cellA;
            SceneGridPrefs.CellSizeB = cellB;
            SceneGridPrefs.RangeA = rangeA;
            SceneGridPrefs.RangeB = rangeB;
            SceneGridPrefs.ShowCellIndices = showIndices;
            SceneGridPrefs.LineColor = lineColor;
            SceneGridPrefs.TextColor = textColor;

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "Grid is drawn on selected plane through world origin. Label count is auto-limited for performance.",
                MessageType.None);

            SceneView.RepaintAll();
        }

        private static void DrawPlaneSelector()
        {
            EditorGUILayout.LabelField("Plane");
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPlaneToggle(SceneGridPlane.XY, "XY");
                DrawPlaneToggle(SceneGridPlane.XZ, "XZ");
                DrawPlaneToggle(SceneGridPlane.YZ, "YZ");
            }
        }

        private static void DrawPlaneToggle(SceneGridPlane plane, string label)
        {
            var selected = SceneGridPrefs.Plane == plane;
            var next = GUILayout.Toggle(selected, label, EditorStyles.miniButton);
            if (next && !selected)
            {
                SceneGridPrefs.Plane = plane;
            }
        }

        private static void GetAxisLabels(SceneGridPlane plane, out string axisA, out string axisB)
        {
            switch (plane)
            {
                case SceneGridPlane.XY:
                    axisA = "X";
                    axisB = "Y";
                    return;
                case SceneGridPlane.XZ:
                    axisA = "X";
                    axisB = "Z";
                    return;
                case SceneGridPlane.YZ:
                    axisA = "Y";
                    axisB = "Z";
                    return;
                default:
                    axisA = "X";
                    axisB = "Y";
                    return;
            }
        }
    }
}
