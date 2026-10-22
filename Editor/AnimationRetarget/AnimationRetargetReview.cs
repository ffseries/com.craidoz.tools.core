using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Craidoz.Tools.Core.Editor
{
    internal sealed partial class AnimationRetargetWindow
    {
        private bool showMappingIssues = true;
        private bool showExportDetails;
        private bool showNoticeDetails;

        private int[] VisibleMappingIndices()
        {
            return Enumerable.Range(0, mappings.Count).Where(i =>
            {
                var m = mappings[i];
                if ((!showMeshNodes && source.bones[i].meshOnly) || (meshesOnly && !source.bones[i].meshOnly)) return false;
                if (needsAttentionOnly && (m.skipped || (m.confirmed && Issue(m) == null))) return false;
                string text = m.sourcePath + " " + m.TargetPath + " " + source.bones[i].name;
                return text.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
            }).ToArray();
        }

        private void SetVisibleChecked(bool value)
        {
            foreach (int i in VisibleMappingIndices())
                if (value) checkedPaths.Add(mappings[i].sourcePath);
                else checkedPaths.Remove(mappings[i].sourcePath);
        }

        private void SelectSingleRow(int index)
        {
            checkedPaths.Clear();
            selected = index;
            detailsScroll = Vector2.zero;
            Repaint();
        }

        private void SetRowChecked(int index, bool value)
        {
            if (value) { checkedPaths.Add(mappings[index].sourcePath); selected = index; }
            else checkedPaths.Remove(mappings[index].sourcePath);
            if (checkedPaths.Count == 1) selected = mappings.FindIndex(m => checkedPaths.Contains(m.sourcePath));
            detailsScroll = Vector2.zero;
        }

        private void DrawSelectionToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(checkedPaths.Count == 0 ? "Click a row to inspect" : checkedPaths.Count + " selected", EditorStyles.miniBoldLabel, GUILayout.Width(135));
                if (GUILayout.Button("Select visible", EditorStyles.toolbarButton, GUILayout.Width(90))) SetVisibleChecked(true);
                using (new EditorGUI.DisabledScope(checkedPaths.Count == 0))
                {
                    if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(45))) SelectSingleRow(selected);
                }
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawMultipleSelectionDetails()
        {
            var rows = mappings.Where(m => checkedPaths.Contains(m.sourcePath)).ToArray();
            detailsScroll = EditorGUILayout.BeginScrollView(detailsScroll);
            GUILayout.Space(6);
            GUILayout.Label(rows.Length + " transforms selected", cardTitleStyle);
            int skipped = rows.Count(m => m.skipped);
            GUILayout.Label((rows.Length - skipped) + " included  ·  " + skipped + " skipped", EditorStyles.miniBoldLabel);
            GUILayout.Space(8);
            GUILayout.Label("Actions below apply to every selected row.", EditorStyles.wordWrappedLabel);
            using (new EditorGUI.DisabledScope(skipped == rows.Length))
                if (GUILayout.Button("Skip selected", GUILayout.Height(28))) ApplyBulkSkip(true);
            using (new EditorGUI.DisabledScope(skipped == 0))
                if (GUILayout.Button("Include selected", GUILayout.Height(28))) ApplyBulkSkip(false);
            GUILayout.Space(8);
            EditorGUILayout.HelpBox("To change a target bone or movement settings, click one source row to edit that pair.", MessageType.None);
            GUILayout.Space(8);
            GUILayout.Label("Selected transforms", EditorStyles.miniBoldLabel);
            foreach (var row in rows)
                GUILayout.Label(new GUIContent(source.bones[source.indices[row.sourcePath]].name,
                    RetargetRig.DisplayPath(row.sourcePath)), EditorStyles.miniLabel);
            EditorGUILayout.EndScrollView();
        }

        private void ApplyBulkSkip(bool skip)
        {
            int changed = 0;
            foreach (var m in mappings.Where(m => checkedPaths.Contains(m.sourcePath) && m.skipped != skip))
            {
                m.skipped = skip;
                m.confirmed = false;
                changed++;
            }
            if (changed == 0) return;
            MarkDirty();
            int issues = mappings.Count(m => !m.skipped && Issue(m) != null);
            Notify(changed + (skip ? " transforms skipped." : " transforms included. Review and confirm their pairs.") +
                (issues > 0 ? " " + issues + " pairs need attention; check their parent mappings." : ""),
                issues > 0 ? MessageType.Warning : MessageType.Info);
        }

        private string ExportBlockReason
        {
            get
            {
                if (demo) return "Choose actual rigs and a clip to export.";
                if (EditorApplication.isPlayingOrWillChangePlaymode) return "Exit Play Mode to export.";
                if (!HasRigs) return "Choose both model assets.";
                if (sourceClip == null) return "Choose a source animation clip.";
                if (ClipReport.errors.Count > 0) return "Resolve the export blockers above.";
                int remaining = mappings.Count(m => !m.skipped && !m.confirmed);
                return remaining > 0 ? "Confirm " + remaining + " pairs before export." : null;
            }
        }

        private void DrawReviewSummary()
        {
            int confirmed = mappings.Count(m => !m.skipped && m.confirmed && Issue(m) == null);
            int skipped = mappings.Count(m => m.skipped);
            int remaining = mappings.Count - confirmed - skipped;
            GUILayout.Label("Review & export", titleStyle);
            GUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSummaryCard(confirmed.ToString(), "Confirmed pairs", Accent);
                DrawSummaryCard(remaining.ToString(), "Need review", remaining > 0 ? Amber : Accent);
                DrawSummaryCard(skipped.ToString(), "Skipped transforms", Blue);
            }
            if (remaining > 0)
            {
                GUILayout.Space(6);
                showMappingIssues = EditorGUILayout.Foldout(showMappingIssues, "Review " + remaining + " pairs", true, EditorStyles.foldoutHeader);
                if (showMappingIssues)
                {
                    DrawReviewActions();
                    for (int i = 0; i < mappings.Count; i++)
                    {
                        var m = mappings[i];
                        if (m.skipped || (m.confirmed && Issue(m) == null)) continue;
                        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                        {
                            GUILayout.Label(source.bones[i].name + " — " + (Issue(m) ?? "Awaiting confirmation"), EditorStyles.wordWrappedLabel);
                            if (GUILayout.Button("Inspect", GUILayout.Width(70)))
                            {
                                selected = i;
                                search = "";
                                needsAttentionOnly = false;
                                meshesOnly = false;
                                showMeshNodes |= source.bones[i].meshOnly;
                                checkedPaths.Clear();
                                step = 1;
                            }
                        }
                    }
                }
            }
        }

        private void DrawSummaryCard(string count, string label, Color color)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Space(6);
                var previous = GUI.contentColor;
                GUI.contentColor = color;
                GUILayout.Label(count, titleStyle);
                GUI.contentColor = previous;
                GUILayout.Label(label, EditorStyles.miniBoldLabel);
                GUILayout.Space(6);
            }
        }

        private void DrawExportNotices()
        {
            if (demo) return;
            var report = ClipReport;
            // Blocking errors stay visible. Mapping rows above provide the individual fixes.
            foreach (string error in report.errors.Take(3)) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (report.errors.Count > 3) GUILayout.Label("+ " + (report.errors.Count - 3) + " more blockers; inspect the flagged pairs.", EditorStyles.miniLabel);
            if (report.warnings.Count == 0) return;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Before you export", cardTitleStyle);
                if (report.excludedPaths.Count > 0)
                    GUILayout.Label("• " + report.excludedPaths.Count + " clip paths excluded — only included pairs are exported.", EditorStyles.wordWrappedLabel);
                if (report.warnings.Any(w => w.Contains("keep target rest positions")))
                    GUILayout.Label("• Some position tracks are held at the target rest pose; check root / hip movement.", EditorStyles.wordWrappedLabel);
                if (report.warnings.Any(w => w.Contains("different model asset")))
                    GUILayout.Label("• Clip and source model differ. Check their reference poses in motion preview.", EditorStyles.wordWrappedLabel);
                if (report.warnings.Any(w => w.Contains("unsupported tracks")))
                    GUILayout.Label("• Unsupported tracks, such as blendshapes or materials, will be omitted.", EditorStyles.wordWrappedLabel);
                showNoticeDetails = EditorGUILayout.Foldout(showNoticeDetails, "Details (" + report.warnings.Count + " notes)", true);
                if (showNoticeDetails)
                {
                    foreach (string warning in report.warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
                    if (report.excludedPaths.Count > 0)
                    {
                        showExcluded = EditorGUILayout.Foldout(showExcluded, "Excluded clip paths (" + report.excludedPaths.Count + ")", true);
                        if (showExcluded)
                            foreach (string path in report.excludedPaths) GUILayout.Label(RetargetRig.DisplayPath(path), EditorStyles.wordWrappedMiniLabel);
                    }
                }
            }
        }

        private void DrawExportFooter()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string reason = ExportBlockReason;
                var previous = GUI.contentColor;
                GUI.contentColor = reason == null ? Accent : Amber;
                GUILayout.Label(reason ?? "Ready to generate · saves a new .anim file", EditorStyles.boldLabel);
                GUI.contentColor = previous;
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Back to mapping", GUILayout.Height(34), GUILayout.Width(135))) step = 1;
                    using (new EditorGUI.DisabledScope(demo))
                        if (GUILayout.Button("Save preset", GUILayout.Height(34), GUILayout.Width(100))) SavePreset();
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(demo || PreviewReport.errors.Count != 0))
                        if (GUILayout.Button("Preview motion", GUILayout.Height(34), GUILayout.Width(135)) && BuildMotionPreview()) step = 1;
                    var background = GUI.backgroundColor;
                    GUI.backgroundColor = Accent;
                    using (new EditorGUI.DisabledScope(reason != null))
                        if (GUILayout.Button("Generate animation clip…", GUILayout.Height(38), GUILayout.Width(240))) GenerateWithDialog();
                    GUI.backgroundColor = background;
                }
            }
        }
    }
}
