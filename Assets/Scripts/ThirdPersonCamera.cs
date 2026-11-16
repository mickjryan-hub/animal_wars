using UnityEngine;
using UnityEngine.InputSystem;

namespace AnimalWars
{
    /// <summary>
    /// Orbits a camera behind the kangaroo. Pulls in when something is between the camera and the body.
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public float pivotHeight = 1.1f;
        public float distance = 4.5f;
        public float sensitivity = 0.12f;
        public LayerMask collisionMask = ~0;

        float yaw = 20f;
        float pitch = 18f;

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (target != null)
                yaw = target.eulerAngles.y;
        }

        void LateUpdate()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                var locked = Cursor.lockState == CursorLockMode.Locked;
                Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = locked;
            }

            if (target == null || Cursor.lockState != CursorLockMode.Locked)
                return;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                var delta = mouse.delta.ReadValue();
                yaw += delta.x * sensitivity;
                pitch = Mathf.Clamp(pitch - delta.y * sensitivity, -20f, 55f);
            }

            var focus = target.position + Vector3.up * pivotHeight;
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            var back = rotation * Vector3.back;
            var desired = focus + back * distance;
            if (Physics.Raycast(focus, desired - focus, out var hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
                desired = hit.point + hit.normal * 0.15f;

            transform.SetPositionAndRotation(desired, rotation);
        }
    }
}
