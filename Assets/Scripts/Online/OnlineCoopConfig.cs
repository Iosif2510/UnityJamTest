using UnityEngine;

namespace PotionShop.Online
{
    /// <summary>Shared references for the online co-op screens. Loaded from Resources so existing scenes stay untouched.</summary>
    [CreateAssetMenu(menuName = "Potion Shop/Online Co-op Config")]
    public sealed class OnlineCoopConfig : ScriptableObject
    {
        public const string ResourcePath = "OnlineCoopConfig";
        public const string TitleScene = "00_Title";
        public const string CounterScene = "01_Order";
        public const string LoftScene = "03_Loft";

        [SerializeField] private Font bodyFont;
        [SerializeField] private Font boldFont;
        [SerializeField, Tooltip("Cover, six potion memos in POT order, then the counter manual.")]
        private Sprite[] memoSheets;
        [SerializeField] private string[] memoLabels =
            { "표지", "메모 1", "메모 2", "메모 3", "메모 4", "메모 5", "메모 6", "접객 매뉴얼" };

        [Header("Shared look")]
        [SerializeField, Tooltip("The painted loft desk the narrator's sheets lie on.")]
        private Sprite deskBackground;
        [SerializeField, Tooltip("The parchment panel used by the counter scenes.")]
        private Sprite parchmentPanel;
        [SerializeField, Tooltip("The yellow wooden button used by the counter scenes.")]
        private Sprite woodButton;

        [SerializeField, Tooltip("A long slip of aged paper: the note that comes up from downstairs.")]
        private Sprite noteSlip;
        [SerializeField, Tooltip("A small card of aged paper: the result note and the pause menu.")]
        private Sprite noteCard;

        [Header("Shared with the counter and workshop screens")]
        [SerializeField] private Sprite namePlate;
        [SerializeField] private Sprite timerFrame;
        [SerializeField] private Sprite timerFill;
        [SerializeField] private Sprite timerHourglass;
        [SerializeField] private Sprite stampGreat;
        [SerializeField] private Sprite stampOkay;
        [SerializeField] private Sprite stampSlime;
        [SerializeField] private Sprite stampSlow;

        public Sprite NamePlate => namePlate;
        public Sprite TimerFrame => timerFrame;
        public Sprite TimerFill => timerFill;
        public Sprite TimerHourglass => timerHourglass;
        public Sprite StampGreat => stampGreat;
        public Sprite StampOkay => stampOkay;
        public Sprite StampSlime => stampSlime;
        public Sprite StampSlow => stampSlow;
        public Sprite NoteSlip => noteSlip;
        public Sprite NoteCard => noteCard;
        public Sprite DeskBackground => deskBackground;
        public Sprite ParchmentPanel => parchmentPanel;
        public Sprite WoodButton => woodButton;
        public Font BodyFont => bodyFont;
        public Font BoldFont => boldFont != null ? boldFont : bodyFont;
        public Sprite[] MemoSheets => memoSheets;
        public string[] MemoLabels => memoLabels;
        public int CounterManualIndex => memoSheets.Length - 1;

        private static OnlineCoopConfig cached;

        public static OnlineCoopConfig Load()
        {
            if (cached == null)
                cached = Resources.Load<OnlineCoopConfig>(ResourcePath);
            return cached;
        }
    }
}
