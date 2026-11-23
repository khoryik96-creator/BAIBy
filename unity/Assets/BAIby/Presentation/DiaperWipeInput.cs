using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BAIby.Presentation
{
    public sealed class DiaperWipeInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IInitializePotentialDragHandler, IDragHandler
    {
        private RectTransform mat;
        private Action<Vector2, Vector2> wipe;
        private Vector2 previous;
        private int pointer;
        private bool tracking;

        internal void Bind(RectTransform target, Action<Vector2, Vector2> onWipe)
        {
            mat = target;
            wipe = onWipe;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (tracking || eventData.button != PointerEventData.InputButton.Left ||
                !TryLocalPoint(eventData, out var position))
                return;

            tracking = true;
            pointer = eventData.pointerId;
            previous = position;
            wipe(position, position);
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            eventData.useDragThreshold = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            Move(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!tracking || eventData.pointerId != pointer)
                return;

            Move(eventData);
            tracking = false;
        }

        private void Move(PointerEventData eventData)
        {
            if (!tracking || eventData.pointerId != pointer || !TryLocalPoint(eventData, out var position))
                return;

            wipe(previous, position);
            previous = position;
        }

        private bool TryLocalPoint(PointerEventData eventData, out Vector2 position)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(mat, eventData.position,
                eventData.pressEventCamera, out position);
        }

        private void OnDisable()
        {
            tracking = false;
        }
    }
}
