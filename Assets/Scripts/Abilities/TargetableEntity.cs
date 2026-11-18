using UnityEngine;

namespace Lucha3D.Abilities
{
    /// <summary>
    /// Componente que identifica a una entidad (jugador, enemigo, maniquí o escombro)
    /// como seleccionable para habilidades de posicionamiento como Boogie-Woogie o fijación de cámara.
    /// </summary>
    [DisallowMultipleComponent]
    public class TargetableEntity : MonoBehaviour
    {
        [Header("--- Propiedades de Selección ---")]
        [Tooltip("Transform que define el punto focal para la puntería (por defecto este mismo Transform).")]
        [SerializeField] private Transform aimFocusPoint;

        [Tooltip("Etiqueta de tipo de entidad para filtrado o interfaz visual.")]
        [SerializeField] private string entityDisplayName = "Objetivo";

        [Tooltip("¿Puede esta entidad ser objetivo del Boogie-Woogie?")]
        [SerializeField] private bool canBeSwapped = true;

        public Transform AimFocusPoint => aimFocusPoint != null ? aimFocusPoint : transform;
        public string EntityDisplayName => entityDisplayName;
        public bool CanBeSwapped => canBeSwapped;

        // Marcadores de estado para feedback visual (UI, shaders, auras)
        public bool IsMarkedSlotA { get; set; }
        public bool IsMarkedSlotB { get; set; }

        private void Reset()
        {
            aimFocusPoint = transform;
            entityDisplayName = gameObject.name;
        }

        private void Awake()
        {
            if (aimFocusPoint == null)
            {
                aimFocusPoint = transform;
            }
        }
    }
}
