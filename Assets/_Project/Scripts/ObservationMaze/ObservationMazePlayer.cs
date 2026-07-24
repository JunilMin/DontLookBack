using UnityEngine;
using UnityEngine.InputSystem;

namespace DontLookBack.ObservationMaze
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class ObservationMazePlayer : MonoBehaviour
    {
        private const float MoveSpeed = 3.8f;
        private Rigidbody2D body;
        private Camera gameCamera;
        private Vector2 move;

        public Vector2 LookDirection { get; private set; } = Vector2.right;
        public bool IsListening { get; private set; }
        public bool MovementLocked { get; set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            GetComponent<CircleCollider2D>().radius = 0.32f;
            gameCamera = Camera.main;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            move = Vector2.zero;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1f;
            move = Vector2.ClampMagnitude(move, 1f);

            IsListening = keyboard.fKey.isPressed;
            if (IsListening)
            {
                move *= 0.25f;
            }

            var mouse = Mouse.current;
            if (mouse != null && gameCamera != null)
            {
                var pointer = mouse.position.ReadValue();
                var world = gameCamera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y, 0f));
                var direction = (Vector2)world - (Vector2)transform.position;
                if (direction.sqrMagnitude > 0.01f)
                {
                    LookDirection = direction.normalized;
                }
            }

            if (MovementLocked)
            {
                move = Vector2.zero;
                IsListening = false;
            }

            transform.right = LookDirection;
        }

        private void FixedUpdate()
        {
            body.linearVelocity = move * MoveSpeed;
        }
    }
}
