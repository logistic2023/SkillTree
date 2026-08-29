using UnityEngine;

public class SkillTreeCursor : MonoBehaviour
{
   [SerializeField] RectTransform rectTransform;
   
   public void LateUpdate()
   {
      rectTransform.position = Input.mousePosition;
   }
}
