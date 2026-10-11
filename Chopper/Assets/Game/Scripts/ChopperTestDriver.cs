using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Chopper
{
    /// <summary>
    /// Test driver for TestScene. Idle: holds the Idle state. Walk: sets Speed and moves the
    /// character forward at walkSpeed (the Walk clip is authored in place at 0.75 m/s, so the
    /// feet stay planted). Space toggles the mode in Play mode. The character returns to its
    /// start point after maxDistance so it stays on the 20 m ground.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class ChopperTestDriver : MonoBehaviour
    {
        public enum Mode { Idle, Walk, Run }

        public static readonly int SpeedId = Animator.StringToHash("Speed");

        public Mode mode = Mode.Walk;
        [Tooltip("Ground speed in m/s; must match the authored walk speed (0.75 m/s).")]
        public float walkSpeed = 0.75f;
        public float runSpeed = 2.2f;
        [Tooltip("Distance after which the character is sent back to its start point.")]
        public float maxDistance = 8f;

        Animator animator;
        Vector3 startPosition;

        void Awake()
        {
            animator = GetComponent<Animator>();
            startPosition = transform.position;
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                mode = mode == Mode.Idle ? Mode.Walk : mode == Mode.Walk ? Mode.Run : Mode.Idle;
            if (Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame)
                animator.SetTrigger("Attack1");
            if (Keyboard.current != null && Keyboard.current.digit2Key.wasPressedThisFrame)
                animator.SetTrigger("Attack2");
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
                animator.SetTrigger("Hit");
            if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
                animator.SetTrigger("Die");
#endif
            Step(Time.deltaTime);
        }

        /// <summary>Applies the current mode for one step of dt seconds (moves the root only).</summary>
        public void Step(float dt)
        {
            float speed = mode == Mode.Run ? runSpeed : mode == Mode.Walk ? walkSpeed : 0f;
            if (animator == null) animator = GetComponent<Animator>();
            animator.SetFloat(SpeedId, speed);
            if (speed <= 0f) return;

            transform.position += transform.forward * (speed * dt);
            if (Vector3.Distance(transform.position, startPosition) > maxDistance)
                transform.position = startPosition;
        }
    }
}
