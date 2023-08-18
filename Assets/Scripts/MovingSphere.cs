using UnityEditor.Build;
using UnityEngine;

public class MovingSphere : MonoBehaviour
{
    [SerializeField, Range(0f, 100f)]
    float maxSpeed = 10f;
    
    //[SerializeField]
    //Rect allowedArea = new Rect(-5f, -5f, 10f, 10f);
    //[SerializeField, Range(0f, 1f)]
    //float bounciness = 0.5f;
    Vector3 velocity, desiredVelocity;
    Rigidbody body;
    bool desiredJump;
    [SerializeField, Range(0f, 10f)]
    float jumpHeight = 2f;
    [SerializeField, Range(0, 5)]
    int maxAirJumps = 0;
    int jumpPhase;
    [SerializeField, Range(0f, 100f)]
    float maxAcceleration = 10f, maxAirAcceleration = 1f;
    [SerializeField, Range(0f, 90f)]
    float maxGroundAngle = 25f, maxStairsAngle = 50f;
    float minGroundDotProduct, minStairsDotProduct;
    int groundContactCount, steepContactCount;
    bool OnGround => groundContactCount > 0;
    bool OnSteep => steepContactCount > 0;
    Vector3 contactNormal, steepNormal;
    int stepsSinceLastGrounded, stepsSinceLastJump;
    [SerializeField]
    float maxSnapSpeed = 100f;
    [SerializeField, Min(0f)]
    float probeDistance = 1f;
    [SerializeField]
    LayerMask probeMask = -1, stairsMask = -1;
    [SerializeField]
    Transform playerInputSpace = default; // Make player movement relative to camera. 
    Vector3 upAxis;

    private void OnValidate()
    {
        minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
        minStairsDotProduct = Mathf.Cos(maxStairsAngle * Mathf.Deg2Rad);
    }
    void Awake()
    {
        body = GetComponent<Rigidbody>();
        OnValidate();
    }

