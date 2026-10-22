using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Craidoz.Tools.Core.Editor
{
    internal sealed partial class AnimationRetargetWindow : EditorWindow
    {
        [SerializeField] private GameObject sourceModel;
        [SerializeField] private GameObject targetModel;
        [SerializeField] private AnimationClip sourceClip;
        [SerializeField] private bool demo;
        [SerializeField] private int step;
        [SerializeField] private int selected;
        [SerializeField] private int view;
        [SerializeField] private Vector2 orbit = new Vector2(-35f, 20f);
        [SerializeField] private float previewZoom = 1f;
        [SerializeField] private Vector2 previewPan;
        private readonly HashSet<string> checkedPaths = new HashSet<string>();
        private bool meshesOnly;
        private int navigationControl;
        private bool panning;
        [SerializeField] private List<RetargetBoneMapping> mappings = new List<RetargetBoneMapping>();
        [SerializeField] private bool dirty;
        private RetargetRig source;
        private RetargetRig target;
        private Vector2 mappingScroll;
        private Vector2 detailsScroll;
        private Vector2 setupScroll;
        private string search = "";
        private bool needsAttentionOnly;
        private bool advanced;
        private string status = "";
        private MessageType statusType = MessageType.Info;
        private GUIStyle titleStyle, subtitleStyle, cardTitleStyle, pathStyle;
        private static readonly Color Accent = new Color(.35f, .79f, .73f);
        private static readonly Color Amber = new Color(1f, .73f, .35f);
        private static readonly Color Blue = new Color(.4f, .69f, 1f);

        [MenuItem("CraidoZ Tools/Animation/Animation Retarget")]
        public static void Open()
        {
            var window = GetWindow<AnimationRetargetWindow>("Animation Retarget");
            window.minSize = new Vector2(960, 680);
            window.Show();
        }

        private void OnEnable()
        {
            Rebuild(false);
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += UpdateMotionPreview;
            EditorApplication.projectChanged += OnRetargetProjectChanged;
        }

        private void OnDisable()
        {
            ReleaseNavigation();
            InvalidatePreview();
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.update -= UpdateMotionPreview;
            EditorApplication.projectChanged -= OnRetargetProjectChanged;
        }

        private void OnLostFocus() => ReleaseNavigation();

        private void ReleaseNavigation()
        {
            if (navigationControl != 0 && GUIUtility.hotControl == navigationControl)
                GUIUtility.hotControl = 0;
            navigationControl = 0;
        }

        private void OnPlayModeChanged(PlayModeStateChange state) { InvalidatePreview(); Repaint(); }

        private void Styles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 23 };
            subtitleStyle = new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = 12 };
            cardTitleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            pathStyle = new GUIStyle(EditorStyles.miniLabel) { clipping = TextClipping.Clip };
        }

        private void OnGUI()
        {
            Styles();
            if (source == null || target == null) Rebuild(false);
            using (new EditorGUILayout.VerticalScope())
            {
                DrawHeader();
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    string[] steps = { "1   Choose rigs", "2   Map bones", "3   Review" };
                    for (int i = 0; i < steps.Length; i++)
                    {
                        using (new EditorGUI.DisabledScope(i > 0 && !HasRigs))
                            if (GUILayout.Toggle(step == i, steps[i], EditorStyles.toolbarButton)) step = i;
                    }
                }
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorGUILayout.HelpBox("Exit Play Mode to edit mapping presets.", MessageType.Info);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (step == 0) DrawSetup();
                    else if (step == 1 && HasRigs) DrawMapping();
                    else if (HasRigs) DrawReview();
                    else step = 0;
                    DrawFooter();
                }
            }
        }

        private bool HasRigs => source.bones.Count > 0 && target.bones.Count > 0 &&
                                string.IsNullOrEmpty(source.error) && string.IsNullOrEmpty(target.error);

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(16);
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Space(14);
                    GUILayout.Label("Animation Retarget", titleStyle);
                    GUILayout.Label("Match bones once. Reuse your setup across animations.", subtitleStyle);
                    GUILayout.Space(12);
                }
                GUILayout.FlexibleSpace();
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(165)))
                {
                    GUILayout.Space(18);
                    GUILayout.Label(demo ? "DEMO  /  no assets changed" : "GENERIC ANIMATION", EditorStyles.miniBoldLabel);
                    if (GUILayout.Button(demo ? "Exit demo" : "Try example rigs", GUILayout.Height(24)))
                    {
                        if (CanReplace())
                        {
                            demo = !demo;
                            Rebuild(true);
                            if (demo)
                            {
                                // Deliberately leave two pairs for the user to try themselves.
                                for (int i = 0; i < mappings.Count; i++)
                                    if (i != 4 && i != 5)
                                    {
                                        mappings[i].TargetPath = target.bones[i].path;
                                        mappings[i].confirmed = i < 4;
                                    }
                                selected = 4;
                                step = 1;
                                status = "Try it: select Foreleg_L, then click Bone023 in the target skeleton. Confirm the pair when it looks right.";
                            }
                            else { step = 0; status = ""; }
                        }
                    }
                }
                GUILayout.Space(16);
            }
        }

        private void DrawSetup()
        {
            setupScroll = EditorGUILayout.BeginScrollView(setupScroll);
            GUILayout.Space(16);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(16);
                DrawModelCard(true);
                GUILayout.Space(12);
                DrawModelCard(false);
                GUILayout.Space(16);
            }
            GUILayout.Space(14);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("How it works", cardTitleStyle);
                GUILayout.Label("Choose model or prefab assets in their intended reference pose. Their roots define the animation paths.", subtitleStyle);
                GUILayout.Label("Map each source transform to its counterpart, inspect the skeletons, then confirm or skip each row.", subtitleStyle);
                GUILayout.Label("Preview the motion, then export a new .anim clip for the target. Mesh-only nodes are skipped by default; extra bones must be mapped or explicitly skipped.", subtitleStyle);
            }
            DrawSourceClipField();
            if (sourceClip != null)
                EditorGUILayout.HelpBox(sourceClip.name + "  |  " + sourceClip.length.ToString("0.00") +
                    " seconds. The clip paths must be relative to the selected source model root.", MessageType.Info);
            GUILayout.Space(12);
            if (!string.IsNullOrEmpty(source.error)) EditorGUILayout.HelpBox("Source: " + source.error, MessageType.Error);
            if (!string.IsNullOrEmpty(target.error)) EditorGUILayout.HelpBox("Target: " + target.error, MessageType.Error);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndScrollView();
        }

        private void DrawModelCard(bool isSource)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinHeight(155)))
            {
                GUILayout.Space(10);
                GUILayout.Label(isSource ? "SOURCE  /  original animation rig" : "TARGET  /  your character", cardTitleStyle);
                GUILayout.Label(isSource ? "The rig the animation was made for." :
                    "The character that will receive the movement.", subtitleStyle);
                GUILayout.Space(12);
                if (demo)
                {
                    GUILayout.Label(isSource ? "Example · Source_Rig" : "Example · Creature_B", EditorStyles.boldLabel);
                    GUILayout.Label("Use Exit demo to choose your own model assets.", EditorStyles.miniLabel);
                    GUILayout.Space(10);
                    return;
                }
                var current = isSource ? sourceModel : targetModel;
                var next = (GameObject)EditorGUILayout.ObjectField(current, typeof(GameObject), false, GUILayout.Height(26));
                if (next != current && CanReplace())
                {
                    if (isSource) sourceModel = next; else targetModel = next;
                    demo = false;
                    Rebuild(true);
                }
                GUILayout.Label(current == null ? "Drop a model or prefab here" :
                    (isSource ? source.bones.Count : target.bones.Count) + " transforms found", EditorStyles.miniLabel);
                GUILayout.Space(10);
            }
        }

        private void DrawMapping()
        {
            GUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(12);
                DrawCounts();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Suggest by name", GUILayout.Width(125)))
                {
                    int count = RetargetRig.Suggest(source, target, mappings);
                    if (count > 0) MarkDirty();
                    Notify(count + " suggestions added. Only unique, matching names are suggested. Review each pair.", MessageType.Info);
                }
                using (new EditorGUI.DisabledScope(demo))
                {
                    if (GUILayout.Button("Load preset", GUILayout.Width(90))) LoadPreset();
                    if (GUILayout.Button("Save preset", GUILayout.Width(90))) SavePreset();
                }
                GUILayout.Space(12);
            }
            GUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(12);
                GUILayout.Label("Skeleton view", EditorStyles.boldLabel, GUILayout.Width(100));
                int nextView = GUILayout.Toolbar(view, new[] { "Orbit", "Front", "Side", "Top" }, GUILayout.Width(260));
                if (nextView != view)
                {
                    view = nextView;
                    ResetPreviewFraming();
                }
                if (GUILayout.Button("Reset view", GUILayout.Width(82)))
                {
                    orbit = new Vector2(-35f, 20f);
                    view = 0;
                    ResetPreviewFraming();
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label("Views move together", EditorStyles.miniLabel);
                GUILayout.Space(12);
            }
            GUILayout.Label("   Left click: select joint  ·  Middle-drag / Shift + left-drag: pan  ·  Right-drag / Alt + left-drag: rotate  ·  Wheel: zoom", EditorStyles.miniLabel);
            DrawMotionControls();
            var previews = GUILayoutUtility.GetRect(200, Mathf.Clamp(position.height * .29f, 160, 270), GUILayout.ExpandWidth(true));
            float half = (previews.width - 36) / 2;
            DrawSkeleton(new Rect(previews.x + 12, previews.y, half, previews.height), motionPreview == null ? source : motionPreview.SourcePreview, true);
            DrawSkeleton(new Rect(previews.x + 24 + half, previews.y, half, previews.height), motionPreview == null ? target : motionPreview.TargetPreview, false);
            GUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(12);
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Max(400, position.width - 336))))
                {
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                    {
                        EditorGUI.BeginChangeCheck();
                        search = GUILayout.TextField(search, EditorStyles.toolbarSearchField);
                        needsAttentionOnly = GUILayout.Toggle(needsAttentionOnly, "Needs review", EditorStyles.toolbarButton, GUILayout.Width(100));
                        int filter = EditorGUILayout.Popup(meshesOnly ? 2 : showMeshNodes ? 1 : 0,
                            new[] { "Bones", "All nodes", "Meshes only" }, GUILayout.Width(110));
                        showMeshNodes = filter != 0;
                        meshesOnly = filter == 2;
                        if (EditorGUI.EndChangeCheck()) checkedPaths.Clear();
                    }
                    DrawSelectionToolbar();
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                    {
                        var visibleRows = VisibleMappingIndices();
                        bool allChecked = visibleRows.Length > 0 && visibleRows.All(i => checkedPaths.Contains(mappings[i].sourcePath));
                        using (new EditorGUI.DisabledScope(visibleRows.Length == 0))
                        {
                            bool check = GUILayout.Toggle(allChecked, new GUIContent("", "Select all visible rows"), GUILayout.Width(22));
                            if (check != allChecked) SetVisibleChecked(check);
                        }
                        GUILayout.Label("SOURCE", GUILayout.Width(175));
                        GUILayout.Label("TARGET");
                        GUILayout.Label("STATUS", GUILayout.Width(105));
                    }
                    mappingScroll = EditorGUILayout.BeginScrollView(mappingScroll);
                    int visible = 0;
                    foreach (int i in VisibleMappingIndices())
                    {
                        DrawRow(i);
                        visible++;
                    }
                    if (visible == 0)
                        EditorGUILayout.HelpBox("No rows match this filter. Clear the search or turn off Needs review.", MessageType.Info);
                    EditorGUILayout.EndScrollView();
                }
                GUILayout.Space(10);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(290)))
                    DrawDetails();
                GUILayout.Space(12);
            }
        }

        private void DrawRow(int i)
        {
            var m = mappings[i];
            var rect = EditorGUILayout.GetControlRect(false, 39);
            if (checkedPaths.Count > 0 ? checkedPaths.Contains(m.sourcePath) : i == selected)
                EditorGUI.DrawRect(rect, new Color(.2f, .47f, .47f, .32f));
            else if (i % 2 == 0) EditorGUI.DrawRect(rect, new Color(.5f, .5f, .5f, .06f));
            bool wasChecked = checkedPaths.Contains(m.sourcePath);
            bool check = GUI.Toggle(new Rect(rect.x + 3, rect.y + 11, 20, 20), wasChecked, new GUIContent("", "Select for bulk Skip / Include"));
            if (check != wasChecked) SetRowChecked(i, check);
            var sourceRect = new Rect(rect.x + 27, rect.y + 3, 150, 32);
            GUI.Label(new Rect(sourceRect.x, sourceRect.y, sourceRect.width, 18),
                new GUIContent(source.bones[i].name, RetargetRig.DisplayPath(m.sourcePath)), EditorStyles.boldLabel);
            GUI.Label(new Rect(sourceRect.x, sourceRect.y + 17, sourceRect.width, 15),
                RetargetRig.DisplayPath(m.sourcePath), pathStyle);
            var pickRect = new Rect(rect.x + 180, rect.y + 7, Mathf.Max(70, rect.width - 290), 25);
            using (new EditorGUI.DisabledScope(m.skipped || checkedPaths.Count > 1))
                if (GUI.Button(pickRect, new GUIContent(TargetName(m) + "  ▾", m.TargetPath ?? "Choose a target bone")))
                {
                    SelectSingleRow(i);
                    OpenPicker(pickRect, i);
                }
            var labelRect = new Rect(rect.xMax - 104, rect.y + 10, 104, 20);
            var oldColor = GUI.contentColor;
            GUI.contentColor = m.skipped ? Color.gray : Issue(m) != null ? Amber : m.confirmed ? Accent : Blue;
            GUI.Label(labelRect, m.skipped ? "Skipped" : m.TargetPath == null ? "Unmapped" :
                Issue(m) != null ? "Check pair" : m.confirmed ? "Confirmed" : "Review pair", EditorStyles.miniBoldLabel);
            GUI.contentColor = oldColor;
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                SelectSingleRow(i);
                Event.current.Use();
                Repaint();
            }
        }

        private void DrawDetails()
        {
            if (mappings.Count == 0) return;
            if (checkedPaths.Count > 1)
            {
                DrawMultipleSelectionDetails();
                return;
            }
            if (checkedPaths.Count == 1) selected = mappings.FindIndex(m => checkedPaths.Contains(m.sourcePath));
            selected = Mathf.Clamp(selected, 0, mappings.Count - 1);
            var m = mappings[selected];
            detailsScroll = EditorGUILayout.BeginScrollView(detailsScroll);
            GUILayout.Space(6);
            GUILayout.Label("Selected pair", cardTitleStyle);
            GUILayout.Label(source.bones[selected].name + "  →  " + TargetName(m), EditorStyles.wordWrappedLabel);
            GUILayout.Label(source.bones[selected].Kind, EditorStyles.miniBoldLabel);
            GUILayout.Space(4);
            string issue = Issue(m);
            EditorGUILayout.HelpBox(m.skipped ? "This transform is explicitly excluded." : issue ??
                (m.confirmed ? "Pair confirmed. You can still change it below." :
                    "Check both highlighted joints before confirming."), issue == null ? MessageType.Info : MessageType.Warning);
            bool skip = EditorGUILayout.ToggleLeft("Skip this transform intentionally", m.skipped);
            if (skip != m.skipped)
            {
                m.skipped = skip;
                m.confirmed = false;
                MarkDirty();
            }
            using (new EditorGUI.DisabledScope(m.skipped))
            {
                var pickRect = GUILayoutUtility.GetRect(100, 26);
                if (GUI.Button(pickRect, "Choose target bone…")) OpenPicker(pickRect, selected);
                using (new EditorGUI.DisabledScope(m.TargetPath == null || issue != null))
                    if (GUILayout.Button(m.confirmed ? "Mark for review" : "Confirm this pair", GUILayout.Height(28)))
                    {
                        m.confirmed = !m.confirmed;
                        MarkDirty();
                    }
            }
            if (GUILayout.Button("Next unresolved pair", GUILayout.Height(23))) NextUnresolved();
            GUILayout.Space(8);
            advanced = EditorGUILayout.Foldout(advanced, "Movement settings", true);
            if (advanced)
            {
                EditorGUILayout.HelpBox("Position uses movement relative to the source reference pose. Scale uses ratios along paired local axes. Rebuild motion preview after changes.", MessageType.None);
                EditorGUI.BeginChangeCheck();
                int mode = EditorGUILayout.Popup("Position", (int)m.positionMode,
                    new[] { "Keep target rest pose", "Transfer movement offset", "Scale movement offset" });
                float multiplier = m.positionMultiplier;
                if (mode == 2) multiplier = EditorGUILayout.FloatField("Offset multiplier", multiplier);
                bool scale = EditorGUILayout.Toggle("Allow scale changes", m.transferScale);
                if (EditorGUI.EndChangeCheck())
                {
                    m.positionMode = (RetargetPositionMode)mode;
                    m.positionMultiplier = float.IsNaN(multiplier) || float.IsInfinity(multiplier) ? 1f : Mathf.Max(0, multiplier);
                    m.transferScale = scale;
                    MarkDirty();
                }
                GUILayout.Label("Rotation is converted using both reference poses. No IK or automatic foot locking is applied.", EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        private void ResetPreviewFraming()
        {
            previewZoom = 1f;
            previewPan = Vector2.zero;
            Repaint();
        }

        private Quaternion PreviewRotation => view == 1 ? Quaternion.identity :
            view == 2 ? Quaternion.Euler(0, 90, 0) :
            view == 3 ? Quaternion.Euler(-90, 0, 0) :
            Quaternion.Euler(orbit.y, orbit.x, 0);

        private void HandlePreviewNavigation(Rect viewport, int control)
        {
            var evt = Event.current;
            if (!GUI.enabled) return;
            if (evt.type == EventType.ScrollWheel && viewport.Contains(evt.mousePosition))
            {
                // Zoom around the cursor, retaining the same point under it.
                float zoom = Mathf.Clamp(previewZoom * Mathf.Exp(-evt.delta.y * .08f), .25f, 8f);
                var anchor = evt.mousePosition - viewport.center;
                previewPan = anchor - (anchor - previewPan) * (zoom / previewZoom);
                previewZoom = zoom;
                evt.Use();
                Repaint();
            }
            if (evt.type == EventType.MouseDown && viewport.Contains(evt.mousePosition) &&
                GUIUtility.hotControl == 0 && (evt.button == 1 || evt.button == 2 || (evt.button == 0 && (evt.alt || evt.shift))))
            {
                panning = evt.button == 2 || (evt.button == 0 && evt.shift);
                navigationControl = control;
                GUIUtility.hotControl = control;
                if (!panning && view != 0)
                {
                    orbit = view == 1 ? Vector2.zero : view == 2 ? new Vector2(90, 0) : new Vector2(0, -90);
                    view = 0;
                }
                evt.Use();
            }
            if (navigationControl != control || GUIUtility.hotControl != control) return;
            if (evt.type == EventType.MouseDrag)
            {
                if (panning) previewPan += evt.delta;
                else
                {
                    orbit.x = Mathf.Repeat(orbit.x + evt.delta.x * .5f + 180f, 360f) - 180f;
                    orbit.y = Mathf.Clamp(orbit.y + evt.delta.y * .5f, -90f, 90f);
                }
                evt.Use();
                Repaint();
            }
            else if (evt.type == EventType.MouseUp || evt.type == EventType.Ignore ||
                (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape))
            {
                ReleaseNavigation();
                evt.Use();
            }
        }

        private void DrawSkeleton(Rect rect, RetargetRig rig, bool isSource)
        {
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(.105f, .13f, .15f) : new Color(.84f, .88f, .9f));
            GUI.Label(new Rect(rect.x + 12, rect.y + 8, rect.width - 24, 20),
                (isSource ? "SOURCE" : "TARGET") + (motionPreview == null ? "  /  reference pose" : "  /  motion preview"), EditorStyles.boldLabel);
            if (rig.bones.Count == 0) return;
            var viewport = new Rect(rect.x + 4, rect.y + 29, rect.width - 8, rect.height - 55);
            int control = GUIUtility.GetControlID(isSource ? 97401 : 97402, FocusType.Passive);
            HandlePreviewNavigation(viewport, control);
            var framingRig = isSource ? source : target;
            var bounds = new Bounds(framingRig.bones[0].position, Vector3.zero);
            foreach (var bone in framingRig.bones.Where(b => showMeshNodes || !b.meshOnly)) bounds.Encapsulate(bone.position);
            float radius = Mathf.Max(.05f, framingRig.bones.Where(b => showMeshNodes || !b.meshOnly).Max(b => Vector3.Distance(b.position, bounds.center)));
            // Fit a fixed sphere: rotating a long rig must not change its zoom or pivot.
            float scale = Mathf.Min(viewport.width - 24, viewport.height - 24) / (2f * radius) * previewZoom;
            var rotation = PreviewRotation;
            Vector2 screenCenter = viewport.size * .5f + previewPan;
            var points = rig.bones.Select(b => {
                var p = (rotation * (b.position - bounds.center)) * scale;
                return screenCenter + new Vector2(p.x, -p.y);
            }).ToArray();
            string selectedPath = selected >= 0 && selected < mappings.Count ?
                (isSource ? mappings[selected].sourcePath : mappings[selected].TargetPath) : null;
            int highlighted = selectedPath != null && rig.indices.ContainsKey(selectedPath) ? rig.indices[selectedPath] : -1;
            GUI.BeginGroup(viewport);
            Handles.BeginGUI();
            var old = Handles.color;
            for (int i = 0; i < points.Length; i++)
            {
                int parent = rig.bones[i].parent;
                if (parent < 0 || (!showMeshNodes && rig.bones[i].meshOnly)) continue;
                Handles.color = i == highlighted ? Amber : (isSource ? Blue : Accent);
                Handles.DrawAAPolyLine(i == highlighted ? 4f : 2f, points[parent], points[i]);
            }
            for (int i = 0; i < points.Length; i++)
            {
                if (!showMeshNodes && rig.bones[i].meshOnly) continue;
                Handles.color = i == highlighted ? Amber : (isSource ? Blue : Accent);
                Handles.DrawSolidDisc(points[i], Vector3.forward, i == highlighted ? 6 : 3.5f);
            }
            Handles.color = old;
            Handles.EndGUI();
            for (int i = 0; i < points.Length; i++)
            {
                if (!showMeshNodes && rig.bones[i].meshOnly) continue;
                GUI.Label(new Rect(points[i].x - 7, points[i].y - 7, 14, 14),
                    new GUIContent("", rig.bones[i].name + " · " + RetargetRig.DisplayPath(rig.bones[i].path)));
            }
            var clickRect = new Rect(Vector2.zero, viewport.size);
            if (GUI.enabled && GUIUtility.hotControl == 0 && Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 && !Event.current.alt && !Event.current.shift && clickRect.Contains(Event.current.mousePosition) &&
                (isSource || checkedPaths.Count <= 1))
            {
                var mouse = Event.current.mousePosition;
                var hits = Enumerable.Range(0, points.Length)
                    .Where(i => (showMeshNodes || !rig.bones[i].meshOnly) && clickRect.Contains(points[i]) && Vector2.Distance(mouse, points[i]) < 12f)
                    .OrderBy(i => Vector2.SqrMagnitude(mouse - points[i])).ToArray();
                if (hits.Length > 0)
                {
                    int sourceIndex = selected;
                    if (hits.Length == 1) SelectPreviewBone(rig, isSource, hits[0], sourceIndex);
                    else
                    {
                        // Coincident joints remain selectable even when orbiting cannot separate them.
                        var menu = new GenericMenu();
                        foreach (int hit in hits)
                        {
                            int index = hit;
                            string label = rig.bones[index].name + "  —  " +
                                RetargetRig.DisplayPath(rig.bones[index].path).Replace("/", " › ");
                            menu.AddItem(new GUIContent(label), false,
                                () => SelectPreviewBone(rig, isSource, index, sourceIndex));
                        }
                        menu.ShowAsContext();
                    }
                    Event.current.Use();
                }
            }
            GUI.EndGroup();
            if (highlighted >= 0)
                GUI.Label(new Rect(rect.x + 12, rect.yMax - 23, rect.width - 24, 20),
                    rig.bones[highlighted].name + "  ·  " + RetargetRig.DisplayPath(rig.bones[highlighted].path), pathStyle);
            else GUI.Label(new Rect(rect.x + 12, rect.yMax - 23, rect.width - 24, 20),
                isSource ? "Click a joint to inspect" : "Pick the matching joint or use Choose target bone", pathStyle);
        }

        private void SelectPreviewBone(RetargetRig rig, bool isSource, int index, int sourceIndex)
        {
            if (isSource) SelectSingleRow(index);
            else Assign(sourceIndex, rig.bones[index].path);
            Repaint();
        }

        private void DrawReview()
        {
            setupScroll = EditorGUILayout.BeginScrollView(setupScroll);
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                GUILayout.Space(12);
                DrawReviewSummary();
                GUILayout.Space(12);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawGenerationPanel();
                }
                GUILayout.Space(8);
                DrawExportNotices();
                GUILayout.Space(8);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndScrollView();
        }

        private void DrawCounts()
        {
            int confirmed = mappings.Count(m => !m.skipped && m.confirmed && Issue(m) == null);
            int skipped = mappings.Count(m => m.skipped);
            GUILayout.Label(confirmed + " confirmed   ·   " + (mappings.Count - confirmed - skipped) +
                " need review   ·   " + skipped + " skipped", EditorStyles.boldLabel);
        }

        private void DrawFooter()
        {
            if (!string.IsNullOrEmpty(status))
                EditorGUILayout.HelpBox(status, statusType);
            GUILayout.Space(5);
            if (step == 2 && HasRigs)
            {
                DrawExportFooter();
                return;
            }
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox, GUILayout.Height(40)))
            {
                GUILayout.Label(demo ? "Example data only" : dirty ? "Preset has unsaved changes" : "Original assets stay unchanged",
                    EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (step > 0 && GUILayout.Button("Back", GUILayout.Width(75), GUILayout.Height(27))) step--;
                using (new EditorGUI.DisabledScope(!HasRigs))
                {
                    var original = GUI.backgroundColor;
                    GUI.backgroundColor = Accent;
                    if (step < 2)
                    {
                        if (GUILayout.Button(step == 0 ? "Start mapping  →" : "Review mapping  →", GUILayout.Width(165), GUILayout.Height(27)))
                            step++;
                    }
                    else using (new EditorGUI.DisabledScope(demo))
                        if (GUILayout.Button("Save mapping preset", GUILayout.Width(165), GUILayout.Height(27))) SavePreset();
                    GUI.backgroundColor = original;
                }
            }
        }

        private string Issue(RetargetBoneMapping mapping) => RetargetRig.Issue(mapping, source, target, mappings);
        private string TargetName(RetargetBoneMapping m) =>
            m.TargetPath == null ? "Choose bone…" : target.indices.TryGetValue(m.TargetPath, out int index) ?
                target.bones[index].name : "(Missing target)";

        private void OpenPicker(Rect rect, int index)
        {
            var used = new HashSet<string>(mappings.Where((m, i) => i != index && !m.skipped && m.TargetPath != null).Select(m => m.TargetPath));
            PopupWindow.Show(rect, new RetargetBonePicker(target, used, path => Assign(index, path)));
        }

        private void Assign(int index, string path)
        {
            if (index < 0 || index >= mappings.Count) return;
            if (checkedPaths.Count > 1)
            {
                Notify("Select one source row to assign its target bone.", MessageType.Info);
                return;
            }
            if (mappings[index].skipped)
            {
                Notify("Uncheck Skip this transform before assigning a target.", MessageType.Info);
                return;
            }
            if (path != null && mappings.Where((m, i) => i != index).Any(m => !m.skipped && m.TargetPath == path))
            {
                Notify("That target is already mapped. Clear its existing pair before assigning it here.", MessageType.Warning);
                return;
            }
            mappings[index].TargetPath = path;
            mappings[index].confirmed = false;
            MarkDirty();
            status = "";
            Repaint();
        }

        private void NextUnresolved()
        {
            for (int offset = 1; offset <= mappings.Count; offset++)
            {
                int index = (selected + offset) % mappings.Count;
                var m = mappings[index];
                if (!m.skipped && (!m.confirmed || Issue(m) != null))
                {
                    selected = index;
                    search = "";
                    meshesOnly = false;
                    showMeshNodes |= source.bones[index].meshOnly;
                    checkedPaths.Clear();
                    mappingScroll.y = Mathf.Max(0, index * 41 - 40);
                    return;
                }
            }
            Notify("All included pairs have been reviewed.", MessageType.Info);
        }

        private void Rebuild(bool reset)
        {
            checkedPaths.Clear();
            InvalidatePreview();
            source = demo ? RetargetRig.Demo(false) : RetargetRig.Read(sourceModel);
            target = demo ? RetargetRig.Demo(true) : RetargetRig.Read(targetModel);
            if (reset || mappings == null)
                mappings = RetargetRig.CreateMappings(source);
            else
            {
                var previous = mappings.Where(m => m != null && m.sourcePath != null)
                    .GroupBy(m => m.sourcePath).ToDictionary(g => g.Key, g => g.First());
                mappings = source.bones.Select(b => previous.TryGetValue(b.path, out var old) ? old :
                    new RetargetBoneMapping { sourcePath = b.path, skipped = b.meshOnly }).ToList();
            }
            if (!meshClassificationApplied || reset)
            {
                foreach (var m in mappings.Where(m => source.bones[source.indices[m.sourcePath]].meshOnly && !m.confirmed))
                    m.skipped = true;
                meshClassificationApplied = true;
            }
            selected = Mathf.Clamp(selected, 0, Mathf.Max(0, mappings.Count - 1));
            if (reset)
            {
                dirty = false;
                search = "";
                needsAttentionOnly = false;
                meshesOnly = false;
                mappingScroll = Vector2.zero;
            }
        }

        private bool CanReplace() => !dirty || demo ||
            EditorUtility.DisplayDialog("Replace current mapping?", "There are unsaved mapping changes. Save the preset first if you want to keep them.", "Discard changes", "Cancel");
        private void MarkDirty() { dirty = true; InvalidatePreview(); Repaint(); }
        private void Notify(string message, MessageType type) { status = message; statusType = type; Repaint(); }

        private void SavePreset()
        {
            if (demo || !HasRigs) return;
            var path = EditorUtility.SaveFilePanel("Save mapping preset", Application.dataPath, targetModel.name + "_Mapping", "json");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                var preset = new RetargetMappingPreset {
                    sourceGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sourceModel)),
                    targetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(targetModel)), bones = mappings
                };
                File.WriteAllText(path, JsonUtility.ToJson(preset, true));
                dirty = false;
                Notify("Mapping preset saved: " + path, MessageType.Info);
            }
            catch (Exception exception) { Notify("Could not save preset: " + exception.Message, MessageType.Error); }
        }

        private void LoadPreset()
        {
            if (!CanReplace()) return;
            string path = EditorUtility.OpenFilePanel("Load mapping preset for these rigs", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                var preset = JsonUtility.FromJson<RetargetMappingPreset>(File.ReadAllText(path));
                if (preset == null || preset.version != 1 || preset.bones == null)
                    throw new InvalidDataException("Unsupported mapping preset.");
                string sourceGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sourceModel));
                string targetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(targetModel));
                if (demo || preset.sourceGuid != sourceGuid || preset.targetGuid != targetGuid)
                    throw new InvalidDataException("This preset belongs to different model assets. Choose its original source and target first.");
                if (preset.bones.Count != source.bones.Count ||
                    preset.bones.Any(m => m == null || m.sourcePath == null || !source.indices.ContainsKey(m.sourcePath)) ||
                    preset.bones.Select(m => m.sourcePath).Distinct().Count() != source.bones.Count)
                    throw new InvalidDataException("The source hierarchy has changed or this preset has invalid rows.");
                if (preset.bones.Any(m => (m.TargetPath != null && !target.indices.ContainsKey(m.TargetPath)) ||
                    !Enum.IsDefined(typeof(RetargetPositionMode), m.positionMode) ||
                    float.IsNaN(m.positionMultiplier) || float.IsInfinity(m.positionMultiplier) || m.positionMultiplier < 0))
                    throw new InvalidDataException("The target hierarchy or movement settings are invalid. Check the preset against these rigs.");
                mappings = source.bones.Select(b => preset.bones.First(m => m.sourcePath == b.path)).ToList();
                checkedPaths.Clear();
                InvalidatePreview();
                dirty = false;
                step = 1;
                Notify("Preset loaded. Review any flagged pairs before continuing.", MessageType.Info);
            }
            catch (Exception exception) { Notify("Could not load preset: " + exception.Message, MessageType.Error); }
        }
    }
}
