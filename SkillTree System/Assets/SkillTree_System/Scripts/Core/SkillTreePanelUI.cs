using DG.Tweening;
using UnityEngine;

namespace JollyLlama.SkillTreeSystem
{
    public class SkillTreePanelUI : MonoBehaviour
    {
        public GameObject skillTreePanel;

        public void Open()
        {
            skillTreePanel.SetActive(true);
            skillTreePanel.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack);
        }

        public void Close()
        {
            skillTreePanel.transform.DOScale(0f, 0.25f).SetEase(Ease.InBack).OnComplete(() =>
            {
                skillTreePanel.SetActive(false);
            });
        }
    }
}