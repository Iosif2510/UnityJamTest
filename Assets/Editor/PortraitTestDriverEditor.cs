using PotionShop.Art;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace PotionShop.Editor
{
    [CustomEditor(typeof(PortraitTestDriver))]
    public sealed class PortraitTestDriverEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            var driver = (PortraitTestDriver)target;
            var actions = new VisualElement();
            void Button(string text, System.Action action) => actions.Add(new Button(action) { text = text });
            Button("SO 입력 적용 / 초기화", driver.Apply);
            Button("한 번 깜빡이기", driver.BlinkOnce); Button("감은 눈 유지", driver.CloseEyes);
            Button("자동 깜빡임", driver.AutoBlink); Button("깜빡임 정지", driver.StopBlink);
            Button("엔딩 틴트", driver.Tint); Button("슬라임 교체", driver.Slime);
            Button("전체 합성 복원", () => driver.SetLayer(-1));
            string[] names = { "뒷머리", "왼쪽 홍채", "오른쪽 홍채", "베이스", "감은 눈", "앞머리" };
            for (int i = 0; i < names.Length; i++) { int slot = i; Button(names[i] + "만 표시", () => driver.SetLayer(slot)); }
            Button("연결한 리그 재생", driver.PlayRig); Button("리그 정지", driver.StopRig);
            root.Add(actions);
            var status = new HelpBox("Play 후 버튼으로 확인하세요. 원본 수정은 Play 종료 후 저장합니다.", HelpBoxMessageType.Info);
            root.Add(status);
            root.schedule.Execute(() => { actions.SetEnabled(UnityEditor.EditorApplication.isPlaying); status.text = driver.Status ?? "Play 후 테스트하세요."; }).Every(200);
            return root;
        }
    }
}
