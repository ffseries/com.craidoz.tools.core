using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Craidoz.Tools.Core.Editor
{
    internal sealed class ComponentFinderWindow : EditorWindow
    {
        private MonoScript targetScript;
        private bool includeInactive = true;
        private readonly List<GameObject> results = new List<GameObject>();
        private Vector2 scroll;
        private string statusMessage = string.Empty;
        private MessageType statusType = MessageType.Info;

        [MenuItem("CraidoZ Tools/Hierarchy/Find Components")]
        private static void ShowWindow()
        {
            var window = GetWindow<ComponentFinderWindow>("Find Components");
            window.minSize = new Vector2(420f, 280f);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Find Components In Hierarchy", EditorStyles.boldLabel);
            EditorGUILayout.Space(6f);

            targetScript = (MonoScript)EditorGUILayout.ObjectField("Script", targetScript, typeof(MonoScript), false);
            includeInactive = EditorGUILayout.Toggle("Include Inactive", includeInactive);

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                var originalColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.75f, 0.85f, 1f);
                if (GUILayout.Button("Use Selected", GUILayout.Height(24f)))
                {
                    ApplySelection();
                }

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

        private void ApplySelection()
        {
            if (Selection.activeObject is MonoScript script)
            {
                targetScript = script;
                return;
            }

            if (Selection.activeGameObject != null)
            {
                var component = Selection.activeGameObject.GetComponent<MonoBehaviour>();
                if (component != null)
                {
                    targetScript = MonoScript.FromMonoBehaviour(component);
                }
            }
        }

        private void Scan()
        {
            results.Clear();

            if (targetScript == null)
            {
                SetStatus("Please assign a script.", MessageType.Warning);
                return;
            }

            var type = targetScript.GetClass();
            if (type == null || !typeof(Component).IsAssignableFrom(type))
            {
                SetStatus("Script is not a Component.", MessageType.Warning);
                return;
            }

            var scenes = GetOpenScenes();
            foreach (var scene in scenes)
            {
                var roots = scene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    var components = root.GetComponentsInChildren(type, includeInactive);
                    foreach (var component in components)
                    {
                        if (component != null && component.gameObject != null)
                        {
                            results.Add(component.gameObject);
                        }
                    }
                }
            }

            SetStatus($"Found {results.Count} object(s) with {type.Name}.", MessageType.Info);
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
            using (var scrollView = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = scrollView.scrollPosition;
                for (var i = 0; i < results.Count; i++)
                {
                    var obj = results[i];
                    if (obj == null)
                    {
                        continue;
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField(obj, typeof(GameObject), true);
                        if (GUILayout.Button("Select", GUILayout.Width(64f)))
                        {
                            Selection.activeObject = obj;
                            EditorGUIUtility.PingObject(obj);
                        }
                    }
                }
            }
        }

        private void SetStatus(string message, MessageType type)
        {
            statusMessage = message;
            statusType = type;
        }
    }
}
