using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Craidoz.Tools.Core.Editor
{
    internal sealed class RetargetBonePicker : PopupWindowContent
    {
        private readonly RetargetRig rig;
        private readonly Action<string> onPick;
        private readonly HashSet<string> used;
        private string search = "";
        private Vector2 scroll;

        public RetargetBonePicker(RetargetRig rig, HashSet<string> used, Action<string> onPick)
        {
            this.rig = rig;
            this.used = used;
            this.onPick = onPick;
        }

        public override Vector2 GetWindowSize() => new Vector2(420, 360);

        public override void OnGUI(Rect rect)
        {
            GUILayout.Space(8);
            GUILayout.Label("Choose a target bone", EditorStyles.boldLabel);
            GUI.SetNextControlName("BoneSearch");
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                editorWindow.Close();
                return;
            }
            if (GUILayout.Button("None / clear mapping", EditorStyles.miniButton))
            {
                onPick(null);
                editorWindow.Close();
                return;
            }
            bool picked = false;
            string pickedPath = null;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            try
            {
                foreach (var bone in rig.bones)
                {
                    string path = RetargetRig.DisplayPath(bone.path);
                    if (!string.IsNullOrEmpty(search) &&
                        (bone.name + "/" + path).IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    bool taken = used.Contains(bone.path);
                    using (new EditorGUI.DisabledScope(taken))
                    {
                        if (GUILayout.Button(new GUIContent(
                            new string(' ', Math.Min(bone.depth, 8) * 2) + bone.name + (taken ? "  [already used]" : ""),
                            path), EditorStyles.miniButton, GUILayout.Height(24)))
                        {
                            picked = true;
                            pickedPath = bone.path;
                            break;
                        }
                    }
                    GUILayout.Label(path, EditorStyles.miniLabel);
                }
            }
            finally { EditorGUILayout.EndScrollView(); }
            // Finish GUI scopes before changing the mapping or closing the popup.
            if (picked)
            {
                onPick(pickedPath);
                editorWindow.Close();
            }
        }
    }
}
