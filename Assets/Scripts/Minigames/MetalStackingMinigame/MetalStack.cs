using UnityEngine;

public class MetalStack : MonoBehaviour
{
    [Header("Drop Physics")]
    [SerializeField] private float dropVelocityBoost = 5f; // initial downward speed on release

    [Header("Stick Behavior")]
    [SerializeField] private float minStickSpeed = 0f; // 0 = always stick on first contact
    [SerializeField] private bool destroyRigidbodyOnStick = true; // fully static after stick

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

        // Correct Unity physics settings (no linearVelocity/linearDamping)
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.linearDamping = 0.1f;
        _rb.angularDamping = 0.5f;

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
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

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

        if (dropVelocityBoost > 0f)
        {
            _rb.linearVelocity += Vector3.down * dropVelocityBoost;
        }

        // Keep ignoring collisions with the claw (as requested)
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

        if (destroyRigidbodyOnStick)
        {
            Destroy(_rb);
            _rb = null;
        }
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
