using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Craidoz.Tools.Core.Editor
{
    internal sealed partial class AnimationRetargetWindow
    {
        [SerializeField] private bool meshClassificationApplied;
        [SerializeField] private bool showMeshNodes;
        [SerializeField] private int sampleRate = 30;
        [SerializeField] private bool loopOutput;
        [SerializeField] private bool loopPreview = true;
        [SerializeField] private bool copyEvents = true;
        [SerializeField] private AnimationClip lastExport;
        private RetargetClipBaker motionPreview;
        private RetargetClipBaker.Report clipReport;
        private RetargetClipBaker.Report previewReport;
        private float previewTime;
        private bool previewPlaying;
        private double lastPreviewUpdate;
        private bool showExcluded;
        private int speedIndex = 2;
        private static readonly float[] PreviewSpeeds = { .25f, .5f, 1f, 2f };

        private void OnRetargetProjectChanged() { Rebuild(false); Repaint(); }

        private void InvalidatePreview()
        {
            previewPlaying = false;
            if (motionPreview != null) motionPreview.Dispose();
            motionPreview = null;
            clipReport = null;
            previewReport = null;
        }

        private RetargetClipBaker.Report ClipReport
        {
            get
            {
                if (clipReport == null)
                    clipReport = RetargetClipBaker.Inspect(sourceModel, targetModel, sourceClip, source, target, mappings, false);
                return clipReport;
            }
        }

        private RetargetClipBaker.Report PreviewReport => previewReport ?? (previewReport =
            RetargetClipBaker.Inspect(sourceModel, targetModel, sourceClip, source, target, mappings, false, true));

        private void DrawSourceClipField()
        {
            var clip = (AnimationClip)EditorGUILayout.ObjectField(
                new GUIContent("Source animation clip", "A Generic Transform clip authored for this source hierarchy."),
                sourceClip, typeof(AnimationClip), false);
            if (clip == sourceClip) return;
            sourceClip = clip;
            if (clip != null)
            {
                sampleRate = Mathf.Clamp(Mathf.RoundToInt(clip.frameRate), 1, 120);
                loopOutput = AnimationUtility.GetAnimationClipSettings(clip).loopTime;
            }
            previewTime = 0;
            InvalidatePreview();
        }

        private bool BuildMotionPreview()
        {
            InvalidatePreview();
            try
            {
                if (demo) throw new InvalidOperationException("Exit demo and choose actual model assets and a clip.");
                motionPreview = new RetargetClipBaker(sourceModel, targetModel, sourceClip, mappings, true);
                previewTime = Mathf.Clamp(previewTime, 0, motionPreview.Length);
                motionPreview.Sample(previewTime);
                lastPreviewUpdate = EditorApplication.timeSinceStartup;
                status = "";
                return true;
            }
            catch (Exception exception)
            {
                InvalidatePreview();
                Notify(exception.Message, MessageType.Error);
                return false;
            }
        }

        private void UpdateMotionPreview()
        {
            if (!previewPlaying || motionPreview == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            double now = EditorApplication.timeSinceStartup;
            float delta = (float)Math.Min(.1, now - lastPreviewUpdate);
            lastPreviewUpdate = now;
            previewTime += delta * PreviewSpeeds[speedIndex];
            if (previewTime > motionPreview.Length)
            {
                if (loopPreview) previewTime %= motionPreview.Length;
                else { previewTime = motionPreview.Length; previewPlaying = false; }
            }
            try { motionPreview.Sample(previewTime); }
            catch (Exception exception) { InvalidatePreview(); Notify(exception.Message, MessageType.Error); }
            Repaint();
        }

        private void DrawMotionControls()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(demo || sourceClip == null || !HasRigs || PreviewReport.errors.Count != 0))
                {
                    if (GUILayout.Button(motionPreview == null ? "Build motion preview" : "Rebuild preview", EditorStyles.toolbarButton, GUILayout.Width(140)))
                        BuildMotionPreview();
                    if (GUILayout.Button(previewPlaying ? "Pause" : "Play", EditorStyles.toolbarButton, GUILayout.Width(50)))
                    {
                        if (motionPreview != null || BuildMotionPreview())
                        {
                            if (!previewPlaying && previewTime >= motionPreview.Length) previewTime = 0;
                            previewPlaying = !previewPlaying;
                            lastPreviewUpdate = EditorApplication.timeSinceStartup;
                        }
                    }
                }
                using (new EditorGUI.DisabledScope(motionPreview == null))
                {
                    EditorGUI.BeginChangeCheck();
                    float time = GUILayout.HorizontalSlider(previewTime, 0, sourceClip == null ? 1 : sourceClip.length);
                    if (EditorGUI.EndChangeCheck() && motionPreview != null)
                    {
                        previewPlaying = false;
                        previewTime = time;
                        motionPreview.Sample(previewTime);
                        Repaint();
                    }
                }
                GUILayout.Label(previewTime.ToString("0.00") + " / " + (sourceClip == null ? "—" : sourceClip.length.ToString("0.00")) + " s",
                    EditorStyles.miniLabel, GUILayout.Width(100));
                speedIndex = EditorGUILayout.Popup(speedIndex, new[] { "0.25x", "0.5x", "1x", "2x" }, GUILayout.Width(60));
                loopPreview = GUILayout.Toggle(loopPreview, new GUIContent("Loop preview", "Replay in this window only. Does not change the exported clip."), EditorStyles.toolbarButton, GUILayout.Width(95));
                if (GUILayout.Button("Rest pose", EditorStyles.toolbarButton, GUILayout.Width(70))) InvalidatePreview();
            }
            if (!demo && HasRigs && sourceClip != null)
            {
                var report = PreviewReport;
                if (report.errors.Count > 0) EditorGUILayout.HelpBox(report.errors[0], MessageType.Warning);
                else GUILayout.Label("   Preview: " + report.mappedPairs + " mapped  ·  " + report.omittedPairs +
                    " unmapped / invalid  ·  Unmapped target joints keep their local rest pose and follow their parents.", EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawReviewActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Suggest remaining pairs")) { RetargetRig.Suggest(source, target, mappings); MarkDirty(); }
                if (GUILayout.Button("Confirm all valid pairs"))
                {
                    foreach (var m in mappings.Where(m => !m.skipped && Issue(m) == null)) m.confirmed = true;
                    MarkDirty();
                }
                if (GUILayout.Button("Skip unmapped extras…"))
                {
                    var extras = mappings.Where(m => !m.skipped && m.TargetPath == null && m.sourcePath != "").ToArray();
                    if (extras.Length > 0 && EditorUtility.DisplayDialog("Skip unmapped transforms?",
                        "These source transforms will not be exported:\n\n" +
                        string.Join("\n", extras.Select(m => m.sourcePath)) +
                        "\n\nTheir animation will be omitted. A mapped child still requires its parent.", "Skip listed transforms", "Cancel"))
                    {
                        foreach (var m in extras) { m.skipped = true; m.confirmed = false; }
                        MarkDirty();
                    }
                }
            }
        }

        private void DrawGenerationPanel()
        {
            GUILayout.Label("Output clip", cardTitleStyle);
            DrawSourceClipField();
            EditorGUILayout.ObjectField("Target character", targetModel, typeof(GameObject), false);
            if (sourceClip != null)
                GUILayout.Label(sourceClip.length.ToString("0.00") + " seconds  ·  " + sampleRate + " samples/sec  ·  Generic animation", EditorStyles.miniLabel);
            GUILayout.Space(6);
            loopOutput = EditorGUILayout.Toggle(new GUIContent("Loop exported clip", "Sets Loop Time on the new .anim. Does not change preview playback or make the end pose match the start pose."), loopOutput);
            GUILayout.Label(loopOutput ? "Repeats in the Animator. Check that the start and end poses join smoothly." :
                "One pass, unless your Animator or playback code repeats it.", EditorStyles.wordWrappedMiniLabel);
            showExportDetails = EditorGUILayout.Foldout(showExportDetails, "Advanced export settings", true);
            if (showExportDetails)
            {
                sampleRate = EditorGUILayout.IntSlider("Sample rate", sampleRate, 1, 120);
                copyEvents = EditorGUILayout.Toggle("Copy animation events", copyEvents);
                GUILayout.Label("Generic Transform curves only. No IK / foot locking, Humanoid muscles or blendshape retargeting. Preview motion to check pose compatibility.", EditorStyles.wordWrappedMiniLabel);
            }
            if (demo)
            {
                EditorGUILayout.HelpBox("Choose actual model assets and an animation clip to generate a .anim file.", MessageType.Info);
                return;
            }
            if (lastExport != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.ObjectField("Last exported clip", lastExport, typeof(AnimationClip), false);
                    if (GUILayout.Button("Show", GUILayout.Width(55))) { Selection.activeObject = lastExport; EditorGUIUtility.PingObject(lastExport); }
                }
            }
        }

        private void GenerateWithDialog()
        {
            string name = targetModel.name + "_" + sourceClip.name + "_Retargeted";
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            string path = EditorUtility.SaveFilePanelInProject("Save retargeted animation", name, "anim",
                "Choose an output path. An existing clip will receive a new filename instead of being replaced.");
            if (string.IsNullOrEmpty(path)) return;
            try { GenerateAtPath(path); }
            catch (OperationCanceledException) { Notify("Export cancelled. Original assets were not changed.", MessageType.Info); }
            catch (Exception exception) { Notify(exception.Message, MessageType.Error); }
        }

        // Shared by the UI and editor validation. Always creates a new asset.
        internal string GenerateAtPath(string path)
        {
            if (demo || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Export requires actual rigs in Edit Mode.");
            path = path.Replace('\\', '/');
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.Split('/').Any(p => p == ".." || p == ".") ||
                !path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase) ||
                !AssetDatabase.IsValidFolder(Path.GetDirectoryName(path).Replace('\\', '/')))
                throw new ArgumentException("Choose a .anim path in an existing Assets folder.");
            // Re-read assets to avoid exporting against a stale mapping snapshot.
            var freshSource = RetargetRig.Read(sourceModel);
            var freshTarget = RetargetRig.Read(targetModel);
            var report = RetargetClipBaker.Inspect(sourceModel, targetModel, sourceClip, freshSource, freshTarget, mappings, true);
            if (report.errors.Count > 0) throw new InvalidOperationException(string.Join("\n", report.errors));
            previewPlaying = false;
            AnimationClip output = null;
            try
            {
                using (var baker = new RetargetClipBaker(sourceModel, targetModel, sourceClip, mappings))
                    output = baker.Bake(sampleRate, loopOutput, copyEvents, progress =>
                        EditorUtility.DisplayCancelableProgressBar("Retarget animation", "Sampling and converting paired bones…", progress));
                string unique = AssetDatabase.GenerateUniqueAssetPath(path);
                output.name = Path.GetFileNameWithoutExtension(unique);
                AssetDatabase.CreateAsset(output, unique);
                AssetDatabase.SaveAssets();
                lastExport = output;
                EditorGUIUtility.PingObject(output);
                Notify("Created " + unique + " · " + report.animatedPairs + " animated pairs; " +
                    report.excludedPaths.Count + " clip paths excluded. Review the motion on the target before production use.", MessageType.Info);
                return unique;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (output != null && !AssetDatabase.Contains(output)) UnityEngine.Object.DestroyImmediate(output);
            }
        }
    }
}
