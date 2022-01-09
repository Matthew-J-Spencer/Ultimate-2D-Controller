using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour {

    // Public for external hooks
    public Vector3 Velocity { get; private set; } 
    public FrameInput Input { get; private set; }
    public bool JumpingThisFrame { get; private set; }
    
    private Vector3 _lastPosition;
    private float _currentHorizontalSpeed, _currentVerticalSpeed;

    private void Update() {
        //Application.targetFrameRate = (int)Mathf.PingPong(Time.time * 40, 200) + 60; // Testing various frame rates

        // Calculate velocity
        Velocity = (transform.position - _lastPosition) / Time.deltaTime;
        _lastPosition = transform.position;
        
        GatherInput();
        RunCollisionChecks();
        
        CalculateWalk(); // Horizontal movement
        CalculateJumpApex(); // Affects fall speed, so calculate before gravity
        CalculateGravity(); // Vertical movement
        CalculateJump(); // Possibly overrides vertical

        MoveCharacter(); // Actually perform the axis movement
    }



    #region Gather Input

    private void GatherInput() {
        Input = new FrameInput {
            JumpDown = UnityEngine.Input.GetKeyDown(KeyCode.Space),
            HoldingJump = UnityEngine.Input.GetKey(KeyCode.Space),
            JumpUp = UnityEngine.Input.GetKeyUp(KeyCode.Space),
            X = UnityEngine.Input.GetAxisRaw("Horizontal")
        };

        if (Input.JumpDown) _lastJumpPressed = Time.time;
    }

    #endregion

    #region Walk

    [Header("WALKING")] [SerializeField] private float _acceleration = 30;
    [SerializeField] private float _moveClamp = 13;
    [SerializeField] private float _deAcceleration = 0.3f;
    [SerializeField] private float _apexBonus = 2;
    
    private void CalculateWalk() {
        if (Input.X != 0) {
            // Set horizontal move speed
            _currentHorizontalSpeed += Input.X * (_acceleration);

            // clamped by max frame movement
            _currentHorizontalSpeed = Mathf.Clamp(_currentHorizontalSpeed,
                -_moveClamp,
                _moveClamp);

            // Apply bonus at the apex of a jump
            var apexBonus = (Input.X > 0 ? _apexBonus : -_apexBonus) * _apexPoint;
            _currentHorizontalSpeed += apexBonus;
        }
        else {
            // No input. Let's slow the character down
            _currentHorizontalSpeed = Mathf.MoveTowards(_currentHorizontalSpeed, 0, _deAcceleration);
        }

        // Escape walls
        if (_colRight.Inside) _currentHorizontalSpeed = -(_obstacleEscapeSpeed);
        else if (_colLeft.Inside) _currentHorizontalSpeed = _obstacleEscapeSpeed;
        else if (_currentHorizontalSpeed > 0 && _colRight.Colliding || _currentHorizontalSpeed < 0 && _colLeft.Colliding) {
            // Don't walk through walls
            _currentHorizontalSpeed = 0;
        }
    }

    #endregion

    #region Gravity

    [Header("GRAVITY")] [SerializeField] private float _fallClamp = -30f;
    [SerializeField] private float _minFallSpeed = 80f;
    [SerializeField] private float _maxFallSpeed = 120f;
    private float _fallSpeed;

    private void CalculateGravity() {
        if (_colDown.Colliding) {
            // Move out of the ground
            if (_colDown.Inside) _currentVerticalSpeed = _obstacleEscapeSpeed;
            else _currentVerticalSpeed = 0;
        }
        else {
            // Fall
            _currentVerticalSpeed -= _fallSpeed * Time.deltaTime;

            // Clamp
            if (_currentVerticalSpeed < _fallClamp) {
                _currentVerticalSpeed = _fallClamp;
            }
        }
    }

    #endregion

    #region Jump

    [Header("JUMPING")] [SerializeField] private float _jumpHeight = 30;
    [SerializeField] private float _jumpApexThreshold = 10f;
    [SerializeField] private float _coyoteTimeThreshold = 0.1f;
    [SerializeField] private float _jumpBuffer = 0.1f;
    private bool _coyoteUsable;
    private bool _endedJumpEarly = true;
    private float _apexPoint; // Becomes 1 at the apex of a jump
    private float _lastJumpPressed;
    private bool CanUseCoyote => _coyoteUsable && !_colDown.Colliding && _timeLeftGrounded + _coyoteTimeThreshold > Time.time;
    private bool HasBufferedJump => _colDown.Colliding && _lastJumpPressed + _jumpBuffer > Time.time;

    private void CalculateJumpApex() {
        if (!_colDown.Colliding) {
            // Gets stronger the closer to the top of the jump
            _apexPoint = Mathf.InverseLerp(_jumpApexThreshold, 0, Mathf.Abs(Velocity.y));
            _fallSpeed = Mathf.Lerp(_minFallSpeed, _maxFallSpeed, _apexPoint);
        }
        else {
            _apexPoint = 0;
        }
    }

    private void CalculateJump() {
        // Jump if: grounded or within coyote threshold || sufficient jump buffer
        if (Input.JumpDown && (_colDown.Colliding || CanUseCoyote) || HasBufferedJump) {
            _currentVerticalSpeed = _jumpHeight;
            _endedJumpEarly = false;
            _coyoteUsable = false;
            _timeLeftGrounded = float.MinValue;
            JumpingThisFrame = true;
        }
        else JumpingThisFrame = false;

        // End the jump early if button released
        if (!_colDown.Colliding && Input.JumpUp && !_endedJumpEarly && Velocity.y > 0) {
            _currentVerticalSpeed = 0;
            _endedJumpEarly = true;
        }

        if (_colUp.Colliding) {
            // Can we move from a corner bump
            // TODO

            if (_currentVerticalSpeed > 0)
                _currentVerticalSpeed = 0;
            if (_colUp.Inside) _currentVerticalSpeed = -_obstacleEscapeSpeed;
        }
    }

    #endregion

    private void MoveCharacter() {
        transform.position += new Vector3(_currentHorizontalSpeed, _currentVerticalSpeed) * Time.deltaTime;
    }


    #region Collisions

    [Header("COLLISION")] [SerializeField] private Bounds _characterBounds;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private int _detectorCount = 3;
    [SerializeField] private float _detectionRayLength = 0.3f;
    [SerializeField] private float _detectionThreshold = 0.01f;
    [SerializeField] private float _obstacleEscapeSpeed = 1f;

    [SerializeField, Range(0.1f, 0.3f)] private float _rayBuffer = 0.1f; // Prevents side detectors hitting the ground

    private RayRange _raysUp, _raysRight, _raysDown, _raysLeft;
    private CollisionInfo _colUp, _colRight, _colDown, _colLeft;

    private float _timeLeftGrounded;

    public bool Grounded => _colDown.Colliding;

    private void RunCollisionChecks() {
        // Generate ray ranges. 
        CalculateRayRanged();

        // Ground
        var groundedCheck = RunDetection(_raysDown, Vector3.down);
        if (_colDown.Colliding && !groundedCheck.Colliding) _timeLeftGrounded = Time.time; // Only trigger when first leaving
        else if (!_colDown.Colliding && groundedCheck.Colliding) _coyoteUsable = true; // Only trigger when first touching
        _colDown = groundedCheck;


        // The rest
        _colUp = RunDetection(_raysUp, Vector2.up);
        _colLeft = RunDetection(_raysLeft, Vector3.left);
        _colRight = RunDetection(_raysRight, Vector3.right);

        CollisionInfo RunDetection(RayRange range, Vector2 dir) {
            var detected = false;
            var inCollision = false;

            foreach (var point in EvaluateRayPositions(range)) {
                var cast = Physics2D.Raycast(point, dir, _detectionRayLength, _groundLayer);

                if (cast && cast.distance < _detectionThreshold) {
                    detected = true;
                    if (!inCollision && cast.distance <= 0) inCollision = true;
                }
            }

            return new CollisionInfo(detected, inCollision);
        }
    }


    private void CalculateRayRanged() {
        var p = transform.position;
        // This is insanely gross
        _raysDown = new RayRange(p + new Vector3(_characterBounds.min.x + _rayBuffer, _characterBounds.min.y), p + new Vector3(_characterBounds.max.x - _rayBuffer, _characterBounds.min.y));
        _raysUp = new RayRange(p + new Vector3(_characterBounds.min.x + _rayBuffer, _characterBounds.max.y), p + new Vector3(_characterBounds.max.x - _rayBuffer, _characterBounds.max.y));
        _raysLeft = new RayRange(p + new Vector3(_characterBounds.min.x, _characterBounds.min.y + _rayBuffer), p + new Vector3(_characterBounds.min.x, _characterBounds.max.y - _rayBuffer));
        _raysRight = new RayRange(p + new Vector3(_characterBounds.max.x, _characterBounds.min.y + _rayBuffer), p + new Vector3(_characterBounds.max.x, _characterBounds.max.y - _rayBuffer));
    }


    private IEnumerable<Vector2> EvaluateRayPositions(RayRange range) {
        var step = 1f / _detectorCount;
        for (var i = 0f; i < _detectorCount; i += step) {
            yield return Vector2.Lerp(range.Start, range.End, i);
        }
    }

    private void OnDrawGizmos() {
        // Bounds
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, _characterBounds.size);

        // Rays
        if (!Application.isPlaying) CalculateRayRanged();
        Gizmos.color = Color.green;
        foreach (var point in EvaluateRayPositions(_raysDown)) {
            Gizmos.DrawRay(point, Vector3.down * _detectionRayLength);
        }

        foreach (var point in EvaluateRayPositions(_raysUp)) {
            Gizmos.DrawRay(point, Vector3.up * _detectionRayLength);
        }

        foreach (var point in EvaluateRayPositions(_raysLeft)) {
            Gizmos.DrawRay(point, Vector3.left * _detectionRayLength);
        }

        foreach (var point in EvaluateRayPositions(_raysRight)) {
            Gizmos.DrawRay(point, Vector3.right * _detectionRayLength);
        }
    }

    #endregion

    public struct FrameInput {
        public float X;
        public bool JumpDown;
        public bool JumpUp;
        public bool HoldingJump;
    }

    private struct RayRange {
        public RayRange(Vector2 start, Vector2 end) {
            Start = start;
            End = end;
        }

        public readonly Vector2 Start, End;
    }

    private struct CollisionInfo {
        public CollisionInfo(bool colliding, bool inside) {
            Colliding = colliding;
            Inside = inside;
        }

        public readonly bool Colliding,Inside;

    }
}