using TMPro;
using UnityEngine;

namespace JollyLlama.SkillTreeSystem.Economy
{
    public class GoldUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI goldText;

        [Header("Display")]
        [SerializeField] private string prefix = "Gold: ";
        [SerializeField] private string format = "N0"; 

        private void OnEnable()
        {
            GoldManager.OnGoldChanged += UpdateDisplay;

            if (GoldManager.Instance != null)
                UpdateDisplay(GoldManager.Instance.CurrentGold);
        }

        private void OnDisable()
        {
            GoldManager.OnGoldChanged -= UpdateDisplay;
        }

        private void UpdateDisplay(int amount)
        {
            if (goldText == null) return;
            goldText.text = prefix + amount.ToString(format);
        }
    }
}
