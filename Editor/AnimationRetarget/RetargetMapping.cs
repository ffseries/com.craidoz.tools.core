using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Craidoz.Tools.Core.Editor
{
    [Serializable]
    internal sealed class RetargetBoneMapping
    {
        public string sourcePath;
        public string targetPath = "";
        public bool hasTarget;
        public string TargetPath { get => hasTarget ? targetPath : null; set { hasTarget = value != null; targetPath = value ?? ""; } }
        public bool confirmed;
        public bool skipped;
        public RetargetPositionMode positionMode;
        public float positionMultiplier = 1f;
        public bool transferScale;
        public Vector3 positionOffset;
        public Vector3 rotationOffset;
        public float rotationMultiplier = 1f;
        public Vector3 scaleMultiplier = Vector3.one;

        public bool ValidMovementSettings => Enum.IsDefined(typeof(RetargetPositionMode), positionMode) &&
            Finite(positionMultiplier) && positionMultiplier >= 0 && Finite(positionOffset) &&
            Finite(rotationOffset) && Finite(rotationMultiplier) && rotationMultiplier >= 0 &&
            Finite(scaleMultiplier) && scaleMultiplier.x > 0 && scaleMultiplier.y > 0 && scaleMultiplier.z > 0;

        public void ResetMovementSettings()
        {
            positionMode = RetargetPositionMode.KeepTargetRestPosition;
            positionMultiplier = rotationMultiplier = 1f;
            transferScale = false;
            positionOffset = rotationOffset = Vector3.zero;
            scaleMultiplier = Vector3.one;
        }

        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        private static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
    }

    internal enum RetargetPositionMode
    {
        KeepTargetRestPosition,
        TransferRestPoseOffset,
        ScaleRestPoseOffset
    }

    internal enum RetargetHierarchyMode
    {
        Strict,
        Flexible
    }

    [Serializable]
    internal sealed class RetargetMappingPreset
    {
        public int version = 1;
        public string sourceGuid;
        public string targetGuid;
        public RetargetHierarchyMode hierarchyMode;
        public List<RetargetBoneMapping> bones = new List<RetargetBoneMapping>();
    }

    // The UI reads a snapshot. It never changes the original model or scene transforms.
    internal sealed class RetargetRig
    {
        internal sealed class Bone
        {
            public string path;
            public string name;
            public int parent;
            public int depth;
            public Vector3 position;
            public bool meshOnly;
            public bool skinBone;
            public string Kind => parent < 0 ? "Model root" : meshOnly ? "Mesh (not a joint)" :
                skinBone ? "Skin bone" : "Rig transform";
        }

        public readonly List<Bone> bones = new List<Bone>();
        public readonly Dictionary<string, int> indices = new Dictionary<string, int>();
        public string error;

        public static RetargetRig Read(GameObject root)
        {
            var rig = new RetargetRig();
            if (root == null) return rig;
            var skinBones = new HashSet<Transform>(root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .SelectMany(renderer => renderer.bones).Where(b => b != null));
            var skeleton = new HashSet<Transform>(skinBones);
            foreach (var bone in skinBones)
                for (var ancestor = bone.parent; ancestor != null && ancestor.IsChildOf(root.transform); ancestor = ancestor.parent)
                    skeleton.Add(ancestor);
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                var path = AnimationUtility.CalculateTransformPath(transform, root.transform);
                if (transform != root.transform && (transform.name.Contains("/") || string.IsNullOrEmpty(transform.name)))
                {
                    rig.error = "A transform has an empty name or a '/' in its name. Use unique, valid animation paths.";
                    continue;
                }
                if (rig.indices.ContainsKey(path))
                {
                    rig.error = "Duplicate hierarchy path: " + path + ". Rename duplicate siblings before mapping.";
                    continue;
                }
                var parentPath = transform == root.transform ? null :
                    AnimationUtility.CalculateTransformPath(transform.parent, root.transform);
                int parent = -1;
                if (parentPath != null) rig.indices.TryGetValue(parentPath, out parent);
                rig.Add(path, transform.name, parent,
                    root.transform.InverseTransformPoint(transform.position));
                var entry = rig.bones[rig.bones.Count - 1];
                entry.skinBone = skinBones.Contains(transform);
                entry.meshOnly = transform != root.transform && !skeleton.Contains(transform) &&
                    transform.GetComponent<Renderer>() != null;
            }
            return rig;
        }

        public static RetargetRig Demo(bool target)
        {
            var rig = new RetargetRig();
            string[] names = target
                ? new[] { "Creature_B", "Bone001", "Bone002", "Bone003", "Bone023", "Bone024", "Bone041", "Bone042", "Bone052", "Bone053", "Bone061", "Bone062", "Bone070" }
                : new[] { "Source_Rig", "Hips", "Spine", "Head", "Foreleg_L", "Forefoot_L", "Foreleg_R", "Forefoot_R", "Hindleg_L", "Hindfoot_L", "Hindleg_R", "Hindfoot_R", "Tail" };
            int[] parents = { -1, 0, 1, 2, 2, 4, 2, 6, 1, 8, 1, 10, 1 };
            Vector3[] points = {
                new Vector3(0, 0, 0), new Vector3(0, 1.4f, -.55f),
                new Vector3(0, 1.55f, .55f), new Vector3(0, 2.1f, 1f),
                new Vector3(.45f, 1f, .65f), new Vector3(.45f, .1f, .8f),
                new Vector3(-.45f, 1f, .65f), new Vector3(-.45f, .1f, .8f),
                new Vector3(.45f, .85f, -.65f), new Vector3(.45f, .1f, -.7f),
                new Vector3(-.45f, .85f, -.65f), new Vector3(-.45f, .1f, -.7f),
                new Vector3(0, 1.6f, -1.3f)
            };
            for (int i = 0; i < names.Length; i++)
            {
                string path = parents[i] < 0 ? "" :
                    (rig.bones[parents[i]].path.Length == 0 ? "" : rig.bones[parents[i]].path + "/") + names[i];
                var p = points[i];
                if (target) p = Vector3.Scale(p, new Vector3(1.25f, .72f, 1.1f));
                rig.Add(path, names[i], parents[i], p);
            }
            return rig;
        }

        private void Add(string path, string name, int parent, Vector3 position)
        {
            indices.Add(path, bones.Count);
            bones.Add(new Bone { path = path, name = name, parent = parent,
                depth = parent < 0 ? 0 : bones[parent].depth + 1, position = position });
        }

        public static string DisplayPath(string path) => path == "" ? "(Model root)" : path;

        public static List<RetargetBoneMapping> CreateMappings(RetargetRig source)
        {
            return source.bones.Select(b => new RetargetBoneMapping { sourcePath = b.path, skipped = b.meshOnly }).ToList();
        }

        // Intentionally conservative: names are only suggestions, never anatomical proof.
        public static int Suggest(RetargetRig source, RetargetRig target, List<RetargetBoneMapping> mappings)
        {
            int count = 0;
            var used = new HashSet<string>(mappings.Where(m => !m.skipped && m.TargetPath != null).Select(m => m.TargetPath));
            foreach (var mapping in mappings)
            {
                if (mapping.skipped || mapping.TargetPath != null) continue;
                var bone = source.bones[source.indices[mapping.sourcePath]];
                // Root paths have the same meaning even when the model asset names differ.
                // Exact hierarchy paths also distinguish a skin bone from a similarly named mesh.
                if (target.indices.TryGetValue(bone.path, out int exact) &&
                    target.bones[exact].meshOnly == bone.meshOnly && !used.Contains(bone.path))
                {
                    mapping.TargetPath = bone.path;
                    mapping.confirmed = false;
                    used.Add(bone.path);
                    count++;
                    continue;
                }
                var candidates = target.bones.Where(b =>
                    b.meshOnly == bone.meshOnly && string.Equals(b.name, bone.name, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (candidates.Length != 1 || used.Contains(candidates[0].path)) continue;
                mapping.TargetPath = candidates[0].path;
                mapping.confirmed = false;
                used.Add(mapping.TargetPath);
                count++;
            }
            return count;
        }

        public static string Issue(RetargetBoneMapping mapping, RetargetRig source, RetargetRig target,
            List<RetargetBoneMapping> mappings, RetargetHierarchyMode hierarchyMode = RetargetHierarchyMode.Strict)
        {
            if (mapping.skipped) return null;
            if (mapping.TargetPath == null) return "Choose a target bone, or explicitly skip this source.";
            if (!target.indices.ContainsKey(mapping.TargetPath)) return "The target path no longer exists.";
            if (mappings.Count(m => !m.skipped && m.TargetPath == mapping.TargetPath) > 1)
                return "This target is used by more than one source bone.";
            int sourceParent = source.bones[source.indices[mapping.sourcePath]].parent;
            int targetParent = target.bones[target.indices[mapping.TargetPath]].parent;
            if (sourceParent < 0)
                return targetParent < 0 ? null : "The model root must map to the target model root.";
            if (targetParent < 0) return "Only the source model root can map to the target model root.";
            if (hierarchyMode == RetargetHierarchyMode.Flexible) return null;
            var parentMapping = mappings.FirstOrDefault(m => m.sourcePath == source.bones[sourceParent].path);
            if (parentMapping == null || parentMapping.skipped || parentMapping.TargetPath == null)
                return "Map the source parent first, or use Flexible hierarchy for different chains.";
            if (targetParent < 0 || target.bones[targetParent].path != parentMapping.TargetPath)
                return "The mapped parent does not match the target parent. Check this chain or use Flexible hierarchy.";
            return null;
        }
    }
}
