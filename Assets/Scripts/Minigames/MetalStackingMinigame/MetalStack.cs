using System;
using UnityEngine;

public class MetalStack : MonoBehaviour
{
    [Header("Drop Physics")]
    [SerializeField] private float dropVelocityBoost = 5f; // initial downward speed on release

    [Header("Stick Behavior")]
    [SerializeField] private float minStickSpeed = 0f; // 0 = always stick on first contact
    [SerializeField] private bool destroyRigidbodyOnStick = true; // fully static after stick

    [Header("Cut Alignment")]
    [SerializeField] private bool cutOnStick = true; // enable auto-cut on Z to align with the piece below
    [SerializeField] private float minOverlapZ = 0.001f; // minimal Z overlap to consider valid (in world units)
    [SerializeField] private float yStackTolerance = 0.01f; // how much higher this piece must be to consider it "on top"

    // Raised when this piece sticks. Parameter is the MetalStack it stuck to (can be null if non-MetalStack).
    public event Action<MetalStack> StuckTo;

    private Rigidbody _rb;
    private bool _isHeld;
    private bool _isStuck;

    // Cached for collision ignoring with the claw
    private Collider[] _clawColliders;
    private Collider[] _pieceColliders;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (_rb == null)
        {
            _rb = gameObject.AddComponent<Rigidbody>();
        }

        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.linearDamping = 0.1f;
        _rb.angularDamping = 0.5f;

        // Always freeze rotation
        _rb.constraints = RigidbodyConstraints.FreezeRotation;

