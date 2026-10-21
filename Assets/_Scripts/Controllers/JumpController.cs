using Character.Controls;
using UnityEngine;
using UnityEngine.InputSystem;

public class JumpController : MonoBehaviour
{
    [Header("Jumping")]
    [SerializeField] float jumpHeight = 3f;
    protected float JumpUpVel;
    float gravity;
    [SerializeField] float maxJumpTime = .5f;
    [SerializeField] float heaviness = 3f;
    [Header("Jumping Logic")]
    [SerializeField] float JumpBufferTime = 0.25f;
    protected float _jumpBufferTimer = 0.0f;

    [SerializeField, RequireInterface(typeof(IGroundDetector))]
    private MonoBehaviour groundDetector;
    public IGroundDetector GroundDetector => groundDetector as IGroundDetector;
    public Rigidbody _rb;
    bool _isJumpPressed = false;
    protected bool _isJumping = false;


    public bool IsGrounded
    {
        get
        {
            return GroundDetector.IsGrounded;
        }
    }
    public bool IsJumping
    {
        get { return _isJumping; }
    }
    public bool IsJumpPressed
    {
        get { return _isJumpPressed; }
    }
    public bool IsFalling { get { return _rb.linearVelocity.y < -9f; } }
    public void Start()
    {
        float timeToApex = maxJumpTime / 2;
        gravity = (-2 * jumpHeight) / Mathf.Pow(timeToApex, 2);
        JumpUpVel = (2 * jumpHeight) / timeToApex;

    }
    public void onJump(InputAction.CallbackContext ctx)
    {
        _isJumpPressed = ctx.ReadValueAsButton();
        if (ctx.performed)
            _jumpBufferTimer = JumpBufferTime;
    }
    public void FixedUpdate()
    {

        JumpPlayer();
        HandleGravity();
    }
    protected void HandleGravity()
    {
        if (GroundDetector.IsGrounded) return;

        if (_isJumpPressed)
        {
            _rb.AddForce(gravity * _rb.mass * Vector3.up);
        }
        else
        {

            _rb.AddForce((gravity * _rb.mass * Vector3.up) * heaviness);
        }

    }

    protected void JumpPlayer()
    {
        if (_jumpBufferTimer > 0 && GroundDetector.IsGrounded && !_isJumping)
        {
            _isJumping = true;

            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, JumpUpVel, _rb.linearVelocity.z);
        }
        else if (GroundDetector.IsGrounded && _isJumping)
        {
            _isJumping = false;
        }
        if (_jumpBufferTimer > 0)
            _jumpBufferTimer -= Time.fixedDeltaTime;
    }


}
