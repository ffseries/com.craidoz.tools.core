using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Craidoz.Tools.Core.Editor
{
    internal static class PrefabUnpackTool
    {
        [MenuItem("CraidoZ Tools/Prefab/Unpack Selected %&u")]
        private static void UnpackSelected()
        {
            UnpackSelectedInternal(PrefabUnpackMode.OutermostRoot, "Unpack Prefab");
        }

        [MenuItem("CraidoZ Tools/Prefab/Unpack Selected (Completely) %&#u")]
        private static void UnpackSelectedCompletely()
        {
            UnpackSelectedInternal(PrefabUnpackMode.Completely, "Unpack Prefab Completely");
        }

        [MenuItem("CraidoZ Tools/Prefab/Unpack Selected", true)]
        [MenuItem("CraidoZ Tools/Prefab/Unpack Selected (Completely)", true)]
        private static bool ValidateUnpackSelected()
        {
            return GetSelectedPrefabRoots().Count > 0;
        }

        private static void UnpackSelectedInternal(PrefabUnpackMode mode, string undoLabel)
        {
            var roots = GetSelectedPrefabRoots();
            if (roots.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Unpack Prefab",
                    "Please select one or more prefab instances in the Hierarchy.",
                    "OK");
                return;
            }

            foreach (var root in roots)
            {
                Undo.RegisterFullObjectHierarchyUndo(root, undoLabel);
                PrefabUtility.UnpackPrefabInstance(root, mode, InteractionMode.UserAction);
            }
        }

        private static List<GameObject> GetSelectedPrefabRoots()
        {
            var roots = new List<GameObject>();
            var seen = new HashSet<GameObject>();
            var selection = Selection.gameObjects;

            foreach (var obj in selection)
            {
                if (obj == null || !PrefabUtility.IsPartOfPrefabInstance(obj))
                {
                    continue;
                }

                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(obj);
                if (root != null && seen.Add(root))
                {
                    roots.Add(root);
                }
            }

            return roots;
        }
    }
}
