using UnityEngine;
using UnityEngine.InputSystem;

namespace Lucha3D.Movement
{
    /// <summary>
    /// Cámara en tercera persona para juegos de lucha y acción 3D.
    /// Sigue suavemente al objetivo, permite rotación orbital con ratón o stick derecho,
    /// y admite fijación de cámara (lock-on) durante técnicas especiales.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public class CameraFollow : MonoBehaviour
    {
        [Header("--- Objetivo ---")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);

        [Header("--- Distancia y Órbita ---")]
        [SerializeField] private float distance = 5.5f;
        [SerializeField] private float minDistance = 2.0f;
        [SerializeField] private float maxDistance = 12.0f;
        [SerializeField] private float mouseSensitivityX = 120f;
        [SerializeField] private float mouseSensitivityY = 100f;
        [SerializeField] private float minPitch = -20f;
        [SerializeField] private float maxPitch = 70f;
        [SerializeField] private float smoothSpeed = 15f;

        [Header("--- Fijación de Objetivo (Lock-On) ---")]
        [SerializeField] private Transform lockedTarget;

        private float _yaw = 0f;
        private float _pitch = 15f;

        public Transform Target { get => target; set => target = value; }
        public Transform LockedTarget { get => lockedTarget; set => lockedTarget = value; }

        private void Start()
        {
            Vector3 angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x;

            // Bloquear cursor para control tipo cámara de acción
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            HandleInput();
            UpdateCameraPosition();
        }

        private void HandleInput()
        {
            // Desbloquear cursor con tecla Escape si se desea
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = !Cursor.visible;
            }

            if (Cursor.lockState != CursorLockMode.Locked) return;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue() * 0.05f;
                _yaw += delta.x * mouseSensitivityX * Time.deltaTime;
                _pitch -= delta.y * mouseSensitivityY * Time.deltaTime;
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            }

            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                Vector2 stick = gamepad.rightStick.ReadValue();
                _yaw += stick.x * mouseSensitivityX * Time.deltaTime;
                _pitch -= stick.y * mouseSensitivityY * Time.deltaTime;
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            }
        }

        private void UpdateCameraPosition()
        {
            Vector3 focusPoint = target.position + targetOffset;

            // Si hay un objetivo fijado (ej: durante la Técnica de Proyección)
            if (lockedTarget != null)
            {
                Vector3 toEnemy = (lockedTarget.position - focusPoint).normalized;
                if (toEnemy.sqrMagnitude > 0.001f)
                {
                    Quaternion lockRotation = Quaternion.LookRotation(toEnemy, Vector3.up);
                    _yaw = Mathf.LerpAngle(_yaw, lockRotation.eulerAngles.y, 10f * Time.deltaTime);
                }
            }

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 desiredPosition = focusPoint - (rotation * Vector3.forward * distance);

            // Suavizado de traslación
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
            transform.LookAt(focusPoint);
        }

        public void SetLockedTarget(Transform newLock)
        {
            lockedTarget = newLock;
        }

        /// <summary>
        /// Libera el lock-on y vuelve al modo de cámara libre.
        /// </summary>
        public void ClearLockedTarget()
        {
            lockedTarget = null;
        }
    }
}
