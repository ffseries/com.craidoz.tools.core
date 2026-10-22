using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Craidoz.Tools.Core.Editor
{
    /// <summary>Editor-only retargeting for paired Generic Transform hierarchies.</summary>
    internal sealed class RetargetClipBaker : IDisposable
    {
        internal sealed class Report
        {
            public readonly List<string> errors = new List<string>();
            public readonly List<string> warnings = new List<string>();
            public readonly List<string> excludedPaths = new List<string>();
            public int animatedPairs;
            public int mappedPairs, omittedPairs;
        }

        private sealed class Pose
        {
            public Transform transform;
            public Vector3 position, scale;
            public Quaternion rotation, parentRotation;
            public Matrix4x4 parentMatrix;
            public void Reset()
            {
                transform.localPosition = position;
                transform.localRotation = rotation;
                transform.localScale = scale;
            }
        }

        private sealed class Pair
        {
            public RetargetBoneMapping mapping;
            public Pose source, target;
            public Quaternion basis;
            public Matrix4x4 translationBasis;
        }

        private Scene scene;
        private GameObject sourceRoot, targetRoot;
        private AnimationClip samplingClip;
        private readonly AnimationClip originalClip;
        private readonly bool previewOnly;
        private Pose[] sourcePose, targetPose;
        private Pair[] pairs;
        private Matrix4x4 sourceFrame, targetFrame;
        public RetargetRig SourcePreview { get; private set; }
        public RetargetRig TargetPreview { get; private set; }
        public float Length => originalClip.length;
        public bool IsDisposed { get; private set; }

        // Preview can evaluate individual pairs while their ancestors remain at rest.
        // Ambiguous, missing or invalid assignments must never compete for a target.
        private static RetargetBoneMapping[] PreviewMappings(RetargetRig source, RetargetRig target,
            List<RetargetBoneMapping> mappings)
        {
            return mappings.Where(m => !m.skipped && m.TargetPath != null &&
                source.indices.ContainsKey(m.sourcePath) && target.indices.ContainsKey(m.TargetPath) &&
                mappings.Count(other => !other.skipped && other.TargetPath == m.TargetPath) == 1 &&
                (source.bones[source.indices[m.sourcePath]].parent < 0) == (target.bones[target.indices[m.TargetPath]].parent < 0) &&
                Enum.IsDefined(typeof(RetargetPositionMode), m.positionMode) && Finite(m.positionMultiplier) && m.positionMultiplier >= 0).ToArray();
        }

        internal static bool Supported(EditorCurveBinding binding)
        {
            if (binding.type != typeof(Transform)) return false;
            string property = binding.propertyName;
            return property.StartsWith("m_LocalPosition.", StringComparison.Ordinal) ||
                   property.StartsWith("m_LocalRotation.", StringComparison.Ordinal) ||
                   property.StartsWith("m_LocalScale.", StringComparison.Ordinal) ||
                   property.StartsWith("localEulerAnglesRaw.", StringComparison.Ordinal) ||
                   property.StartsWith("localEulerAnglesBaked.", StringComparison.Ordinal) ||
                   property.StartsWith("localEulerAngles.", StringComparison.Ordinal);
        }

        public static Report Inspect(GameObject sourceModel, GameObject targetModel, AnimationClip clip,
            RetargetRig source, RetargetRig target, List<RetargetBoneMapping> mappings, bool requireConfirmation, bool partialPreview = false)
        {
            var report = new Report();
            if (sourceModel == null || targetModel == null) report.errors.Add("Choose both model assets.");
            if (!string.IsNullOrEmpty(source.error)) report.errors.Add(source.error);
            if (!string.IsNullOrEmpty(target.error)) report.errors.Add(target.error);
            if (clip == null) report.errors.Add("Choose a source animation clip in step 1.");
            else if (clip.humanMotion) report.errors.Add("Humanoid muscle clips are not supported. Use a Generic Transform clip.");
            else if (clip.length <= 0 || !Finite(clip.length)) report.errors.Add("The source clip must have a finite, non-zero duration.");
            if (mappings.GroupBy(m => m.sourcePath).Any(group => group.Count() != 1) ||
                source.bones.Any(b => !mappings.Any(m => m.sourcePath == b.path)))
                report.errors.Add("The source hierarchy changed or contains duplicate mapping rows. Refresh the rigs and review the mapping again.");
            var included = partialPreview ? PreviewMappings(source, target, mappings) : mappings.Where(m => !m.skipped).ToArray();
            report.mappedPairs = included.Length;
            report.omittedPairs = mappings.Count(m => !m.skipped) - included.Length;
            if (!partialPreview && included.Length == 0) report.errors.Add("Include at least one mapped transform.");
            foreach (var m in included)
            {
                if (!source.indices.ContainsKey(m.sourcePath)) { report.errors.Add("Source path no longer exists: " + m.sourcePath); continue; }
                string issue = partialPreview ? null : RetargetRig.Issue(m, source, target, mappings);
                if (issue != null) report.errors.Add(RetargetRig.DisplayPath(m.sourcePath) + ": " + issue);
                else if (!partialPreview && requireConfirmation && !m.confirmed) report.errors.Add("Confirm pair: " + RetargetRig.DisplayPath(m.sourcePath));
                if (!Enum.IsDefined(typeof(RetargetPositionMode), m.positionMode) || !Finite(m.positionMultiplier) || m.positionMultiplier < 0)
                    report.errors.Add("Invalid movement settings: " + m.sourcePath);
            }
            if (clip == null) return report;
            var bindings = AnimationUtility.GetCurveBindings(clip);
            var supported = bindings.Where(Supported).ToArray();
            var animated = new HashSet<string>(supported.Select(b => b.path));
            report.animatedPairs = included.Count(m => animated.Contains(m.sourcePath));
            if (partialPreview ? !animated.Any(source.indices.ContainsKey) : report.animatedPairs == 0)
                report.errors.Add("No clip Transform paths match " + (partialPreview ? "the source rig" : "the included source bones") + ". Check the source model and clip root.");
            var mappedPaths = new HashSet<string>(included.Select(m => m.sourcePath));
            report.excludedPaths.AddRange(animated.Where(p => !source.indices.ContainsKey(p) || !mappedPaths.Contains(p)).OrderBy(p => p));
            if (report.excludedPaths.Count > 0)
                report.warnings.Add(report.excludedPaths.Count + " animated paths will be excluded (outside this source rig or explicitly skipped).");
            int other = bindings.Count(b => !Supported(b)) + AnimationUtility.GetObjectReferenceCurveBindings(clip).Length;
            if (other > 0) report.warnings.Add(other + " non-Transform / unsupported tracks are excluded, including any blendshapes or material animation.");
            if (bindings.Any(b => b.type == typeof(Animator)))
                report.errors.Add("Animator root/muscle curves require a different retargeting method. Bake them to Transform curves first.");
            int held = included.Count(m => m.positionMode == RetargetPositionMode.KeepTargetRestPosition &&
                supported.Any(b => b.path == m.sourcePath && b.propertyName.StartsWith("m_LocalPosition.")));
            if (held > 0) report.warnings.Add(held + " mapped transforms contain position keys but are set to keep target rest positions. Enable offset transfer where movement is needed (for example root/hips).");
            if (sourceModel != null && targetModel != null)
            {
                CheckRestScales(sourceModel, included.Select(m => m.sourcePath), report);
                CheckRestScales(targetModel, included.Where(m => m.TargetPath != null).Select(m => m.TargetPath), report);
                var clipModel = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(clip));
                if (clipModel != null && clipModel != sourceModel)
                    report.warnings.Add("This clip comes from a different model asset. Matching paths do not guarantee matching reference poses; inspect motion before using the result.");
            }
            return report;
        }

        private static void CheckRestScales(GameObject model, IEnumerable<string> paths, Report report)
        {
            var visited = new HashSet<Transform>();
            foreach (var path in paths)
            {
                var tr = path == "" ? model.transform : model.transform.Find(path);
                while (tr != null && visited.Add(tr))
                {
                    var s = tr.localScale;
                    if (!Finite(s.x) || !Finite(s.y) || !Finite(s.z) || s.x <= 0 || s.y <= 0 || s.z <= 0 ||
                        Mathf.Abs(s.x - s.y) > .0001f || Mathf.Abs(s.y - s.z) > .0001f)
                        report.errors.Add(model.name + "/" + tr.name + ": reference scales must be positive and uniform. Apply non-uniform/mirrored rest scales in your modeling tool first.");
                    tr = tr == model.transform ? null : tr.parent;
                }
            }
        }

        public RetargetClipBaker(GameObject sourceModel, GameObject targetModel, AnimationClip clip,
            List<RetargetBoneMapping> mappings, bool partialPreview = false)
        {
            originalClip = clip;
            previewOnly = partialPreview;
            var source = RetargetRig.Read(sourceModel);
            var target = RetargetRig.Read(targetModel);
            var report = Inspect(sourceModel, targetModel, clip, source, target, mappings, false, partialPreview);
            if (report.errors.Count > 0) throw new InvalidOperationException(string.Join("\n", report.errors));
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                sourcePose = CloneHierarchy(sourceModel, source, out sourceRoot);
                targetPose = CloneHierarchy(targetModel, target, out targetRoot);
                sourceFrame = sourceRoot.transform.worldToLocalMatrix;
                targetFrame = targetRoot.transform.worldToLocalMatrix;
                SourcePreview = RetargetRig.Read(sourceRoot);
                TargetPreview = RetargetRig.Read(targetRoot);
                // The clone intentionally contains only Transforms; copy the classification.
                for (int i = 0; i < source.bones.Count; i++)
                {
                    SourcePreview.bones[i].meshOnly = source.bones[i].meshOnly;
                    SourcePreview.bones[i].skinBone = source.bones[i].skinBone;
                }
                for (int i = 0; i < target.bones.Count; i++)
                {
                    TargetPreview.bones[i].meshOnly = target.bones[i].meshOnly;
                    TargetPreview.bones[i].skinBone = target.bones[i].skinBone;
                }
                var included = partialPreview ? PreviewMappings(source, target, mappings) : mappings.Where(m => !m.skipped).ToArray();
                pairs = included.Select(m => {
                    var sp = sourcePose[source.indices[m.sourcePath]];
                    var tp = targetPose[target.indices[m.TargetPath]];
                    return new Pair { mapping = JsonUtility.FromJson<RetargetBoneMapping>(JsonUtility.ToJson(m)),
                        source = sp, target = tp,
                        basis = Quaternion.Inverse(tp.parentRotation) * sp.parentRotation,
                        translationBasis = tp.parentMatrix.inverse * sp.parentMatrix };
                }).ToArray();
                samplingClip = new AnimationClip { legacy = true, name = "Retarget sampling (temporary)", hideFlags = HideFlags.HideAndDontSave };
                var bindings = AnimationUtility.GetCurveBindings(clip).Where(b => Supported(b) && source.indices.ContainsKey(b.path)).ToArray();
                AnimationUtility.SetEditorCurves(samplingClip, bindings, bindings.Select(b => AnimationUtility.GetEditorCurve(clip, b)).ToArray());
                Sample(0);
            }
            catch { Dispose(); throw; }
        }

        private Pose[] CloneHierarchy(GameObject model, RetargetRig rig, out GameObject root)
        {
            root = null;
            var result = new Pose[rig.bones.Count];
            for (int i = 0; i < result.Length; i++)
            {
                var bone = rig.bones[i];
                var original = bone.path == "" ? model.transform : model.transform.Find(bone.path);
                var go = new GameObject(original.name);
                SceneManager.MoveGameObjectToScene(go, scene);
                go.hideFlags = HideFlags.HideAndDontSave;
                if (bone.parent >= 0) go.transform.SetParent(result[bone.parent].transform, false);
                else root = go;
                go.transform.localPosition = original.localPosition;
                go.transform.localRotation = original.localRotation;
                go.transform.localScale = original.localScale;
                result[i] = new Pose { transform = go.transform, position = original.localPosition,
                    rotation = original.localRotation, scale = original.localScale,
                    parentRotation = go.transform.parent == null ? Quaternion.identity : go.transform.parent.rotation,
                    parentMatrix = go.transform.parent == null ? Matrix4x4.identity : go.transform.parent.localToWorldMatrix };
            }
            return result;
        }

        public void Sample(float time)
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(RetargetClipBaker));
            foreach (var pose in sourcePose) pose.Reset();
            foreach (var pose in targetPose) pose.Reset();
            samplingClip.SampleAnimation(sourceRoot, Mathf.Clamp(time, 0, Length));
            foreach (var pair in pairs)
            {
                var src = pair.source;
                var dst = pair.target;
                var delta = src.transform.localRotation * Quaternion.Inverse(src.rotation);
                dst.transform.localRotation = (pair.basis * delta * Quaternion.Inverse(pair.basis) * dst.rotation).normalized;
                if (pair.mapping.positionMode != RetargetPositionMode.KeepTargetRestPosition)
                {
                    float multiplier = pair.mapping.positionMode == RetargetPositionMode.ScaleRestPoseOffset ? pair.mapping.positionMultiplier : 1f;
                    dst.transform.localPosition = dst.position +
                        pair.translationBasis.MultiplyVector(src.transform.localPosition - src.position) * multiplier;
                }
                if (pair.mapping.transferScale)
                {
                    var s = src.transform.localScale;
                    dst.transform.localScale = Vector3.Scale(dst.scale, new Vector3(s.x / src.scale.x, s.y / src.scale.y, s.z / src.scale.z));
                }
            }
            UpdateSkeleton(SourcePreview, sourcePose, sourceFrame);
            UpdateSkeleton(TargetPreview, targetPose, targetFrame);
        }

        private static void UpdateSkeleton(RetargetRig rig, Pose[] poses, Matrix4x4 frame)
        {
            for (int i = 0; i < poses.Length; i++) rig.bones[i].position = frame.MultiplyPoint3x4(poses[i].transform.position);
        }

        public AnimationClip Bake(float fps, bool loop, bool copyEvents, Func<float, bool> cancel = null)
        {
            if (previewOnly) throw new InvalidOperationException("Partial preview cannot be exported. Review and confirm the mapping before generating a clip.");
            if (!Finite(fps) || fps < 1 || fps > 120) throw new ArgumentOutOfRangeException(nameof(fps));
            if (Length * fps > 36000)
                throw new InvalidOperationException("This bake is too large. Use a shorter clip or lower sample rate (maximum 36000 intervals).");
            int intervals = Math.Max(1, Mathf.CeilToInt(Length * fps));
            if ((long)(intervals + 1) * pairs.Length * 10 > 5000000)
                throw new InvalidOperationException("This bake is too large. Use a shorter clip or lower sample rate (maximum 5 million channel samples).");
            int frames = intervals + 1;
            var values = new float[pairs.Length * 10][];
            for (int i = 0; i < values.Length; i++) values[i] = new float[frames];
            var previous = new Quaternion[pairs.Length];
            var times = new float[frames];
            for (int frame = 0; frame < frames; frame++)
            {
                if (frame % 30 == 0 && cancel != null && cancel(frame / (float)intervals))
                    throw new OperationCanceledException("Animation export cancelled.");
                // Distribute samples evenly, including the exact endpoint, to avoid a
                // nearly duplicate final key when duration * fps rounds above an integer.
                float time = frame == intervals ? Length : Length * (frame / (float)intervals);
                times[frame] = time;
                Sample(time);
                for (int p = 0; p < pairs.Length; p++)
                {
                    var tr = pairs[p].target.transform;
                    var q = tr.localRotation;
                    if (frame > 0 && Quaternion.Dot(previous[p], q) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                    previous[p] = q;
                    var pos = tr.localPosition;
                    var scale = tr.localScale;
                    float[] channels = { pos.x, pos.y, pos.z, q.x, q.y, q.z, q.w, scale.x, scale.y, scale.z };
                    for (int c = 0; c < 10; c++)
                    {
                        if (!Finite(channels[c])) throw new InvalidOperationException("Non-finite animation value on " + pairs[p].mapping.TargetPath);
                        values[p * 10 + c][frame] = channels[c];
                    }
                }
            }
            var result = new AnimationClip { name = originalClip.name + "_Retargeted", frameRate = fps, legacy = false };
            try
            {
                string[] properties = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z",
                    "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w",
                    "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" };
                var bindings = new List<EditorCurveBinding>();
                var curves = new List<AnimationCurve>();
                for (int p = 0; p < pairs.Length; p++)
                    for (int c = 0; c < 10; c++)
                    {
                        bindings.Add(EditorCurveBinding.FloatCurve(pairs[p].mapping.TargetPath, typeof(Transform), properties[c]));
                        curves.Add(MakeCurve(times, values[p * 10 + c]));
                    }
                AnimationUtility.SetEditorCurves(result, bindings.ToArray(), curves.ToArray());
                result.EnsureQuaternionContinuity();
                var settings = AnimationUtility.GetAnimationClipSettings(result);
                settings.loopTime = loop;
                settings.startTime = 0;
                settings.stopTime = Length;
                AnimationUtility.SetAnimationClipSettings(result, settings);
                if (copyEvents) AnimationUtility.SetAnimationEvents(result, AnimationUtility.GetAnimationEvents(originalClip));
                return result;
            }
            catch { UnityEngine.Object.DestroyImmediate(result); throw; }
        }

        private static AnimationCurve MakeCurve(float[] time, float[] value)
        {
            if (value.All(v => Mathf.Abs(v - value[0]) < .000001f))
                return AnimationCurve.Linear(0, value[0], time[time.Length - 1], value[0]);
            var keys = new Keyframe[time.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                float left = i == 0 ? 0 : (value[i] - value[i - 1]) / (time[i] - time[i - 1]);
                float right = i == keys.Length - 1 ? 0 : (value[i + 1] - value[i]) / (time[i + 1] - time[i]);
                keys[i] = new Keyframe(time[i], value[i], left, right);
            }
            return new AnimationCurve(keys);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            if (samplingClip != null) UnityEngine.Object.DestroyImmediate(samplingClip);
            if (sourceRoot != null) UnityEngine.Object.DestroyImmediate(sourceRoot);
            if (targetRoot != null) UnityEngine.Object.DestroyImmediate(targetRoot);
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