    private void Update()
    {
        Vector2 playerInput;
        playerInput.x = Input.GetAxis("Horizontal");
        playerInput.y = Input.GetAxis("Vertical");
        //playerInput.Normalize(); Constraining, normalizing the input vector limits position to always lie on circle
        // unless input is neutral you end up at origin. so it limits the amount of positions. you can use vector2.clampmagnitude, 
        // vector that is either same or scaled down to provide the maximum allowing for more freedom
        playerInput = Vector2.ClampMagnitude(playerInput, 1f);
        // We're teleporting isntead of moving you need a displacement vector to dictate new position so we create an infinite iterative
        // sequence to Pn+1 = pn + d with p0 as start
        if (playerInputSpace) // If input space is assigned, we convert provided space to world space. Otherwise use world space.
        {   
            //Normalized Direction. Forward speed affected by the vertical orbit angle. Thus deviates from horizontal the slower the
            // sphere moves. That happens because we expected the desired velocity to lie in XZ plane. We can make it so by
            // retrieving forward and right vector from the player input space. Discard y, normalize. Then desired vvelocity becomes sum
            // of vectors scaled by player movement.
            Vector3 forward = playerInputSpace.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = playerInputSpace.right;
            right.y = 0f;
            right.Normalize();
            desiredVelocity =
                (forward * playerInput.y + right * playerInput.x) * maxSpeed;
        }
        else
        {
            desiredVelocity =
                new Vector3(playerInput.x, 0f, playerInput.y) * maxSpeed;
        }
        
        desiredJump |= Input.GetButtonDown("Jump");
        GetComponent<Renderer>().material.SetColor(
            "_Color", Color.white * (groundContactCount * 0.25f));
        GetComponent<Renderer>().material.SetColor(
            "_Color", OnGround ? Color.black : Color.white
            );
    }
    private void FixedUpdate()
    {
        upAxis = -Physics.gravity.normalized;
        //velocity = body.velocity;
        UpdateState();
        AdjustVelocity();
        //float acceleration = onGround ? maxAcceleration : maxAirAcceleration;
        //float maxSpeedChange = acceleration * Time.deltaTime;
        //if (velocity.x < desiredVelocity.x)
        //{
        //    velocity.x =
        //        Mathf.Min(velocity.x + maxSpeedChange, desiredVelocity.x);
        //}
        //else if (velocity.x > desiredVelocity.x){
        //    velocity.x =
        //        Mathf.Max(velocity.x - maxSpeedChange, desiredVelocity.x);
        //}
        //velocity.x = Mathf.MoveTowards(velocity.x, desiredVelocity.x, maxSpeedChange);
        //velocity.z = Mathf.MoveTowards(velocity.z, desiredVelocity.z, maxSpeedChange);
        //Vector3 displacement = velocity * Time.deltaTime;
        //transform.localPosition += displacement;
        //Vector3 newPosition = transform.localPosition + displacement;
        //if (!allowedArea.Contains(new Vector2(newPosition.x, newPosition.z)))
        //{
        //    //newPosition = transform.localPosition;
        //    newPosition.x =
        //        Mathf.Clamp(newPosition.x, allowedArea.xMin, allowedArea.xMax);
        //    newPosition.z = 
        //        Mathf.Clamp(newPosition.z, allowedArea.yMin, allowedArea.yMax);
        //}
        //if(newPosition.x < allowedArea.xMin)
        //{
        //    newPosition.x = allowedArea.xMin;
        //    velocity.x = -velocity.x * bounciness;
        //}
        //else if (newPosition.x > allowedArea.xMax) 
        //{ 
        //    newPosition.x = allowedArea.xMax;
        //    velocity.x = -velocity.x * bounciness;
        //}
        //if (newPosition.z < allowedArea.yMin)
        //{
        //    newPosition.z = allowedArea.yMin;
        //    velocity.z = -velocity.z * bounciness;
        //}
        //else if (newPosition.z > allowedArea.yMax)
        //{
        //    newPosition.z = allowedArea.yMax;
        //    velocity.z = -velocity.z * bounciness;
        //}
        //transform.localPosition = newPosition;


        // Sphere can move anywhere but its too fast its hard to control, its a consequence of adding the input vector each update.
        // Higher frame rate faster it goes, we do not want the frame rate to affect our input, if we use a const input we want const displacement 
        // regardless of framerate. 

        // A single frame represents a duration, how much time t passed between start of previous and current frame which can access
        // via time.delatime. Our displacement is thus actually d = it, we incorrectly assuemd that t is constant.

        // Displacement is measured in Unity units, which are one meter, but we multiply input by a duration expressed in seconds. 
        // Meterpresecond, so you have to have velocity v = i and then d = vt;
        if (desiredJump )
        {
            desiredJump = false;
            Jump();
        }
        body.velocity = velocity;
        //onGround = false;
        ClearState();
    }
    void ClearState()
    {
        //OnGround = false;
        groundContactCount = steepContactCount = 0;
        contactNormal = steepNormal = Vector3.zero;
    }
    void UpdateState()
    {
        stepsSinceLastGrounded += 1;
        stepsSinceLastJump += 1;
        velocity = body.velocity;
        if (OnGround || SnapToGround() || CheckSteepContacts())
        {
            stepsSinceLastGrounded = 0;
            if(stepsSinceLastJump > 1)
            {
                jumpPhase = 0;
            }
    
            if(groundContactCount > 1)
                contactNormal.Normalize();
        }
        else
        {
            contactNormal = upAxis;
        }
    }
   
    private void OnCollisionEnter(Collision collision)
    {
        EvaluateCollision(collision);
    }

