using UnityEngine;


[RequireComponent(typeof(Camera))]
public class OrbitCamera : MonoBehaviour
{
    [SerializeField]
    Transform focus = default;

    [SerializeField, Range(1f, 20f)]
    float distance = 5f;
    [SerializeField, Min(0f)] // Always keeping the sphere in exact focus too rigid. Even smallest motion will be copied. Try Relaxing
    float focusRadius = 1f;
    Vector3 focusPoint, previousFocusPoint; // Criteria used to align cameras varies. We'll base it solely on focus point movement
    // Look in the direction the focus was last heading. We'll need to know both current and previous focus point. So have UpdateFocus
    // set field for both. 
    [SerializeField, Range(0f, 1f)]
    float focusCentering = 0.5f;
    Vector2 orbitAngles = new Vector2(45f, 0f);
    [SerializeField, Range(1f, 360f)]
    float rotationSpeed = 90f;
    [SerializeField, Range(-89f, 89f)] // Serialize field to clamp how much you can vertically rotate so you don't get disorientated.
    float minVerticalAngle = -30f, maxVerticalAngle = 60f;
    [SerializeField, Min(0f)]
    float alignDelay = 5f; // Orbit cameras align themselves to stay behind player's avatar. We do this by adjusting horizontal orbit angle.
    // Important player can override this at all times and that it doesn't kick back in. So we add a delay. 
    float lastManualRotationTime; // Keep track of manual rotation last time. Rely on unscaled time.
    [SerializeField, Range(0f, 90f)]
    float alignSmoothRange = 45f; // Scale linearly rotation speed scale. 
    Camera regularCamera;
    [SerializeField]
    LayerMask obstructionMask = -1; // layer mask 
    private void OnValidate()
    {
        if (maxVerticalAngle < minVerticalAngle)
        {
            maxVerticalAngle = minVerticalAngle; // Max should never drop below min. 
        }
    }

