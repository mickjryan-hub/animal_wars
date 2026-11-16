using UnityEngine;
using UnityEngine.InputSystem;

namespace AnimalWars
{
    /// <summary>
    /// Walks the kangaroo on the ground. WASD is relative to the camera.
    /// The walk clip is in place, so this script moves the body.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class KangarooPlayer : MonoBehaviour
    {
        public float walkSpeed = 1.1f;
        public float turnSpeed = 12f;
        public float gravity = -20f;
        public Animator animator;

        CharacterController body;
        float vertical;

        void Awake()
        {
            body = GetComponent<CharacterController>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (animator != null)
                animator.applyRootMotion = false;
        }

        void Update()
        {
            var moving = ReadMove(out var wish);
            if (moving)
            {
                var face = Quaternion.LookRotation(wish, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, face, turnSpeed * Time.deltaTime);
            }

            if (body.isGrounded && vertical < 0f)
                vertical = -2f;
            vertical += gravity * Time.deltaTime;

            body.Move(wish * walkSpeed * Time.deltaTime + Vector3.up * vertical * Time.deltaTime);

            if (animator != null)
                animator.SetFloat("Speed", moving ? 1f : 0f, 0.12f, Time.deltaTime);
        }

        static bool ReadMove(out Vector3 wish)
        {
            var keyboard = Keyboard.current;
            float x = 0f, z = 0f;
            if (keyboard != null)
            {
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) z += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) z -= 1f;
            }

            var cam = Camera.main != null ? Camera.main.transform : null;
            var forward = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up) : Vector3.forward;
            var right = cam != null ? Vector3.ProjectOnPlane(cam.right, Vector3.up) : Vector3.right;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            wish = Vector3.ClampMagnitude(right.normalized * x + forward.normalized * z, 1f);
            return wish.sqrMagnitude > 0.01f;
        }

        void OnGUI()
        {
            GUI.Label(new Rect(16, 16, 640, 24), "WASD or arrows to walk    Mouse to look    Esc to free the mouse");
        }
    }
}