        _pieceColliders = GetComponentsInChildren<Collider>(true);
    }

    // Attach under claw, lock physics, and ignore collision with the claw
    public void AttachToClaw(Transform clawTransform, Vector3 localOffset, GameObject clawObject)
    {
        transform.SetParent(clawTransform, false);
        transform.localPosition = localOffset;
        transform.localRotation = Quaternion.identity;

        _rb.isKinematic = true;
        _rb.useGravity = false;

        // Keep rotation frozen while held
        _rb.constraints = RigidbodyConstraints.FreezeRotation;

        _isHeld = true;
        _isStuck = false;

        // Permanently ignore collisions with the claw
        _clawColliders = clawObject != null ? clawObject.GetComponentsInChildren<Collider>(true) : null;
        SetIgnoreClawCollision(true);
    }

    // Release from claw and let physics take over
    public void ReleaseFromClaw()
    {
        if (!_isHeld)
        {
            return;
        }

        _isHeld = false;
        transform.SetParent(null, true);

        _rb.isKinematic = false;
        _rb.useGravity = true;

        // Keep rotation frozen while falling
        _rb.constraints = RigidbodyConstraints.FreezeRotation;

        if (dropVelocityBoost > 0f)
        {
            _rb.linearVelocity += Vector3.down * dropVelocityBoost;
        }

    }

    private void OnCollisionEnter(Collision collision)
    {
        TryStick(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryStick(collision);
    }

    private void TryStick(Collision collision)
    {
        if (_isStuck || _isHeld)
        {
            return;
        }

        float speed = _rb.linearVelocity.magnitude;
        if (speed >= minStickSpeed)
        {
            StickTo(collision);
        }
    }

    private void StickTo(Collision collision)
    {
        _isStuck = true;

        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        // Slight push into the surface to avoid post-solve separation
        if (collision.contactCount > 0)
        {
            var contact = collision.GetContact(0);
            transform.position -= contact.normal * 0.001f;
        }

        // Hard lock
        _rb.isKinematic = true;
        _rb.useGravity = false;
        _rb.constraints = RigidbodyConstraints.FreezeAll;
        _rb.Sleep();

        // Identify what we stuck to (may be null or a non-MetalStack)
        MetalStack otherStack = null;
        if (collision.collider != null)
        {
            otherStack = collision.collider.GetComponentInParent<MetalStack>();
        }

        // Align/cut on Z if we stuck on top of another MetalStack
        if (cutOnStick && otherStack != null && otherStack != this)
        {
            // Ensure we are on top of the other piece (by Y)
            if (transform.position.y >= otherStack.transform.position.y + yStackTolerance)
            {
                AlignZWithinBelow(otherStack.transform);
            }
        }

        // Notify listeners
        StuckTo?.Invoke(otherStack);

        if (destroyRigidbodyOnStick)
        {
            Destroy(_rb);
            _rb = null;
        }
    }

    private void AlignZWithinBelow(Transform below)
    {
        Bounds topBounds;
        Bounds bottomBounds;

        if (!TryGetCombinedBounds(transform, out topBounds))
        {
            return;
        }

        if (!TryGetCombinedBounds(below, out bottomBounds))
        {
            return;
        }

        float topMinZ = topBounds.min.z;
        float topMaxZ = topBounds.max.z;
        float bottomMinZ = bottomBounds.min.z;
        float bottomMaxZ = bottomBounds.max.z;

        // Current world Z size of the top piece
        float currentTopLenZ = Mathf.Max(0f, topMaxZ - topMinZ);

        // Overlap on Z between top and bottom
        float overlapZ = Mathf.Max(0f, Mathf.Min(topMaxZ, bottomMaxZ) - Mathf.Max(topMinZ, bottomMinZ));

        if (overlapZ < minOverlapZ || currentTopLenZ <= Mathf.Epsilon)
        {
            // No valid overlap; do nothing (or could hide/destroy). Keeping as-is per request.
            return;
        }

        // If the top piece extends outside the bottom bounds on Z, "cut" by scaling on Z
        if (overlapZ < currentTopLenZ - 1e-5f)
        {
            float scaleFactor = Mathf.Clamp01(overlapZ / currentTopLenZ);

            // Apply Z scale
            var ls = transform.localScale;
            ls.z *= scaleFactor;
            transform.localScale = ls;

            // Recompute bounds after scaling to reposition accurately
            if (!TryGetCombinedBounds(transform, out Bounds topAfter))
            {
                return;
            }

            float newHalfLen = overlapZ * 0.5f;
            float targetCenterZ;

            // If it was overhanging +Z, clamp to bottom's max edge
            if (topMaxZ > bottomMaxZ + 1e-5f)
            {
                targetCenterZ = bottomMaxZ - newHalfLen;
            }
            // If it was overhanging -Z, clamp to bottom's min edge
            else if (topMinZ < bottomMinZ - 1e-5f)
            {
                targetCenterZ = bottomMinZ + newHalfLen;
            }
            else
            {
                // Center within bottom if already inside but bigger for some reason
                targetCenterZ = Mathf.Clamp(topAfter.center.z, bottomMinZ + newHalfLen, bottomMaxZ - newHalfLen);
            }

            // Shift by delta so that the new bounds center aligns with target
            float deltaZ = targetCenterZ - topAfter.center.z;
            if (Mathf.Abs(deltaZ) > 1e-6f)
            {
                var pos = transform.position;
                pos.z += deltaZ;
                transform.position = pos;
            }
        }
    }

    private static bool TryGetCombinedBounds(Transform root, out Bounds bounds)
    {
        // Prefer renderers for visual alignment
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers != null && renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }
            return true;
        }

        // Fallback to collider bounds
        var colliders = root.GetComponentsInChildren<Collider>(true);
        if (colliders != null && colliders.Length > 0)
        {
            bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    bounds.Encapsulate(colliders[i].bounds);
                }
            }
            return true;
        }

        bounds = new Bounds();
        return false;
        }

    private void SetIgnoreClawCollision(bool ignore)
    {
        if (_pieceColliders == null || _clawColliders == null)
        {
            return;
        }

        for (int i = 0; i < _pieceColliders.Length; i++)
        {
            var pc = _pieceColliders[i];
            if (pc == null) continue;

            for (int j = 0; j < _clawColliders.Length; j++)
            {
                var cc = _clawColliders[j];
                if (cc == null) continue;

                Physics.IgnoreCollision(pc, cc, ignore);
            }
        }
    }
}