    //private void OnCollisionExit()
    //{
    //    onGround = false;
    //}
    private void OnCollisionStay(Collision collision)
    {
        EvaluateCollision(collision);
    }
    void EvaluateCollision(Collision collision)
    {
        float minDot = GetMinDot(collision.gameObject.layer);
        for(int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            float upDot = Vector3.Dot(upAxis, normal);
            //onGround |= normal.y >= minGroundDotProduct;
            if (normal.y >= minDot)
            {
                groundContactCount += 1;
                contactNormal += normal;
            }
            else if (upDot > -0.01f)
            {
                steepContactCount += 1;
                steepNormal += normal;
            }
        }
    }
    void Jump()
    {
        Vector3 jumpDirection;
        if (OnGround)
        {
            jumpDirection = contactNormal;
        }
        else if (OnSteep)
        {
            jumpDirection = steepNormal;
            jumpPhase = 0;
        }
        else if (maxAirJumps > 0 && jumpPhase <= maxAirJumps)
        {
            jumpDirection = contactNormal;
            if(jumpPhase == 0)
            {
                jumpPhase = 1;
            }
        }
        else
        {
            return;
        }
        //if (OnGround || jumpPhase < maxAirJumps)
        stepsSinceLastJump = 0;
        jumpPhase += 1;
        float jumpSpeed = Mathf.Sqrt(2f * Physics.gravity.magnitude * jumpHeight);
        jumpDirection = (jumpDirection + upAxis).normalized;
        float alignedSpeed = Vector3.Dot(velocity, jumpDirection);
        if (alignedSpeed > 0f)
        {
            jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed,0f);
        }
        //velocity.y += jumpSpeed;
        velocity += jumpDirection * jumpSpeed;
    }
    Vector3 ProjectOnContactPlane(Vector3 vector)
    {
        return vector - contactNormal * Vector3.Dot(velocity, contactNormal);
    }
    void AdjustVelocity()
    {
        Vector3 xAxis = ProjectOnContactPlane(Vector3.right).normalized;
        Vector3 zAxis = ProjectOnContactPlane(Vector3.forward).normalized;
        float currentX = Vector3.Dot(velocity, xAxis);
        float currentZ = Vector3.Dot(velocity, zAxis);
        float acceleration = OnGround ? maxAcceleration : maxAirAcceleration;
        float maxSpeedChange = acceleration * Time.deltaTime;
        float newX =
            Mathf.MoveTowards(currentX, desiredVelocity.x, maxSpeedChange);
        float newZ =
            Mathf.MoveTowards(currentZ, desiredVelocity.z, maxSpeedChange);
        velocity += xAxis * (newX - currentX) + zAxis * (newZ - currentZ);
    }
    bool SnapToGround()
    {
        if(stepsSinceLastGrounded > 1 || stepsSinceLastJump <= 2)
        {
            return false;
        }
        float speed = velocity.magnitude;
        if (speed > maxSnapSpeed)
        {
            return false;
        }
        if (!Physics.Raycast(body.position, -upAxis, out RaycastHit hit, probeDistance, probeMask))
        {
            return false;
        }
        float upDot = Vector3.Dot(upAxis, hit.normal);
        if (upDot < GetMinDot(hit.collider.gameObject.layer))
        {
            return false;
        }
        groundContactCount = 1;
        contactNormal = hit.normal;
        float dot = Vector3.Dot(velocity, hit.normal);
        if (dot > 0f)
        {
            velocity = (velocity - hit.normal * dot).normalized * speed;
        }
        return true;
    }
    float GetMinDot(int layer)
    {
        return stairsMask != (stairsMask & (1<<layer)) ? minGroundDotProduct : minStairsDotProduct;
    }
    bool CheckSteepContacts()
    {
        if(steepContactCount > 1)
        {
            steepNormal.Normalize();
            float upDot = Vector3.Dot(upAxis, steepNormal);
            if (upDot >= minGroundDotProduct)
            {
                groundContactCount = 1;
                contactNormal = steepNormal;
                return true;
            }
        }
        return false;
    }
}
