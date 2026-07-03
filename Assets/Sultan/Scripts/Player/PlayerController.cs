using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public CharacterController characterController;
    public Animator animator;
    public float speed = 5f;
    public float acceleration = 35f;
    public float deceleration = 50f;
    public float zMoveSpeed = 2.5f;
    public float zMin = -1f;
    public float zMax = 2f;
    public float gravity = -20f;
    public float pushSpeedScale = 0.6f;
    private float _currentSpeed;
    private float _currentZSpeed;
    private float _verticalVelocity;
    private float _inputX;
    private float _inputZ;
    private bool _isGrounded;
    private bool _inputEnabled = true;
    private BoxPusher _pusher;

    private static readonly int SpeedHash = Animator.StringToHash("speed");
    private static readonly int GroundedHash = Animator.StringToHash("isGrounded");
    private static readonly int PushingHash = Animator.StringToHash("isPushing");

    //Ali- Animation
    [Header("Ali- Animation")]
    [SerializeField] private float blendTimeMovement;
    private float _blendSpeed;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
       // animator = GetComponent<Animator>();// we dont need this for now
        _pusher = GetComponent<BoxPusher>();
    }

    void Update()
    {
        if (!_inputEnabled) { _inputX = 0f; _inputZ = 0f; return; }
        _inputX = Input.GetAxisRaw("Horizontal");
        _inputZ = Input.GetAxisRaw("Vertical");

        move();
        zMove();
        flip();
        updateAnimator();

        Debug.Log(_currentSpeed);
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

        Vector3 finalMove = new Vector3(_currentSpeed, _verticalVelocity, 0f);
        characterController.Move(finalMove * Time.deltaTime);
        _isGrounded = characterController.isGrounded;
    }

    void zMove()
    {
        if (_pusher != null && _pusher.IsPushing) return;

        float target = _inputZ * zMoveSpeed;
        float rate = Mathf.Abs(_inputZ) > 0.01f ? acceleration : deceleration;
        _currentZSpeed = Mathf.MoveTowards(_currentZSpeed, target, rate * Time.deltaTime);

        Vector3 pos = transform.position;
        pos.z = Mathf.Clamp(pos.z + _currentZSpeed * Time.deltaTime, zMin, zMax);
        transform.position = pos;
    }

    void flip()
    {
        if (Mathf.Abs(_inputX) < 0.01f) return;
        Vector3 s = transform.localScale;
        s.x = Mathf.Sign(_inputX) * Mathf.Abs(s.x);
        transform.localScale = s;
    }

    void updateAnimator()
    {
        if (animator == null) return;
        // animator.SetFloat(SpeedHash, Mathf.Abs(_currentSpeed));
        // animator.SetBool(GroundedHash, _isGrounded);
        // if (_pusher != null) animator.SetBool(PushingHash, _pusher.IsPushing);

        //Ali 
        // we need to work in the blend tree here 
        float targetSpeed;
            if(Mathf.Abs(_currentSpeed) > 0)
        {
            targetSpeed = 1;
        }
        else
        {
            targetSpeed = 0;
        }
        _blendSpeed = Mathf.Lerp(_blendSpeed, targetSpeed, Time.deltaTime * blendTimeMovement);
         animator.SetFloat("MovementBlend", _blendSpeed);
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