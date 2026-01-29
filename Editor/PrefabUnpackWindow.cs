using System.IO;
using UnityEditor;
using UnityEngine;

namespace Craidoz.Tools.Core.Editor
{
    internal sealed class PrefabUnpackWindow : EditorWindow
    {
        private readonly System.Collections.Generic.List<Object> sourceAssets = new System.Collections.Generic.List<Object>();
        private DefaultAsset outputFolder;
        private string outputName = string.Empty;
        private string statusMessage = string.Empty;
        private MessageType statusType = MessageType.Info;

        [MenuItem("CraidoZ Tools/Prefab/Unpack To New Prefab")]
        private static void ShowWindow()
        {
            var window = GetWindow<PrefabUnpackWindow>("Unpack To Prefab");
            window.minSize = new Vector2(420f, 220f);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Unpack Prefab To New Asset", EditorStyles.boldLabel);
            EditorGUILayout.Space(6f);

            DrawDropArea();
            DrawSourceList();
            outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", outputFolder, typeof(DefaultAsset), false);
            outputName = EditorGUILayout.TextField("Prefix", outputName);

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                var originalColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.75f, 0.85f, 1f);
                if (GUILayout.Button("Use Selected", GUILayout.Height(24f)))
                {
                    ApplySelection();
                }

                GUI.backgroundColor = new Color(0.95f, 0.75f, 0.75f);
                if (GUILayout.Button("Clear List", GUILayout.Height(24f)))
                {
                    sourceAssets.Clear();
                    SetStatus("Cleared source list.", MessageType.Info);
                }

                GUI.backgroundColor = new Color(0.70f, 0.95f, 0.70f);
                if (GUILayout.Button("Create Prefab", GUILayout.Height(24f)))
                {
                    CreatePrefab();
                }
                GUI.backgroundColor = originalColor;
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "Drag prefab assets from Project. Each will be instantiated, unpacked completely, then saved as new prefabs.\n" +
                "Prefix behavior: empty = keep original name. With one item, prefix replaces the name. With multiple items, prefix is added before the original name.",
                MessageType.None);

            if (!string.IsNullOrWhiteSpace(statusMessage))
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.HelpBox(statusMessage, statusType);
            }
        }

        private void ApplySelection()
        {
            if (Selection.activeObject == null)
            {
                SetStatus("No selection found.", MessageType.Info);
                return;
            }

            sourceAssets.Clear();
            foreach (var obj in Selection.objects)
            {
                if (obj != null)
                {
                    sourceAssets.Add(obj);
                }
            }

            SetStatus($"Added {sourceAssets.Count} item(s) from selection.", MessageType.Info);
        }

        private void CreatePrefab()
        {
            if (sourceAssets.Count == 0)
            {
                SetStatus("Please add one or more source assets.", MessageType.Warning);
                return;
            }

            var folderPath = GetOutputFolderPath();
            if (string.IsNullOrEmpty(folderPath))
            {
                SetStatus("Please select a valid output folder inside Assets.", MessageType.Warning);
                return;
            }

            var createdCount = 0;
            var failedCount = 0;

            foreach (var source in sourceAssets)
            {
                if (source == null)
                {
                    failedCount++;
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(source);
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    failedCount++;
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    failedCount++;
                    continue;
                }

                var name = BuildOutputName(prefab.name, sourceAssets.Count);
                var assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folderPath, $"{name}.prefab"));

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                if (instance == null)
                {
                    failedCount++;
                    continue;
                }

                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.UserAction);
                var savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, assetPath);
                DestroyImmediate(instance);

                if (savedPrefab == null)
                {
                    failedCount++;
                    continue;
                }

                createdCount++;
                EditorGUIUtility.PingObject(savedPrefab);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SetStatus($"Created {createdCount} prefab(s). Failed: {failedCount}.", MessageType.Info);
        }

        private string GetOutputFolderPath()
        {
            if (outputFolder == null)
            {
                return "Assets";
            }

            var path = AssetDatabase.GetAssetPath(outputFolder);
            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path))
            {
                return string.Empty;
            }

            return path;
        }

        private void SetStatus(string message, MessageType type)
        {
            statusMessage = message;
            statusType = type;
        }

        private void DrawDropArea()
        {
            var rect = GUILayoutUtility.GetRect(0f, 54f, GUILayout.ExpandWidth(true));
            GUI.Box(rect, "Drop Prefab Assets Here");

            var evt = Event.current;
            if (!rect.Contains(evt.mousePosition))
            {
                return;
            }

            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj != null)
                        {
                            sourceAssets.Add(obj);
                        }
                    }
                    SetStatus($"Added {DragAndDrop.objectReferences.Length} item(s) from drag.", MessageType.Info);
                }
                evt.Use();
            }
        }

        private void DrawSourceList()
        {
            if (sourceAssets.Count == 0)
            {
                EditorGUILayout.LabelField("Sources: (none)");
                return;
            }

            EditorGUILayout.LabelField($"Sources: {sourceAssets.Count}");
            for (var i = 0; i < sourceAssets.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    sourceAssets[i] = EditorGUILayout.ObjectField(sourceAssets[i], typeof(Object), false);
                    if (GUILayout.Button("X", GUILayout.Width(22f)))
                    {
                        sourceAssets.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private string BuildOutputName(string sourceName, int totalCount)
        {
            if (string.IsNullOrWhiteSpace(outputName))
            {
                return sourceName;
            }

            if (totalCount <= 1)
            {
                return outputName;
            }

            return $"{outputName}_{sourceName}";
        }
    }
}
