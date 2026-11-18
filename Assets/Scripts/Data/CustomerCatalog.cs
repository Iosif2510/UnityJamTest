using System;
using System.Collections.Generic;
using UnityEngine;

namespace PotionShop.Data
{
    public enum CustomerGender { Unspecified, Male, Female, Genderless }
    public enum PortraitSlot { Hair, Eyes, Mouth, Ears }
    public enum IrisDetailStyle { Clear, Glow, Oblique, Slit, Soft }
    [Serializable]
    public sealed class IrisDetailPair
    {
        public IrisDetailStyle style;
        public Sprite left;
        public Sprite right;
        [Range(0f, 360f), Tooltip("원본 홍채의 기준 HSV 색상각 (도)")]
        public float sourceHueDegrees;
    }
    public enum AppearanceValue
    {
        Unspecified, None, Black, Brown, Red, Green, Blue, Gold, Silver,
        Yellow, Purple, White, Orange, Moss, Crystal, Normal, Smile,
        Fangs, RoundEars, PointedEars, AnimalEars, RockCrack
    }

    [Serializable]
    public sealed class PortraitPart
    {
        [SerializeField] private string id;
        [SerializeField] private PortraitSlot slot;
        [SerializeField] private AppearanceValue appearance;
        [SerializeField] private Sprite sprite;

        public string Id => id;
        public PortraitSlot Slot => slot;
        public AppearanceValue Appearance => appearance;
        public Sprite Sprite => sprite;

        public PortraitPart() { }
        public PortraitPart(PortraitSlot slot, AppearanceValue appearance)
        {
            this.slot = slot;
            this.appearance = appearance;
            id = $"appearance.{slot}.{appearance}";
        }
    }

    [Serializable]
    public sealed class CustomerProfile : CatalogEntry
    {
        [SerializeField] private string speciesId;
        [SerializeField] private CustomerGender gender;
        [SerializeField] private Sprite basePortrait;
        [SerializeField] private List<PortraitPart> parts = new();
        [Header("고정 인물 초상화 (신규 제작)")]
        [SerializeField] private bool fixedAppearance;
        [SerializeField] private string job;
        [SerializeField] private AppearanceValue earType;
        [SerializeField] private bool hasFang;
        [SerializeField] private Sprite eyeClosed;
        [SerializeField] private Sprite hairFront;
        [SerializeField] private Sprite hairBack;
        [SerializeField] private List<AppearanceValue> hairColors = new();
        [SerializeField] private List<AppearanceValue> eyeColors = new();
        [Tooltip("원본 캔버스 좌하단 기준 0~1 사각형")]
        [SerializeField] private List<Rect> irisRegions = new();
        [SerializeField] private GameObject rigPrefab;
        [Tooltip("Spine 4.3 Portrait 스켈레톤 JSON (idle 애니메이션)")]
        [SerializeField] private TextAsset spineRig;
        [Header("선택형 홍채 마스크·디테일")]
        [SerializeField] private Sprite irisMask;
        [SerializeField] private Sprite irisVariantsMask;
        [SerializeField] private IrisDetailStyle irisDetailStyle;
        [SerializeField] private List<IrisDetailPair> irisDetails = new();

        public string SpeciesId => speciesId;
        public CustomerGender Gender => gender;
        public Sprite BasePortrait => basePortrait;
        public IReadOnlyList<PortraitPart> Parts => parts;
        public bool FixedAppearance => fixedAppearance;
        public string Job => job;
        public AppearanceValue EarType => earType;
        public bool HasFang => hasFang;
        public Sprite EyeClosed => eyeClosed;
        public Sprite HairFront => hairFront;
        public Sprite HairBack => hairBack;
        public IReadOnlyList<AppearanceValue> HairColors => hairColors;
        public IReadOnlyList<AppearanceValue> EyeColors => eyeColors;
        public IReadOnlyList<Rect> IrisRegions => irisRegions;
        public GameObject RigPrefab => rigPrefab;
        public TextAsset SpineRig => spineRig;
        public Sprite IrisMask => irisMask;
        public Sprite IrisVariantsMask => irisVariantsMask;
        public IrisDetailStyle IrisDetailStyle => irisDetailStyle;
        public IReadOnlyList<IrisDetailPair> IrisDetails => irisDetails;

        public PortraitPart[] Describe(AppearanceValue hair, AppearanceValue eyes) => new[]
        {
            new PortraitPart(PortraitSlot.Hair, hair), new PortraitPart(PortraitSlot.Eyes, eyes),
            new PortraitPart(PortraitSlot.Mouth, hasFang ? AppearanceValue.Fangs : AppearanceValue.Normal),
            new PortraitPart(PortraitSlot.Ears, earType)
        };
    }

    [CreateAssetMenu(menuName = "Potion Shop/Data/Customers")]
    public sealed class CustomerCatalog : ScriptableObject
    {
        [SerializeField] private List<CustomerProfile> profiles = new();
        [SerializeField] private Sprite slimePortrait;

        public IReadOnlyList<CustomerProfile> Profiles => profiles;
        public Sprite SlimePortrait => slimePortrait;
    }
}
