using System;
using System.Collections.Generic;
using UnityEngine;

namespace PotionShop.Data
{
    public enum CustomerGender { Unspecified, Male, Female, Genderless }
    public enum PortraitSlot { Hair, Eyes, Mouth, Ears }
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
    }

    [Serializable]
    public sealed class CustomerProfile : CatalogEntry
    {
        [SerializeField] private string speciesId;
        [SerializeField] private CustomerGender gender;
        [SerializeField] private Sprite basePortrait;
        [SerializeField] private List<PortraitPart> parts = new();

        public string SpeciesId => speciesId;
        public CustomerGender Gender => gender;
        public Sprite BasePortrait => basePortrait;
        public IReadOnlyList<PortraitPart> Parts => parts;
    }

    [CreateAssetMenu(menuName = "Potion Shop/Data/Customers")]
    public sealed class CustomerCatalog : ScriptableObject
    {
        [SerializeField] private List<CustomerProfile> profiles = new();

        public IReadOnlyList<CustomerProfile> Profiles => profiles;
    }
}
