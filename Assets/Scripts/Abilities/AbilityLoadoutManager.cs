using UnityEngine;
using UnityEngine.InputSystem;
using Lucha3D.Core;
using Lucha3D.Movement;
using Lucha3D.Combat;

namespace Lucha3D.Abilities
{
    /// <summary>
    /// Gestiona el equipamiento de habilidades especiales.
    /// Al iniciar la partida muestra un menú de selección para escoger un solo poder activo
    /// (o la opción 'Aleatorio'). Permite reabrir el menú con la tecla 'TAB' o 'M'.
    /// </summary>
    [DisallowMultipleComponent]
    public class AbilityLoadoutManager : MonoBehaviour
    {
        [Header("--- Habilidades Disponibles ---")]
        [SerializeField] private BoogieWoogieAbility boogieWoogie;
        [SerializeField] private ProjectionTechnique projectionTechnique;

        [Header("--- Control de Selección ---")]
        [Tooltip("Si es verdadero, abre el menú de selección automáticamente al iniciar la prueba.")]
        [SerializeField] private bool openMenuOnStart = true;

        [Tooltip("Tecla para reabrir el menú de selección durante la prueba.")]
        [SerializeField] private Key toggleMenuKey = Key.Tab;

        private SpecialAbilityType _currentAbility = SpecialAbilityType.None;
        private bool _isMenuOpen = false;
        private PlayerLocomotion _locomotion;
        private PlayerCombatController _combat;

        // GUI styles cacheados
        private GUIStyle _titleStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _subStyle;
        private bool _stylesInitialized = false;

        public event System.Action<SpecialAbilityType> OnAbilityEquipped;
        public event System.Action<bool> OnMenuStateChanged;

        public SpecialAbilityType CurrentAbility => _currentAbility;
        public bool IsMenuOpen => _isMenuOpen;
        public string CurrentAbilityDisplayName => _currentAbility switch
        {
            SpecialAbilityType.BoogieWoogie => "Boogie-Woogie (Aoi Todo)",
            SpecialAbilityType.ProjectionTechnique => "Técnica de Proyección (24 FPS)",
            _ => "Ninguna"
        };

        private void Awake()
        {
            if (boogieWoogie == null) boogieWoogie = GetComponent<BoogieWoogieAbility>();
            if (projectionTechnique == null) projectionTechnique = GetComponent<ProjectionTechnique>();
            _locomotion = GetComponent<PlayerLocomotion>();
            _combat = GetComponent<PlayerCombatController>();
        }

        private void Start()
        {
            // Desactivar ambas habilidades hasta que el jugador elija
            DisableAllAbilities();

            if (openMenuOnStart)
                OpenSelectionMenu();
            else
                EquipAbility(SpecialAbilityType.BoogieWoogie);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb[toggleMenuKey].wasPressedThisFrame)
            {
                if (_isMenuOpen) CloseSelectionMenu();
                else OpenSelectionMenu();
            }
        }

        #region Gestión del Menú
        public void OpenSelectionMenu()
        {
            _isMenuOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (_locomotion != null) _locomotion.SetControlsFrozen(true);

            OnMenuStateChanged?.Invoke(true);
        }

        public void CloseSelectionMenu()
        {
            _isMenuOpen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (_locomotion != null) _locomotion.SetControlsFrozen(false);

            OnMenuStateChanged?.Invoke(false);
        }

        public void EquipAbility(SpecialAbilityType abilityType)
        {
            DisableAllAbilities();
            _currentAbility = abilityType;

            switch (abilityType)
            {
                case SpecialAbilityType.BoogieWoogie:
                    if (boogieWoogie != null) boogieWoogie.enabled = true;
                    break;
                case SpecialAbilityType.ProjectionTechnique:
                    if (projectionTechnique != null) projectionTechnique.enabled = true;
                    break;
            }

            CloseSelectionMenu();
            OnAbilityEquipped?.Invoke(_currentAbility);
            Debug.Log($"[AbilityLoadoutManager] Habilidad equipada: {CurrentAbilityDisplayName}");
        }

        public void EquipRandomAbility()
        {
            SpecialAbilityType[] available = new[]
            {
                SpecialAbilityType.BoogieWoogie,
                SpecialAbilityType.ProjectionTechnique
            };
            EquipAbility(available[UnityEngine.Random.Range(0, available.Length)]);
        }

        private void DisableAllAbilities()
        {
            if (boogieWoogie != null) boogieWoogie.enabled = false;
            if (projectionTechnique != null) projectionTechnique.enabled = false;
        }
        #endregion

        #region Interfaz Visual de Selección (OnGUI)
        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
            _titleStyle.normal.textColor = Color.white;

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter
            };

            _subStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11
            };
            _subStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
        }

        private void OnGUI()
        {
            if (!_isMenuOpen) return;
            InitStyles();

            // Fondo semi-transparente
            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float panelW = 460f;
            float panelH = 360f;
            float panelX = (Screen.width - panelW) * 0.5f;
            float panelY = (Screen.height - panelH) * 0.5f;

            // Marco del panel
            GUI.Box(new Rect(panelX - 5, panelY - 5, panelW + 10, panelH + 10), "");

            GUI.Label(new Rect(panelX, panelY + 10, panelW, 35),
                "⚡ SELECCIÓN DE HABILIDAD ESPECIAL ⚡", _titleStyle);

            GUI.Label(new Rect(panelX, panelY + 50, panelW, 25),
                "Elige el poder activo para esta sesión de prueba:", _subStyle);

            // Botón 1 — Boogie-Woogie
            var btnColor = new Color(0.2f, 0.6f, 1f);
            GUI.backgroundColor = btnColor;
            if (GUI.Button(new Rect(panelX + 30, panelY + 85, panelW - 60, 60),
                "1.  BOOGIE-WOOGIE  (Aoi Todo)\n" +
                "E: Marcar objetivo  |  R: Elegirse a sí mismo  |  Q: Desmarcar último  |  F: ¡INTERCAMBIAR!",
                _buttonStyle))
            {
                EquipAbility(SpecialAbilityType.BoogieWoogie);
            }

            // Botón 2 — Técnica de Proyección
            GUI.backgroundColor = new Color(0.9f, 0.4f, 1f);
            if (GUI.Button(new Rect(panelX + 30, panelY + 160, panelW - 60, 60),
                "2.  TÉCNICA DE PROYECCIÓN  (Naobito · 24 FPS)\n" +
                "Q: Activar  →  0.5s prep  →  Ventana de inputs  →  ¡Ráfaga en 1/24s!",
                _buttonStyle))
            {
                EquipAbility(SpecialAbilityType.ProjectionTechnique);
            }

            // Botón Aleatorio
            GUI.backgroundColor = new Color(1f, 0.85f, 0.1f);
            if (GUI.Button(new Rect(panelX + 30, panelY + 240, panelW - 60, 50),
                "🎲  PODER ALEATORIO  (elige uno al azar entre los disponibles)",
                _buttonStyle))
            {
                EquipRandomAbility();
            }

            GUI.backgroundColor = Color.white;

            GUI.Label(new Rect(panelX, panelY + 305, panelW, 40),
                "Puedes reabrir este menú en cualquier momento pulsando TAB.", _subStyle);
        }
        #endregion
    }
}
