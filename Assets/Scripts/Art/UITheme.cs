using UnityEngine;

namespace PotionShop.Art
{
    [CreateAssetMenu(menuName = "Potion Shop/Art/Shared UI Theme")]
    public sealed class UITheme : ScriptableObject
    {
        public Sprite pocketFrame;
        public Color pocketEmpty = new Color32(39, 49, 50, 255);
        public Color pocketFilled = new Color32(168, 217, 177, 255);
        public Color pocketBackground = new Color32(24, 31, 32, 255);
    }
}
