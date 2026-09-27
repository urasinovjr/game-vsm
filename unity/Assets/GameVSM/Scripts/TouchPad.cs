using UnityEngine;
using UnityEngine.EventSystems;

namespace GameVSM
{
    public sealed class TouchPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public FirstPersonController Player;
        public bool Look;
        int pointer = int.MinValue;
        Vector2 origin;
        public void OnPointerDown(PointerEventData e)
        {
            if (pointer != int.MinValue) return;
            pointer = e.pointerId; origin = e.position;
        }
        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != pointer) return;
            if (Look) Player.TouchLook += e.delta * .1f;
            else Player.TouchMove = Vector2.ClampMagnitude((e.position - origin) / (Screen.height * .14f), 1);
        }
        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != pointer) return;
            pointer = int.MinValue;
            if (!Look) Player.TouchMove = Vector2.zero;
        }
        void OnDisable() { pointer = int.MinValue; if (Player != null) Player.TouchMove = Vector2.zero; }
    }
}
