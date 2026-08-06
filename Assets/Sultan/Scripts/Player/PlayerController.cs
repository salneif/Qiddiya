using System;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // we need instance so we dont ref in other scripts
    public static PlayerController instance;


    public CharacterController characterController;
    public Animator animator;
    public bool CanMove = true;
    public float speed = 5f;
    public float normalWalkSpeed;
    public float acceleration = 35f;
    public float deceleration = 50f;
    public float zMoveSpeed = 2.5f;
    public float gravity = -20f;
    public float pushSpeedScale = 0.6f;
    public bool cameraRelative = false;
    private float _currentSpeed;
    private float _currentZSpeed;
    private float _verticalVelocity;
    private float _inputX;
    private float _inputZ;
    private bool _isGrounded;
    private bool _inputEnabled = true;
    private BoxPusher _pusher;
    private bool _isInAir;
    private TopDownCameraFollow _topDownCam;
    private Vector3 _moveRight = Vector3.right;
    private Vector3 _moveForward = Vector3.forward;
    private int _lastRawX;
    private int _lastRawZ;
    private float _baseZMoveSpeed;

    private static readonly int SpeedHash = Animator.StringToHash("speed");
    private static readonly int GroundedHash = Animator.StringToHash("isGrounded");
    private static readonly int PushingHash = Animator.StringToHash("isPushing");







    //Ali- Animation
    [Header("Ali- Animation")]
    [SerializeField] private float blendTimeMovement;
    private float _blendSpeed;

    // double jump 
    private bool alreadyDoubleJumped = false;

    // Ali - Conections
    [SerializeField] private A_CrouchAndJump crouchAndJumpSystem;

    // ref 
    [SerializeField] private A_PlayerDeath_WaterSection a_PlayerDeath;
    


    private void Awake()
    {
        if(instance != null)
        {
            return;
        }
        else
        {
            instance = this;
        }
    }
    void Start()
    {
        characterController = GetComponent<CharacterController>();
        _pusher = GetComponent<BoxPusher>();
        _baseZMoveSpeed = zMoveSpeed;

        crouchAndJumpSystem.OnCrouch += OnCrouch;
        crouchAndJumpSystem.OnJump += OnJump;

        if (Camera.main != null)
            _topDownCam = Camera.main.GetComponent<TopDownCameraFollow>();

      
    }

    private void OnEnable()
    {
        if(a_PlayerDeath != null)
        a_PlayerDeath.OnPlayerFall += OnPlayerFall;

        A_Jumppad.OnJumppad += OnJumppad;

    }

  

    private void OnDisable()
    {
        if(a_PlayerDeath != null)
        a_PlayerDeath.OnPlayerFall -= OnPlayerFall;

        A_Jumppad.OnJumppad -= OnJumppad;
    }

    private void OnPlayerFall()
    {
        characterController.Move(new Vector3(0,gravity,0));
    }

    private void OnJump(float jumpForce , bool canDoubleJump , bool canJump)
    {
        if (!CanMove)
        {
            return ;
        }
        if (canJump)
        {
            _verticalVelocity = Mathf.Sqrt(jumpForce * -2f * gravity);
            animator.SetTrigger("Jump");
        }
        else if(!characterController.isGrounded && canDoubleJump && !alreadyDoubleJumped)
        {
            _verticalVelocity = Mathf.Sqrt(jumpForce * -2f * gravity);
            animator.SetTrigger("Jump2");
            alreadyDoubleJumped = true;
        }   

        
    }
    private void OnJumppad(float jumpForce)
    {
        _verticalVelocity = Mathf.Sqrt(jumpForce * -2f * gravity);
        animator.SetTrigger("Jump");
    }

    private void OnCrouch(bool isCrouching, float CrouchMoveSpeed)
    {
        if (!CanMove)
        {
            return ;
        }
        if(isCrouching)
        {
            speed = normalWalkSpeed;
            zMoveSpeed = _baseZMoveSpeed;
            animator.SetBool("IsCrouch" , false);
            characterController.height = 1;
            characterController.center = Vector3.zero;
        }
        else if (!isCrouching)
        {
            animator.SetBool("IsCrouch", true);
            speed = CrouchMoveSpeed;
            zMoveSpeed = CrouchMoveSpeed;
            characterController.height = 0.5f;
            characterController.center = new Vector3(0, -0.25f, 0);
        }
    }

    void Update()
    {
        if (!_inputEnabled) { _inputX = 0f; _inputZ = 0f; return; }
        _inputX = Input.GetAxisRaw("Horizontal");
        _inputZ = Input.GetAxisRaw("Vertical");

        if (CanMove)
        {
            updateMoveBasis();
            zMove();
            move();
            flip();
            updateAnimator();
        }

        if (IsGrounded)
        {
            alreadyDoubleJumped = false;
        }
    }

    void updateMoveBasis()
    {
        if (!cameraRelative)
        {
            _moveRight = Vector3.right;
            _moveForward = Vector3.forward;
            return;
        }

        int rx = _inputX > 0.01f ? 1 : (_inputX < -0.01f ? -1 : 0);
        int rz = _inputZ > 0.01f ? 1 : (_inputZ < -0.01f ? -1 : 0);
        if (rx == _lastRawX && rz == _lastRawZ) return;

        _lastRawX = rx;
        _lastRawZ = rz;

        if (rx == 0 && rz == 0) return;

        float yaw = _topDownCam != null ? _topDownCam.HeadingYaw : 0f;
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        _moveRight = q * Vector3.right;
        _moveForward = q * Vector3.forward;
    }

    void move()
    {
        if (_isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;

        _verticalVelocity += gravity * Time.deltaTime;



        bool pushing = _pusher != null && _pusher.IsPushing;
        float s = pushing ? speed * pushSpeedScale : speed;
        float target = _inputX * s;
        float rate = Mathf.Abs(_inputX) > 0.01f ? acceleration : deceleration;
        _currentSpeed = Mathf.MoveTowards(_currentSpeed, target, rate * Time.deltaTime);

        Vector3 planar = _moveRight * _currentSpeed + _moveForward * _currentZSpeed;
        Vector3 finalMove = new Vector3(planar.x, _verticalVelocity, planar.z);
        if (characterController.enabled == true)
        {
            characterController.Move(finalMove * Time.deltaTime);
        }
        _isGrounded = characterController.isGrounded;
    }

    void zMove()
    {
        float target = (_pusher != null && _pusher.IsPushing) ? 0f : _inputZ * zMoveSpeed;
        float rate = Mathf.Abs(target) > 0.01f ? acceleration : deceleration;
        _currentZSpeed = Mathf.MoveTowards(_currentZSpeed, target, rate * Time.deltaTime);
    }

    void flip()
    {
        Vector3 direction = _moveRight * _inputX + _moveForward * _inputZ;
        if (direction.magnitude < 0.01f) { return; }
        transform.rotation = Quaternion.LookRotation(direction);

       
    }

    void updateAnimator()
    {
        if (animator == null) return;
       
        // if (_pusher != null) animator.SetBool(PushingHash, _pusher.IsPushing);

        //Ali 
        // we need to work in the blend tree here 
        float targetSpeed;
            if(Mathf.Abs(_currentSpeed) > 0 || (Mathf.Abs(_currentZSpeed) > 0))
        {
            targetSpeed = 1;
        }
        else
        {
            targetSpeed = 0;
        }
        _blendSpeed = Mathf.Lerp(_blendSpeed, targetSpeed, Time.deltaTime * blendTimeMovement);
         animator.SetFloat("MovementBlend", _blendSpeed);


        bool isCurrentlyInAir = animator.GetBool("InAir");
        if (isCurrentlyInAir != !IsGrounded)
        {
            animator.SetBool("InAir", !IsGrounded);
        }
        //Ali

    }

    public void SetInputEnabled(bool enabled)
    {
        _inputEnabled = enabled;
        if (!enabled)
        {
            _inputX = 0f;
            _inputZ = 0f;
            _currentSpeed = 0f;
            _currentZSpeed = 0f;
            _verticalVelocity = 0f;
        }
    }

    public bool IsGrounded => _isGrounded;
    public float MoveInput => _inputX;
    public float CurrentSpeed => _currentSpeed;
}