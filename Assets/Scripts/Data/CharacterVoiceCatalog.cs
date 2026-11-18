using System;
using System.Collections.Generic;
using UnityEngine;

namespace PotionShop.Data
{
    [Serializable]
    public sealed class CharacterVoice
    {
        [SerializeField] private string code;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string speechStyle;
        [SerializeField, TextArea] private string personality;
        [SerializeField] private string[] chatTopics = Array.Empty<string>();

        public string Code => code;
        public string DisplayName => displayName;
        public string SpeechStyle => speechStyle;
        public string Personality => personality;
        public IReadOnlyList<string> ChatTopics => chatTopics;
    }

    [CreateAssetMenu(menuName = "Potion Shop/Data/Character Voices")]
    public sealed class CharacterVoiceCatalog : ScriptableObject
    {
        [SerializeField] private List<CharacterVoice> voices = new();

        public IReadOnlyList<CharacterVoice> Voices => voices;
    }
}
