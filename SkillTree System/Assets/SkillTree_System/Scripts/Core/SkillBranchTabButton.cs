using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JollyLlama.SkillTreeSystem
{
    /// <summary>
    /// Put this on the tab prefab that SkillTreePanel spawns once per SkillBranchSO.
    /// Every reference except the Button is optional.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class SkillBranchTabButton : MonoBehaviour
    {
        [SerializeField] private Button          button;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private Image           icon;
        [Tooltip("Tinted with the branch color (e.g. an underline or background).")]
        [SerializeField] private Image           colorTarget;

        public SkillBranchSO Branch { get; private set; }
        public Button        Button => button;

        private void Reset() => button = GetComponent<Button>();

        public void Initialize(SkillBranchSO branch, System.Action onClick)
        {
            Branch = branch;
            if (button == null) button = GetComponent<Button>();

            if (label != null) label.text = branch.DisplayName;

            if (icon != null)
            {
                icon.sprite  = branch.icon;
                icon.enabled = branch.icon != null;
            }

            if (colorTarget != null) colorTarget.color = branch.color;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke());
        }
    }
}