    private void Awake()
    {
        regularCamera = GetComponent<Camera>(); // Box cast
        focusPoint = focus.position; // Initialize focus point to obj position in awake. 
        transform.localRotation = Quaternion.Euler(orbitAngles); // Ensure that initial rotation matches orbit angles in Awake.
    }
    private void LateUpdate()
    {
        UpdateFocusPoint();
        
        //Vector3 focusPoint = focus.position;
        Quaternion lookRotation;
        if (ManualRotation() || AutomaticRotation())
        {
            ConstrainAngles();
            lookRotation = Quaternion.Euler(orbitAngles);
        }
        else
        {
            lookRotation = transform.localRotation;
        }
        Vector3 lookDirection = lookRotation * Vector3.forward;
        Vector3 lookPosition = focusPoint - lookDirection * distance;

        Vector3 rectOffset = lookDirection * regularCamera.nearClipPlane;
        Vector3 rectPosition = lookPosition + rectOffset;
        Vector3 castFrom = focus.position;
        Vector3 castLine = rectPosition - castFrom;
        float castDistance = castLine.magnitude;
        Vector3 castDirection = castLine / castDistance;

        //if (Physics.Raycast(// cast ray from focus pint to where we want to place camera
        //    focusPoint, -lookDirection, out RaycastHit hit, distance))
        // Instead of raycasting boxcast
        if(Physics.BoxCast(castFrom, CameraHalfExtends, castDirection, out RaycastHit hit, 
            lookRotation, castDistance, obstructionMask))
        {
            rectPosition = castFrom + castDirection * hit.distance;
            lookPosition = rectPosition - rectOffset; // if hit then use raycast distance instead of configured distance
        }
        transform.SetPositionAndRotation(lookPosition, lookRotation);
        // Even though we used this to minimize clipping into gtetometry, it can still end up inside geometry. near plane rectangle
        // will remain outside. This could fail if box cast already inside of geomytry. If focus object intersects geomytry, the camera
        // will as well.
    }
    void UpdateFocusPoint()
    {
        // update the focus position
        previousFocusPoint = focusPoint;
        Vector3 targetPoint = focus.position;
        if (focusRadius > 0f) // if focus radius is positive
        {
            // Check distance between the target and current focus points is greater than radius
            float distance = Vector3.Distance(targetPoint, focusPoint);
            float t = 1f;
            if (distance > 0.01f && focusCentering > 0f)
            {
                t = Mathf.Pow(1f - focusCentering, Time.unscaledDeltaTime);
            }
            if (distance > focusRadius) // if so
            {
                // Pull focus toward target until distance matches radius. Interpolate from target point to current point.
                // Radius divided by current distance as interpolator. Otherwise set the focus point as before.
                //focusPoint = Vector3.Lerp(targetPoint, focusPoint, focusRadius / distance);
                t = Mathf.Min(t, focusRadius / distance);
            }
            focusPoint = Vector3.Lerp(targetPoint, focusPoint, t);
        }
        else
        {
            focusPoint = targetPoint;
        }
        
    }
    bool ManualRotation() // Allows rotation of Camera while keeping movement of sphere bound to world space.
    {
        // Retrieves an input vector. I defined Vertical Camera and Horizontal Camera input axes for this. bound to the thir and fourth axis.
        // Its a good idea to make snesitivity configurable ingame and allow flipping of axis direction
        // If there an input exceeding some small epsiolon like 0.001, then add the input to the orb angles scaled by rotations speed and
        // unscaledDeltatime. We make this independent of game time.
        Vector2 input = new Vector2(
            Input.GetAxis("Vertical Camera"), // 3rd Axis correspond with i and k
            Input.GetAxis("Horizontal Camera") // 4th Axis correspond with q and e
            );
        const float e = 0.001f;
        if(input.x < -e || input.x > e || input.y < -e || input.y > e)
        {
            orbitAngles += rotationSpeed * Time.unscaledDeltaTime * input;
            lastManualRotationTime = Time.unscaledTime;
            return true;
        }
        return false;
    }
    void ConstrainAngles() // Clamps vertical orbit angle to range value in serialized field 
    { // Horizontal Orbit will have no limits, but need to ensure angle stays within 0-360.
        orbitAngles.x =
            Mathf.Clamp(orbitAngles.x, minVerticalAngle, maxVerticalAngle);
        if(orbitAngles.y < 0f)
        {
            orbitAngles.y += 360f;
        }
        else if(orbitAngles.y >= 360f)
        {
            orbitAngles.y -= 360f;
        }
    }
    bool AutomaticRotation()
    {
        if (Time.unscaledTime - lastManualRotationTime < alignDelay)
        {
            return false;
        }
        // Calculate the movement vector for current frame. Only rotating horizontally we only need XZ Plane 2D movements. If the square
        // magnitude of movement vector is les than 0.0001 don't rotate
        Vector2 movement = new Vector2(
            focusPoint.x - previousFocusPoint.x,
            focusPoint.z - previousFocusPoint.z);
        float movementDeltaSqr = movement.sqrMagnitude;
        if (movementDeltaSqr <0.0001f)
        {
            return false;
        }
        // Use getangle to get heading angle, passing it normalized movement vector. We have squared its magnitude, its more efficient 
        // to do the normalization ourselves.
        float headingAngle = GetAngle(movement / Mathf.Sqrt(movementDeltaSqr));
        float deltaAbs = Mathf.Abs(Mathf.DeltaAngle(orbitAngles.y, headingAngle));
        float rotationChange = rotationSpeed * Mathf.Min(Time.unscaledDeltaTime, movementDeltaSqr);
        if (deltaAbs < alignSmoothRange)
        {
            rotationChange *= deltaAbs / alignSmoothRange;
        }
        else if (180f - deltaAbs < alignSmoothRange)
        {
            rotationChange *= (180f-deltaAbs) / alignSmoothRange;
        }
        orbitAngles.y =
            Mathf.MoveTowardsAngle(orbitAngles.y, headingAngle, rotationChange); // Smooth Alignment, instead of snapping to match heading
        // Slow it down, using configured rotation speed for automatic rotation. Mimics manual rotation. We can use Mathf.movetowardsangle
        // which works like MoveTowards, but works with 0-360 range. Note: Max rotation speed is used for small realignments. 
        return true;
    }

    // We have to figure out horiz angle matching current direction
    static float GetAngle(Vector2 direction)
    {
        float angle = Mathf.Acos(direction.y) * Mathf.Rad2Deg; // Y Component is a cosine of the angle, so put it through acos, convert rad
        // 2 degrees. But angle can represent clockwise or counterclockwise rotation. We can look at X component to know which. If X is neg
        // then its counterclockwise, subtract angle from 360.
        return direction.x < 0f ? 360f - angle : angle;
    }
    Vector3 CameraHalfExtends // A box cast requires 3d vector contains half extends of a box, 1/2 whd
    {
        get
        {
            Vector3 halfExtends;
            halfExtends.y =
                regularCamera.nearClipPlane *
                Mathf.Tan(0.5f * Mathf.Deg2Rad * regularCamera.fieldOfView); // half height, tan of half camera fov angle in rad scaled
                // near clip plane distance. 
            halfExtends.x = halfExtends.y * regularCamera.aspect; // half width is scaled by camera's aspect ratio
            halfExtends.z = 0f; // depth is 0
            return halfExtends; // you could cache and calculate when needed, but calculating every frame ensures it always work.
        }
    }
}
