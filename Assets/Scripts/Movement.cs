using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Retro.ThirdPersonCharacter
{
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(Combat))]
    [RequireComponent(typeof(CharacterController))]
    public class Movement : MonoBehaviour
    {
        private Animator _animator;
        private PlayerInput _playerInput;
        private Combat _combat;
        private CharacterController _characterController;

        private Vector2 lastMovementInput;
        private Vector3 moveDirection = Vector3.zero;
        private Vector3 _lastMoveDirection = Vector3.forward;
        private Camera _camera;

        [Header("Rotation (WASD / kamera yönüne göre)")]
        [SerializeField] private float rotationSpeed = 15f;

        [Header("Dash (animasyonsuz hızlı kaçış)")]
        [SerializeField] private float dashSpeedMultiplier = 2.2f;
        [SerializeField] private float dashDuration = 0.16f;
        [SerializeField] private float dashCooldown = 0.6f;
        [SerializeField] private ParticleSystem dashVfx;

        [Header("Düşme ölümü")]
        [SerializeField] private float killBelowY = -10f;
        [SerializeField] private float restartDelayAfterFall = 1.2f;

        public float gravity = 10;
        public float jumpSpeed = 4; 

        public float MaxSpeed = 10;
        private float DecelerationOnStop = 0.00f;
        private bool _isDashing;
        private float _dashUntilTime;
        private float _nextDashReadyTime;
        private Vector3 _dashDirection = Vector3.forward;
        private PlayerHealth _playerHealth;
        private bool _fallRestartStarted;


        private void Start()
        {
            _animator = GetComponent<Animator>();
            _playerInput = GetComponent<PlayerInput>();
            _combat = GetComponent<Combat>();
            _characterController = GetComponent<CharacterController>();
            _playerHealth = GetComponent<PlayerHealth>();
            _camera = Camera.main;
            // WASD dönüşü kullanıyorsak Aiming rotasyonu ezmesin
            var aiming = GetComponent<Aiming>();
            if (aiming != null) aiming.enabled = false;
        }

        private void Update()
        {
            if (_animator == null) return;

            if (HandleFallDeathAndRestart())
                return;

            HandleDashInput();

            if(_combat.AttackInProgress)
            {
                StopMovementOnAttack();
            }
            else
            {
                Move();
            }

            UpdateRotation();
        }

        private bool HandleFallDeathAndRestart()
        {
            if (_fallRestartStarted)
                return true;
            if (_playerHealth == null || _playerHealth.IsDead)
                return false;
            if (transform.position.y > killBelowY)
                return false;

            _playerHealth.TakeDamage(Mathf.Max(1, _playerHealth.CurrentHealth));
            StartCoroutine(RestartSceneAfterFallRoutine());
            return true;
        }

        private IEnumerator RestartSceneAfterFallRoutine()
        {
            _fallRestartStarted = true;
            float delay = Mathf.Max(0f, restartDelayAfterFall);
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>Kameranın "ileri" ve "sağ"ına göre hareket yönü (XZ düzleminde).</summary>
        private Vector3 GetCameraRelativeMoveDirection(float x, float y)
        {
            if (_camera == null) return new Vector3(x, 0f, y);
            Vector3 f = _camera.transform.forward;
            Vector3 r = _camera.transform.right;
            f.y = 0f;
            r.y = 0f;
            f.Normalize();
            r.Normalize();
            return (f * y + r * x).normalized;
        }

        private void UpdateRotation()
        {
            var x = _playerInput.MovementInput.x;
            var y = _playerInput.MovementInput.y;
            Vector3 worldDir = GetCameraRelativeMoveDirection(x, y);

            if (worldDir.sqrMagnitude > 0.001f)
                _lastMoveDirection = worldDir;

            if (_lastMoveDirection.sqrMagnitude <= 0.001f) return;

            Quaternion targetRot = Quaternion.LookRotation(_lastMoveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        private void Move()
        {
            var x = _playerInput.MovementInput.x;
            var y = _playerInput.MovementInput.y;
            Vector3 worldMoveDir = GetCameraRelativeMoveDirection(x, y);
            if (worldMoveDir.sqrMagnitude > 0.001f)
                _lastMoveDirection = worldMoveDir;

            Vector3 activeDir = _isDashing ? _dashDirection : worldMoveDir;
            float activeSpeed = _isDashing ? MaxSpeed * dashSpeedMultiplier : MaxSpeed;

            bool grounded = _characterController.isGrounded;

            if (grounded)
            {
                moveDirection = activeDir * activeSpeed;
                if (_playerInput.JumpInput && !_isDashing)
                    moveDirection.y = jumpSpeed;
            }

            moveDirection.y -= gravity * Time.deltaTime;
            _characterController.Move(moveDirection * Time.deltaTime);

            Vector2 animInput = _isDashing ? GetCameraRelativeInputFromWorldDirection(_dashDirection) : new Vector2(x, y);
            lastMovementInput = animInput;
            _animator.SetFloat("InputX", animInput.x);
            _animator.SetFloat("InputY", animInput.y);
            _animator.SetBool("IsInAir", !grounded);
        }

        private void HandleDashInput()
        {
            if (_isDashing && Time.time >= _dashUntilTime)
                _isDashing = false;

            if (_isDashing || _combat.AttackInProgress) return;
            if (!_playerInput.DashInput || Time.time < _nextDashReadyTime) return;

            Vector3 inputDir = GetCameraRelativeMoveDirection(_playerInput.MovementInput.x, _playerInput.MovementInput.y);
            _dashDirection = inputDir.sqrMagnitude > 0.001f ? inputDir : _lastMoveDirection;
            if (_dashDirection.sqrMagnitude <= 0.001f)
                _dashDirection = transform.forward;

            _isDashing = true;
            _dashUntilTime = Time.time + dashDuration;
            _nextDashReadyTime = Time.time + dashCooldown;
            RunStatisticsTracker.Instance?.RegisterDash();

            if (dashVfx != null)
                dashVfx.Play();
        }

        private Vector2 GetCameraRelativeInputFromWorldDirection(Vector3 worldDirection)
        {
            if (_camera == null || worldDirection.sqrMagnitude <= 0.001f)
                return Vector2.zero;

            Vector3 f = _camera.transform.forward;
            Vector3 r = _camera.transform.right;
            f.y = 0f;
            r.y = 0f;
            f.Normalize();
            r.Normalize();

            float x = Vector3.Dot(worldDirection, r);
            float y = Vector3.Dot(worldDirection, f);
            return new Vector2(x, y);
        }

        private void StopMovementOnAttack()
        {
            var temp = lastMovementInput;
            temp.x -= DecelerationOnStop;
            temp.y -= DecelerationOnStop;
            lastMovementInput = temp;

            _animator.SetFloat("InputX", lastMovementInput.x);
            _animator.SetFloat("InputY", lastMovementInput.y);
        }
    }
}