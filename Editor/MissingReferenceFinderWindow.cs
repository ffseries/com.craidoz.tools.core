using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Craidoz.Tools.Core.Editor
{
    internal sealed class MissingReferenceFinderWindow : EditorWindow
    {
        private const float MinObjectColumnWidth = 120f;
        private const float MinComponentColumnWidth = 120f;
        private const float MinFieldColumnWidth = 120f;
        private const float MinActionColumnWidth = 64f;
        private const float SeparatorWidth = 1f;
        private const float RowHeight = 20f;
        private const float TableHorizontalPadding = 20f;
        private const float ObjectRatio = 0.40f;
        private const float ComponentRatio = 0.25f;
        private const float FieldRatio = 0.25f;
        private const float ActionRatio = 0.10f;

        private struct MissingRefEntry
        {
            public GameObject Target;
            public Component Component;
            public string FieldPath;
        }

        private bool includeInactive = true;
        private readonly List<MissingRefEntry> results = new List<MissingRefEntry>();
        private Vector2 scroll;
        private string statusMessage = string.Empty;
        private MessageType statusType = MessageType.Info;
        private Rect lastTableRect;

        [MenuItem("CraidoZ Tools/Hierarchy/Find Missing References")]
        private static void ShowWindow()
        {
            var window = GetWindow<MissingReferenceFinderWindow>("Find Missing References");
            window.minSize = new Vector2(520f, 300f);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Find Missing References In Hierarchy", EditorStyles.boldLabel);
            EditorGUILayout.Space(6f);

            includeInactive = EditorGUILayout.Toggle("Include Inactive", includeInactive);

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                var originalColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.70f, 0.95f, 0.70f);
                if (GUILayout.Button("Scan", GUILayout.Height(24f)))
                {
                    Scan();
                }

                GUI.backgroundColor = new Color(0.95f, 0.75f, 0.75f);
                if (GUILayout.Button("Clear", GUILayout.Height(24f)))
                {
                    results.Clear();
                    SetStatus("Cleared results.", MessageType.Info);
                }
                GUI.backgroundColor = originalColor;
            }

            EditorGUILayout.Space(8f);
            DrawResults();

            if (!string.IsNullOrWhiteSpace(statusMessage))
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.HelpBox(statusMessage, statusType);
            }
        }

        private void Scan()
        {
            results.Clear();
            var scenes = GetOpenScenes();
            foreach (var scene in scenes)
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var obj in EnumerateHierarchy(root))
                    {
                        if (!includeInactive && !obj.activeInHierarchy)
                        {
                            continue;
                        }

                        var components = obj.GetComponents<Component>();
                        foreach (var component in components)
                        {
                            if (component == null)
                            {
                                continue;
                            }

                            var so = new SerializedObject(component);
                            var iterator = so.GetIterator();
                            var enterChildren = true;
                            while (iterator.NextVisible(enterChildren))
                            {
                                enterChildren = false;
                                if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                                {
                                    continue;
                                }

                                if (iterator.objectReferenceValue == null &&
                                    iterator.objectReferenceInstanceIDValue != 0)
                                {
                                    results.Add(new MissingRefEntry
                                    {
                                        Target = obj,
                                        Component = component,
                                        FieldPath = iterator.propertyPath
                                    });
                                }
                            }
                        }
                    }
                }
            }

            SetStatus($"Found {results.Count} missing reference(s).", MessageType.Info);
        }

        private static IEnumerable<GameObject> EnumerateHierarchy(GameObject root)
        {
            yield return root;
            for (var i = 0; i < root.transform.childCount; i++)
            {
                foreach (var child in EnumerateHierarchy(root.transform.GetChild(i).gameObject))
                {
                    yield return child;
                }
            }
        }

        private static List<Scene> GetOpenScenes()
        {
            var scenes = new List<Scene>();
            var count = SceneManager.sceneCount;
            for (var i = 0; i < count; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                {
                    scenes.Add(scene);
                }
            }
            return scenes;
        }

        private void DrawResults()
        {
            EditorGUILayout.LabelField($"Results: {results.Count}");
            var tableWidth = position.width - TableHorizontalPadding;
            if (tableWidth < 0f)
            {
                tableWidth = 0f;
            }

            CalculateColumnWidths(
                tableWidth,
                out var objectWidth,
                out var componentWidth,
                out var fieldWidth,
                out var actionWidth);

            DrawHeader(objectWidth, componentWidth, fieldWidth, actionWidth);
            using (var scrollView = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = scrollView.scrollPosition;
                for (var i = 0; i < results.Count; i++)
                {
                    var entry = results[i];
                    if (entry.Target == null)
                    {
                        continue;
                    }

                    DrawRow(entry, objectWidth, componentWidth, fieldWidth, actionWidth);
                }
            }
        }

        private void DrawHeader(float objectWidth, float componentWidth, float fieldWidth, float actionWidth)
        {
            var rect = GUILayoutUtility.GetRect(0f, RowHeight, GUILayout.ExpandWidth(true));
            lastTableRect = rect;
            EditorGUI.DrawRect(rect, new Color(0.20f, 0.20f, 0.20f));

            var x = rect.x;
            var objectRect = new Rect(x, rect.y, objectWidth, rect.height);
            x += objectWidth;
            var sep1 = new Rect(x, rect.y, SeparatorWidth, rect.height);
            x += SeparatorWidth;
            var componentRect = new Rect(x, rect.y, componentWidth, rect.height);
            x += componentWidth;
            var sep2 = new Rect(x, rect.y, SeparatorWidth, rect.height);
            x += SeparatorWidth;
            var fieldRect = new Rect(x, rect.y, fieldWidth, rect.height);
            x += fieldWidth;
            var sep3 = new Rect(x, rect.y, SeparatorWidth, rect.height);
            x += SeparatorWidth;
            var actionRect = new Rect(x, rect.y, actionWidth, rect.height);

            DrawSeparator(sep1);
            DrawSeparator(sep2);
            DrawSeparator(sep3);

            var labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(6, 6, 0, 0)
            };

            EditorGUI.LabelField(objectRect, "Object", labelStyle);
            EditorGUI.LabelField(componentRect, "Component", labelStyle);
            EditorGUI.LabelField(fieldRect, "Field", labelStyle);
            EditorGUI.LabelField(actionRect, "Action", labelStyle);
        }

        private void DrawRow(MissingRefEntry entry, float objectWidth, float componentWidth, float fieldWidth, float actionWidth)
        {
            var rect = GUILayoutUtility.GetRect(0f, RowHeight, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.18f));

            var x = rect.x;
            var objectRect = new Rect(x, rect.y + 2f, objectWidth, rect.height - 4f);
            x += objectWidth;
            var sep1 = new Rect(x, rect.y, SeparatorWidth, rect.height);
            x += SeparatorWidth;
            var componentRect = new Rect(x, rect.y, componentWidth, rect.height);
            x += componentWidth;
            var sep2 = new Rect(x, rect.y, SeparatorWidth, rect.height);
            x += SeparatorWidth;
            var fieldRect = new Rect(x, rect.y, fieldWidth, rect.height);
            x += fieldWidth;
            var sep3 = new Rect(x, rect.y, SeparatorWidth, rect.height);
            x += SeparatorWidth;
            var actionRect = new Rect(x + 4f, rect.y + 2f, actionWidth - 8f, rect.height - 4f);

            DrawSeparator(sep1);
            DrawSeparator(sep2);
            DrawSeparator(sep3);

            EditorGUI.ObjectField(objectRect, entry.Target, typeof(GameObject), true);
            EditorGUI.LabelField(
                componentRect,
                entry.Component != null ? entry.Component.GetType().Name : "Missing Component",
                EditorStyles.miniLabel);
            EditorGUI.LabelField(fieldRect, entry.FieldPath, EditorStyles.miniLabel);

            if (GUI.Button(actionRect, "Select"))
            {
                Selection.activeObject = entry.Target;
                EditorGUIUtility.PingObject(entry.Target);
            }
        }

        private static void DrawSeparator(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));
        }

        private static void CalculateColumnWidths(
            float tableWidth,
            out float objectWidth,
            out float componentWidth,
            out float fieldWidth,
            out float actionWidth)
        {
            var usable = tableWidth - SeparatorWidth * 3f;
            if (usable < 0f)
            {
                usable = 0f;
            }

            objectWidth = Mathf.Max(MinObjectColumnWidth, usable * ObjectRatio);
            componentWidth = Mathf.Max(MinComponentColumnWidth, usable * ComponentRatio);
            fieldWidth = Mathf.Max(MinFieldColumnWidth, usable * FieldRatio);
            actionWidth = Mathf.Max(MinActionColumnWidth, usable * ActionRatio);

            var total = objectWidth + componentWidth + fieldWidth + actionWidth;
            if (total <= 0f)
            {
                return;
            }

            if (total > usable)
            {
                var scale = usable / total;
                objectWidth *= scale;
                componentWidth *= scale;
                fieldWidth *= scale;
                actionWidth *= scale;
            }
        }

        private void SetStatus(string message, MessageType type)
        {
            statusMessage = message;
            statusType = type;
        }
    }
}
