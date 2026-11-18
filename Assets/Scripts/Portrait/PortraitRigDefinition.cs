using UnityEngine;
using UnityEngine.U2D.Animation;

namespace PotionShop.Portraits
{
    public sealed class PortraitRigDefinition : MonoBehaviour
    {
        public Animator animator;
        public SpriteSkin baseSkin, closedEyeSkin, frontHairSkin, backHairSkin;
        public Transform headBone;
    }
}
