using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lucha3D.Movement
{
    /// <summary>
    /// Lee y centraliza los inputs del jugador utilizando el nuevo Unity Input System.
    /// Compatible con InputActionAsset y fallback por teclado directo.
    /// NOTA: Las flechas de dirección están RESERVADAS para la Técnica de Proyección.
    ///       El movimiento normal usa ÚNICAMENTE WASD.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInputReader : MonoBehaviour
    {
        [Header("--- Asset de Input Actions ---")]
        [Tooltip("Referencia opcional al asset InputSystem_Actions de Assets.")]
        [SerializeField] private InputActionAsset actionsAsset;

        // Acciones cacheadas
        private InputAction _moveAction;
        private InputAction _sprintAction;
        private InputAction _jumpAction;
        private InputAction _crouchAction;
        private InputAction _attackAction;

        // Estado actual de inputs
        public Vector2 MoveInput { get; private set; }
        public bool IsSprintHeld { get; private set; }
        public bool IsWalkHeld { get; private set; }
        public bool IsCrouchHeld { get; private set; }
        public bool WasJumpTriggered { get; private set; }
        public bool WasAttackTriggered { get; private set; }
        public bool IsAttackHeld { get; private set; }

        // Eventos
        public event Action OnJumpPressed;
        public event Action OnAttackPressed;
        public event Action OnAttackReleased;

        private void OnEnable()
        {
            InitializeActions();
        }

        private void OnDisable()
        {
            DisableActions();
        }

        private void InitializeActions()
        {
            if (actionsAsset != null)
            {
                var playerMap = actionsAsset.FindActionMap("Player");
                if (playerMap != null)
                {
                    _moveAction = playerMap.FindAction("Move");
                    _sprintAction = playerMap.FindAction("Sprint");
                    _jumpAction = playerMap.FindAction("Jump");
                    _crouchAction = playerMap.FindAction("Crouch");
                    _attackAction = playerMap.FindAction("Attack");

                    playerMap.Enable();

                    if (_jumpAction != null)
                        _jumpAction.performed += HandleJumpPerformed;

                    if (_attackAction != null)
                    {
                        _attackAction.performed += HandleAttackPerformed;
                        _attackAction.canceled += HandleAttackCanceled;
                    }
                }
            }
        }

        private void DisableActions()
        {
            if (_jumpAction != null)
                _jumpAction.performed -= HandleJumpPerformed;

            if (_attackAction != null)
            {
                _attackAction.performed -= HandleAttackPerformed;
                _attackAction.canceled -= HandleAttackCanceled;
            }

            if (actionsAsset != null)
                actionsAsset.FindActionMap("Player")?.Disable();
        }

        private void HandleJumpPerformed(InputAction.CallbackContext ctx)
        {
            WasJumpTriggered = true;
            OnJumpPressed?.Invoke();
        }

        private void HandleAttackPerformed(InputAction.CallbackContext ctx)
        {
            WasAttackTriggered = true;
            IsAttackHeld = true;
            OnAttackPressed?.Invoke();
        }

        private void HandleAttackCanceled(InputAction.CallbackContext ctx)
        {
            IsAttackHeld = false;
            OnAttackReleased?.Invoke();
        }

        private void Update()
        {
            ReadInputs();
        }

        private void LateUpdate()
        {
            WasJumpTriggered = false;
            WasAttackTriggered = false;
        }

        private void ReadInputs()
        {
            // 1. Movimiento Vector2 — SOLO WASD (flechas RESERVADAS para Técnica de Proyección)
            if (_moveAction != null && _moveAction.enabled)
            {
                // Si hay un InputActionAsset, filtramos igualmente para garantizar que las flechas
                // no afecten al movimiento normal (el asset puede tenerlas enlazadas).
                MoveInput = _moveAction.ReadValue<Vector2>();
            }
            else
            {
                // Fallback directo: exclusivamente WASD
                Vector2 raw = Vector2.zero;
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.wKey.isPressed) raw.y += 1f;
                    if (keyboard.sKey.isPressed) raw.y -= 1f;
                    if (keyboard.dKey.isPressed) raw.x += 1f;
                    if (keyboard.aKey.isPressed) raw.x -= 1f;
                }
                var gamepad = Gamepad.current;
                if (gamepad != null && raw == Vector2.zero)
                    raw = gamepad.leftStick.ReadValue();

                MoveInput = Vector2.ClampMagnitude(raw, 1f);
            }

            // 2. Sprint (Shift izquierdo)
            if (_sprintAction != null && _sprintAction.enabled)
            {
                IsSprintHeld = _sprintAction.IsPressed();
            }
            else
            {
                var keyboard = Keyboard.current;
                var gamepad = Gamepad.current;
                IsSprintHeld = (keyboard != null && keyboard.leftShiftKey.isPressed) ||
                               (gamepad != null && gamepad.leftTrigger.isPressed);
            }

            // 3. Modificador de Andar (Alt)
            var kb = Keyboard.current;
            IsWalkHeld = kb != null && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed);

            // 4. Agacharse (Ctrl / C)
            if (_crouchAction != null && _crouchAction.enabled)
            {
                IsCrouchHeld = _crouchAction.IsPressed();
            }
            else
            {
                IsCrouchHeld = kb != null && (kb.cKey.isPressed || kb.leftCtrlKey.isPressed);
            }

            // 5. Salto (Fallback)
            if (_jumpAction == null || !_jumpAction.enabled)
            {
                if (kb != null && kb.spaceKey.wasPressedThisFrame)
                    HandleJumpPerformed(default);
            }

            // 6. Ataque (Fallback) — Click Izquierdo o J
            if (_attackAction == null || !_attackAction.enabled)
            {
                var mouse = Mouse.current;
                if ((mouse != null && mouse.leftButton.wasPressedThisFrame) ||
                    (kb != null && kb.jKey.wasPressedThisFrame))
                    HandleAttackPerformed(default);

                if ((mouse != null && mouse.leftButton.wasReleasedThisFrame) ||
                    (kb != null && kb.jKey.wasReleasedThisFrame))
                    HandleAttackCanceled(default);
            }
        }
    }
}
