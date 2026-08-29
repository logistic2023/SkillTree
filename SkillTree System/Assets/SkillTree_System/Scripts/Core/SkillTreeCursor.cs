using UnityEngine;

namespace JollyLlama.SkillTreeSystem
{
    public class SkillTreeCursor : MonoBehaviour
    {
        [SerializeField] private RectTransform skillTreeCursor;

        private void LateUpdate()
        {
            skillTreeCursor.position = Input.mousePosition;
        }
    }
}