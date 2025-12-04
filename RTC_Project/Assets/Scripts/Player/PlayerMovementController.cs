using System;
using System.Collections;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.InputSystem;

// 이 스크립트는 Rigidbody 기반 3D 캐릭터 이동(카메라 상대) + 점프를 다룹니다.
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovementController : MonoBehaviour
{
    //-----Movement-----
    [Header("Move")]
    public float moveSpeed;
    public float acceleration; // 부드럽게 속도 맞출 때 사용
    public bool canMove = true;
    public float runMoveSpeed = 8f;            // 달리기 속도 (walkSpeed(moveSpeed)보다 크게 설정)
    public float runInputSmoothing = 10f;      // 0이면 즉시 반영, 클수록 부드럽게 변화 (선택값)
    private float currentRunInput = 0f;        // 스무딩용 내부값

    [Header("Jump")]
    public float jumpForce = 6f;
    private enum AirState { Grounded, Rising, Falling }
    public LayerMask groundMask;
    private AirState airState = AirState.Grounded;
    private float lastGroundedTime;
    
    private Rigidbody rb;
    private PlayerAssetsInputs _input;
    private PlayerInput _playerInput;
    private Animator anim;
    
    //-----Cinemachine-----
    [Header("Cinemachine")]
    public GameObject cinemachineCameraTarget;
    public float topClamp = 70.0f; //상한 제어
    public float bottomClamp = -30.0f; //하한 제어
    public float cameraAngleOverride = 0.0f; //추가 각도
    public bool lockCameraPosition = false;
    
    private float _cinemachineTargetYaw;
    private float _cinemachineTargetPitch;
    private const float _threshold = 0.01f;
    
    private bool IsCurrentDeviceMouse
    {
        get
        {
            #if ENABLE_INPUT_SYSTEM
            return _playerInput.currentControlScheme == "KeyboardMouse";
            #else
				return false;
            #endif
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
        // 회전 동결
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    private void Start()
    {
        _cinemachineTargetYaw = cinemachineCameraTarget.transform.rotation.eulerAngles.y;
        _input = GetComponent<PlayerAssetsInputs>();
        _playerInput = GetComponent<PlayerInput>();
    }

    void FixedUpdate()
    {
        HandleMove();
        HandleJump();
        UpdateAirState();
    }

    private void LateUpdate()
    {
        CameraRotation();
    }

    private void HandleMove()
    {
        bool isMoving = _input.move.sqrMagnitude > 0.01f;
        anim.SetBool("Walk", isMoving);

        if (!canMove) return;

        // --- 1) Run input 읽기
        float runInput = 0f;
        if (_playerInput != null && _playerInput.actions != null && _playerInput.actions.FindAction("Run") != null)
        {
            runInput = _playerInput.actions["Run"].ReadValue<float>(); // 키보드: 0/1, 패드: 0~1
        }
        
        // 선택: run input smoothing (부드럽게 바뀌길 원하면 값 > 0)
        if (runInputSmoothing > 0f)
            currentRunInput = Mathf.MoveTowards(currentRunInput, runInput, runInputSmoothing * Time.fixedDeltaTime);
        else
            currentRunInput = runInput;

        // 애니메이터 파라미터 반영
        anim.SetFloat("runSpeed", currentRunInput);

        // --- 2) 최종 속도 계산 (즉시 반영)
        float speed = Mathf.Lerp(moveSpeed, runMoveSpeed, currentRunInput);

        // --- 3) 카메라 기준 이동 벡터 계산
        Vector3 camForward = Camera.main ? Camera.main.transform.forward : Vector3.forward;
        Vector3 camRight = Camera.main ? Camera.main.transform.right : Vector3.right;

        // 평면화
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 desired = (camRight * _input.move.x + camForward * _input.move.y) * speed;

        // --- 4) LERP 기반 가속 (MoveTowards 제거!)
        Vector3 currentVel = rb.linearVelocity;
        Vector3 currentHoriz = new Vector3(currentVel.x, 0f, currentVel.z);

        // acceleration이 클수록 즉시 변화, 작을수록 부드러움
        Vector3 newHoriz = Vector3.Lerp(
            currentHoriz,
            desired,
            acceleration * Time.fixedDeltaTime
        );

        // --- 5) Rigidbody 속도 적용 (수직속도 유지)
        rb.linearVelocity = new Vector3(newHoriz.x, currentVel.y, newHoriz.z);

        // --- 6) 이동방향 바라보기
        Vector3 lookDir = new Vector3(desired.x, 0f, desired.z);

        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                10f * Time.fixedDeltaTime
            );
        }
    }

    private void HandleJump()
    {
        if (!_input.wantJump) return;
        if (IsGrounded())
        {
            PlayMoveDustParticle();
            anim.SetTrigger("Jump");
            rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
        }
        _input.wantJump = false;
    }
    
    void UpdateAirState()
    {
        bool grounded = IsGrounded();
        float vy = rb.linearVelocity.y;

        switch (airState)
        {
            case AirState.Grounded:
                // 착지한 지 0.2초가 안 지났으면 상태 전환 금지
                if (Time.time < lastGroundedTime + 0.2f) return;

                if (!grounded)
                {
                    if (vy > 0.1f) airState = AirState.Rising;
                    else if (vy < -0.1f)
                    {
                        airState = AirState.Falling;
                        anim.SetTrigger("Falling");
                    }
                }
                break;

            case AirState.Rising:
                if (grounded)
                {
                    EnterGroundedState(); break;
                }
                if (vy < -0.1f)
                {
                    airState = AirState.Falling;
                    anim.SetTrigger("Falling");
                }
                break;

            case AirState.Falling:
                if (grounded)
                {
                    EnterGroundedState();
                }
                break;
        }
    }
    
    private bool IsGrounded()
    {
        CapsuleCollider col = GetComponent<CapsuleCollider>();

        Vector3 start = transform.position + Vector3.up * (col.radius);
        Vector3 end = transform.position + Vector3.up * (col.height - col.radius);

        float distance = 0.1f;

        return Physics.CapsuleCast(
            start,
            end,
            col.radius * 0.95f,
            Vector3.down,
            distance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );
    }
    
    private void CameraRotation()
    {
        // 입력이 있고 카메라 위치가 고정되지 않은 경우
        if (_input.look.sqrMagnitude >= _threshold && !lockCameraPosition)
        {
            float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

            _cinemachineTargetYaw += _input.look.x * deltaTimeMultiplier;
            _cinemachineTargetPitch += _input.look.y * deltaTimeMultiplier;
        }

        // 값이 360도로 제한되도록 회전을 고정
        _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
        _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, bottomClamp, topClamp);

        //시네머신 타겟
        cinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + cameraAngleOverride,
            _cinemachineTargetYaw, 0.0f);
    }
    
    //[Animation에서 호출]-----------------
    public void PlayMoveDustParticle()
    {
        if (ParticlePool.Instance == null) return;

        // "MoveDust"는 ParticlePool에서 설정한 key와 동일해야 함
        var go = ParticlePool.Instance.Get("MoveDust", transform.position, transform.rotation);
        if (go == null) return;

        // 안전을 위해 파티클 시스템 재생
        var ps = go.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            // 만약 이전 남은 파티클이 있으면 정리
            ps.Clear(true);
            ps.Play(true);
        }
    }
    
    // [헬퍼 함수] 착지 로직을 공통으로 관리
    private void EnterGroundedState()
    {
        airState = AirState.Grounded;
        lastGroundedTime = Time.time; // 착지 시간 기록

        // 땅에 닿는 순간 튀어 오르는 반동(Y속도)을 강제로 죽임
        Vector3 vel = rb.linearVelocity;
        vel.y = 0f;
        rb.linearVelocity = vel;
        
        anim.SetTrigger("Landing");
    }
    
    private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        if (lfAngle < -360f) lfAngle += 360f;
        if (lfAngle > 360f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }
}
