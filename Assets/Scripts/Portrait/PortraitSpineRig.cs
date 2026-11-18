using System.Collections.Generic;
using System.IO;
using Spine;
using UnityEngine;

namespace PotionShop.Portraits
{
    internal sealed class PortraitSpineRig
    {
        private readonly Skeleton skeleton;
        private readonly Spine.AnimationState animation;
        private readonly Slot blinkSlot;
        private readonly PortraitSkinnedImage baseImage;
        private readonly MeshBinding[] meshes;
        private readonly BoneFollower[] followers;

        private Rect Bounds { get; }
        public bool EyesClosed => blinkSlot.AppliedPose.Attachment != null;

        public PortraitSpineRig(TextAsset json, PortraitLayers layers)
        {
            // Unity's portrait Images provide the textures and tint for the animated geometry.
            using var reader = new StringReader(json.text);
            var data = new SkeletonJson(new AtlasAttachmentLoader(true)).ReadSkeletonData(reader);
            var idle = data.FindAnimation("idle") ?? throw new InvalidDataException("Missing Spine animation 'idle'.");
            if (!(data.Width > 0f && data.Height > 0f))
                throw new InvalidDataException("The Spine rig requires non-zero portrait bounds.");
            baseImage = layers.baseImage as PortraitSkinnedImage;
            if (baseImage == null || baseImage.sprite == null)
                throw new InvalidDataException("The Spine rig requires a PortraitSkinnedImage with a base sprite.");

            Bounds = new Rect(data.X, data.Y, data.Width, data.Height);
            skeleton = new Skeleton(data);
            skeleton.UpdateWorldTransform(Spine.Physics.None);
            var baseSlot = RequireSlot("base");
            var baseMesh = baseSlot.AppliedPose.Attachment as MeshAttachment
                ?? throw new InvalidDataException("Spine slot 'base' requires a setup mesh.");
            blinkSlot = RequireSlot("eye_closed");
            var irisBone = RequireSlot("iris").Bone;
            var meshList = new List<MeshBinding>(3) { new(baseImage, baseSlot, baseMesh, Bounds) };
            var followersList = new List<BoneFollower>(4);
            AddHair(meshList, followersList, layers.hairBack, RequireSlot("hair_back"));
            AddHair(meshList, followersList, layers.hairFront, RequireSlot("hair_front"));
            AddFollower(followersList, layers.eyeClosed, blinkSlot.Bone);
            if (layers.irisMask != null)
                AddFollower(followersList, layers.irisMask, irisBone);
            else
            {
                AddFollower(followersList, layers.irisLeft, irisBone);
                AddFollower(followersList, layers.irisRight, irisBone);
            }
            meshes = meshList.ToArray();
            followers = followersList.ToArray();

            animation = new Spine.AnimationState(new AnimationStateData(data));
            animation.SetAnimation(0, idle, true);
            Advance(0f);
            foreach (var mesh in meshes)
                mesh.Image.BindSpine(mesh);
            UpdateFollowers();
        }

        private Slot RequireSlot(string name) => skeleton.FindSlot(name)
            ?? throw new InvalidDataException($"Missing Spine slot '{name}'.");

        private void AddHair(List<MeshBinding> meshList, List<BoneFollower> followersList,
            UnityEngine.UI.Image image, Slot slot)
        {
            if (image == null) return;
            if (slot.AppliedPose.Attachment is MeshAttachment mesh)
            {
                if (image is not PortraitSkinnedImage skinnedImage)
                    throw new InvalidDataException($"Spine slot '{slot.Data.Name}' requires a PortraitSkinnedImage.");
                meshList.Add(new MeshBinding(skinnedImage, slot, mesh, Bounds));
            }
            else
                AddFollower(followersList, image, slot.Bone);
        }

        private static void AddFollower(List<BoneFollower> followersList,
            UnityEngine.UI.Image image, Bone bone)
        {
            if (image != null)
                followersList.Add(new BoneFollower(image.rectTransform, bone));
        }

        public void Advance(float deltaTime)
        {
            animation.Update(deltaTime);
            animation.Apply(skeleton);
            skeleton.UpdateWorldTransform(Spine.Physics.None);
            foreach (var mesh in meshes)
                mesh.UpdateVertices(skeleton);
        }

        public void UpdateFollowers()
        {
            var rect = baseImage.GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            foreach (var follower in followers)
                follower.Apply(baseImage.rectTransform, rect, Bounds);
        }

        public void Clear()
        {
            foreach (var mesh in meshes)
                if (mesh.Image != null)
                    mesh.Image.BindSpine(null);
            foreach (var follower in followers)
                follower.Restore();
        }

        internal sealed class MeshBinding
        {
            private readonly Slot slot;
            private readonly MeshAttachment mesh;

            public PortraitSkinnedImage Image { get; }
            public Rect Bounds { get; }
            public float[] Vertices { get; }
            public float[] UVs => mesh.RegionUVs;
            public int[] Triangles => mesh.Triangles;

            public MeshBinding(PortraitSkinnedImage image, Slot slot, MeshAttachment mesh, Rect bounds)
            {
                Image = image;
                this.slot = slot;
                this.mesh = mesh;
                Bounds = bounds;
                Vertices = new float[mesh.WorldVerticesLength];
            }

            public void UpdateVertices(Skeleton skeleton) => mesh.ComputeWorldVertices(skeleton, slot, Vertices);
        }

        private readonly struct BoneFollower
        {
            private readonly RectTransform target;
            private readonly Bone bone;
            private readonly Matrix4x4 inverseBind;
            private readonly Vector2 position;
            private readonly Quaternion rotation;
            private readonly Vector3 scale;

            public BoneFollower(RectTransform target, Bone bone)
            {
                this.target = target;
                this.bone = bone;
                inverseBind = BoneMatrix(bone.AppliedPose).inverse;
                position = target.anchoredPosition;
                rotation = target.localRotation;
                scale = target.localScale;
            }

            public void Apply(RectTransform baseTransform, Rect rect, Rect bounds)
            {
                var delta = BoneMatrix(bone.AppliedPose) * inverseBind;
                Vector2 uiPerPixel = new(rect.width / bounds.width, rect.height / bounds.height);
                Vector2 restPivot = (Vector2)target.localPosition - target.anchoredPosition + position
                    - (Vector2)baseTransform.localPosition;
                Vector2 pivot = new(bounds.x + (restPivot.x - rect.x) / uiPerPixel.x,
                    bounds.y + (restPivot.y - rect.y) / uiPerPixel.y);
                Vector2 moved = (Vector2)delta.MultiplyPoint3x4(pivot) - pivot;
                target.anchoredPosition = position + Vector2.Scale(moved, uiPerPixel);
                target.localRotation = delta.rotation * rotation;
                target.localScale = Vector3.Scale(scale, delta.lossyScale);
            }

            public void Restore()
            {
                if (target == null) return;
                target.anchoredPosition = position;
                target.localRotation = rotation;
                target.localScale = scale;
            }

            private static Matrix4x4 BoneMatrix(BonePose pose)
            {
                var matrix = Matrix4x4.identity;
                matrix.m00 = pose.A; matrix.m01 = pose.B; matrix.m03 = pose.WorldX;
                matrix.m10 = pose.C; matrix.m11 = pose.D; matrix.m13 = pose.WorldY;
                return matrix;
            }
        }
    }
}
