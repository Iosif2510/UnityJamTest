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
        private readonly Slot baseSlot;
        private readonly Slot blinkSlot;
        private readonly MeshAttachment baseMesh;
        private readonly PortraitSkinnedImage baseImage;
        private readonly BoneFollower[] followers;

        public Rect Bounds { get; }
        public float[] Vertices { get; }
        public float[] UVs => baseMesh.RegionUVs;
        public int[] Triangles => baseMesh.Triangles;
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
            baseSlot = RequireSlot("base");
            baseMesh = baseSlot.AppliedPose.Attachment as MeshAttachment
                ?? throw new InvalidDataException("Spine slot 'base' requires a setup mesh.");
            blinkSlot = RequireSlot("eye_closed");
            Vertices = new float[baseMesh.WorldVerticesLength];
            var irisBone = RequireSlot("iris").Bone;
            var followersList = new List<BoneFollower>(4);
            AddFollower(followersList, layers.hairBack, RequireSlot("hair_back").Bone);
            AddFollower(followersList, layers.hairFront, RequireSlot("hair_front").Bone);
            AddFollower(followersList, layers.eyeClosed, blinkSlot.Bone);
            if (layers.irisMask != null)
                AddFollower(followersList, layers.irisMask, irisBone);
            else
            {
                AddFollower(followersList, layers.irisLeft, irisBone);
                AddFollower(followersList, layers.irisRight, irisBone);
            }
            followers = followersList.ToArray();

            animation = new Spine.AnimationState(new AnimationStateData(data));
            animation.SetAnimation(0, idle, true);
            Advance(0f);
            baseImage.BindSpine(this);
            UpdateFollowers();
        }

        private Slot RequireSlot(string name) => skeleton.FindSlot(name)
            ?? throw new InvalidDataException($"Missing Spine slot '{name}'.");

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
            baseMesh.ComputeWorldVertices(skeleton, baseSlot, Vertices);
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
            if (baseImage != null)
                baseImage.BindSpine(null);
            foreach (var follower in followers)
                follower.Restore();
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
