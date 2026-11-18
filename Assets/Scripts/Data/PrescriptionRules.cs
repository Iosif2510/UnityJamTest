using System;
using System.Collections.Generic;
using UnityEngine;

namespace PotionShop.Data
{
    public enum BottleShape { Unspecified, Square, Heart, Round }

    [Serializable]
    public sealed class BottleMapping
    {
        [SerializeField] private CustomerGender gender;
        [SerializeField] private BottleShape bottle;

        public CustomerGender Gender => gender;
        public BottleShape Bottle => bottle;
    }

    [Serializable]
    public sealed class AppearanceCondition
    {
        [SerializeField] private PortraitSlot slot;
        [SerializeField] private List<AppearanceValue> anyOf = new();

        public PortraitSlot Slot => slot;
        public IReadOnlyList<AppearanceValue> AnyOf => anyOf;
    }

    [Serializable]
    public sealed class InkRule
    {
        [SerializeField] private string id;
        [SerializeField, Range(1, 6)] private int inkNumber = 1;
        [SerializeField] private List<AppearanceCondition> conditions = new();
        [SerializeField] private List<AppearanceCondition> exceptions = new();

        public string Id => id;
        public int InkNumber => inkNumber;
        public IReadOnlyList<AppearanceCondition> Conditions => conditions;
        public IReadOnlyList<AppearanceCondition> Exceptions => exceptions;
    }

    public enum ReactionKind
    {
        WrongBottle, WrongInk, AllIngredientsCorrect,
        OneIngredientWrong, MultipleIngredientsWrong, Timeout
    }

    [Serializable]
    public sealed class ReactionDialogue
    {
        [SerializeField] private ReactionKind kind;
        [SerializeField] private List<string> lines = new();

        public ReactionKind Kind => kind;
        public IReadOnlyList<string> Lines => lines;
    }

    [CreateAssetMenu(menuName = "Potion Shop/Data/Prescription Rules")]
    public sealed class PrescriptionRules : ScriptableObject
    {
        [SerializeField] private List<BottleMapping> bottles = new();
        [SerializeField] private List<InkRule> inks = new();
        [SerializeField] private List<ReactionDialogue> reactions = new();

        public IReadOnlyList<BottleMapping> Bottles => bottles;
        public IReadOnlyList<InkRule> Inks => inks;
        public IReadOnlyList<ReactionDialogue> Reactions => reactions;
    }
}
