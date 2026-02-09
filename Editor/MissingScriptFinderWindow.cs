using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Craidoz.Tools.Core.Editor
{
    internal sealed class MissingScriptFinderWindow : EditorWindow
    {
        private bool includeInactive = true;
        private readonly List<GameObject> results = new List<GameObject>();
        private Vector2 scroll;
        private string statusMessage = string.Empty;
        private MessageType statusType = MessageType.Info;

        [MenuItem("CraidoZ Tools/Hierarchy/Find Missing Scripts")]
        private static void ShowWindow()
        {
            var window = GetWindow<MissingScriptFinderWindow>("Find Missing Scripts");
            window.minSize = new Vector2(420f, 280f);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Find Missing Scripts In Hierarchy", EditorStyles.boldLabel);
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

                GUI.backgroundColor = new Color(1f, 0.85f, 0.55f);
                if (GUILayout.Button("Remove Missing", GUILayout.Height(24f)))
                {
                    RemoveMissing();
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
                var roots = scene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    foreach (var obj in EnumerateHierarchy(root))
                    {
                        if (!includeInactive && !obj.activeInHierarchy)
                        {
                            continue;
                        }

                        if (HasMissingScript(obj))
                        {
                            results.Add(obj);
                        }
                    }
                }
            }

            SetStatus($"Found {results.Count} object(s) with missing scripts.", MessageType.Info);
        }

        private void RemoveMissing()
        {
            if (results.Count == 0)
            {
                SetStatus("No results to process. Run Scan first.", MessageType.Info);
                return;
            }

            var removedCount = 0;
            foreach (var obj in results)
            {
                if (obj == null)
                {
                    continue;
                }

                Undo.RegisterFullObjectHierarchyUndo(obj, "Remove Missing Scripts");
                var count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(obj);
                removedCount += count;
                EditorUtility.SetDirty(obj);
            }

            AssetDatabase.SaveAssets();
            SetStatus($"Removed {removedCount} missing component(s).", MessageType.Info);
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

        private static bool HasMissingScript(GameObject obj)
        {
            var components = obj.GetComponents<Component>();
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    return true;
                }
            }
            return false;
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
