using System.Collections;
using UnityEngine;


/*

---- Player Movement Script. Created by Secrart 2022. ----


The general plan for this was to obtain that quake/half-life "physical movement" feel. 
Instead of starting at your top speed or coming to an immediate stop, the player instead
slides across the level with a "friction" feel. 

To obtain this feel I figured out you could simply have a constant direction the player picked to 
move in and a force variable that the player is pushed in.

The direction will always be a non-zero value and the force pushes the player in the selected
direction. When the player pushes any of the selected movement keys the force has a positive 
value slowly added to it, instead of it being set to a constant.
         

TODO: 
    1. allow variable height
    2. allow an external force to be applied to the player (and have it fall off over time)
    3. get a life.
*/



[RequireComponent(typeof(CharacterController))]
public class SecrartsCharacterControllerSetup : MonoBehaviour
{

    [Header("Mouse Settings")]
    public float MouseSensitivity = 2.5f;

    [Header("Player Settings")]
    [Tooltip("The player walkspeed")]
    public float speed = 3.0f;
    [Tooltip("How high the player jumps")]
    public float jumpPower = 3.0f;
    [Tooltip("How quickly the player builds speed")]
    public float speedRampUpConstant = 5.0f;
    [Tooltip("How quickly the player loses speed")]
    public float speedRampDownConstant = 5.0f;

    [Header("Camera Settings")]
    [Tooltip("Makes the character controller generate a camera at runtime (if a camera hasn't already been set)")]
    public bool GenerateCamera = false;
    [Tooltip("The camera the player will use")]
    public new Camera camera;
    
    [Header("Gravity Settings")]
    [Tooltip("Maximum velocity the player can fall at")]
    public float gravity = 9.81f;
    [Tooltip("How fast the player will build fall velocity")]
    public float fallVelocityRamp = 1.0f;
    
    [Header("Debug")]
    [Tooltip("This will draw a (selected) mesh gizmo for player location identification.")]
    public bool drawToolMesh = false;
    public Mesh playerMesh;

    // player components
    private CharacterController cc;



    //Controls. 
    private Vector3 direction; // Which way the player is moving.
    private float mouseX, mouseY; // Mouse position values.

    //Simple physics
    private float force; // Current player speed.
    private Vector3 currentDirection; // Current player direction.
    private Vector3 appliedVelocity; // Where da player goin?

    //Jump
    private Vector3 JumpVelocity = Vector3.zero;
    private bool Jumping = false;
    bool grounded = false;
    private Vector3 jumpDirection;

    //Slopes
    Vector3 slopeProduct = Vector3.zero;
    bool isOnSlope = false;

    //gravity
    float appliedGravitationalForce = 0.0f;
    

    //Slope debugging
    Vector3 normalVector;

    //Crouching
    bool isCrouched = false;


    //This handles a small amount of delay to make the input feel smoother (not modifyable because it has been fine-tuned)
    IEnumerator couroutine;
    private void Start()
    {
        
        if(GenerateCamera && (camera == null)) {

            GameObject gObj = new GameObject(); 
            gObj.AddComponent<Camera>();
            gObj.transform.parent = transform;
            gObj.transform.localPosition = new Vector3(0.0f, 1.5f, 0.0f);
            camera = gObj.GetComponent<Camera>();

        }

        GameObject gCollider = new GameObject();
        
        Rigidbody gRb = gCollider.AddComponent<Rigidbody>();
        gRb.useGravity = false;
        gRb.isKinematic = true;
        gRb.interpolation = RigidbodyInterpolation.None;
        gRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        gRb.freezeRotation = true;

        CapsuleCollider gCC = gCollider.AddComponent<CapsuleCollider>();
        gCC.isTrigger = true;
        gCC.radius = 1.56f;
        gCC.height = 9.04f;
        gCC.center = Vector3.zero;


        gCollider.AddComponent<SecrartsGroundedCollider>();
        gCollider.GetComponent<SecrartsGroundedCollider>().player = this;

        gCollider.layer = LayerMask.NameToLayer("Player");
        gCollider.name = "Ground Collider Check";
        gCollider.transform.parent = transform;
        gCollider.transform.localPosition = new Vector3(0.0f, 0.421f, 0.0f);
        gCollider.transform.localScale = new Vector3(0.319999993f,0.119999997f,0.310000002f);

        cc = GetComponent<CharacterController>();
        cc.center = new Vector3(0.0f, 1.0f, 0.0f);

        couroutine = directionCalculator();
        StartCoroutine(couroutine);

        Cursor.lockState = CursorLockMode.Locked;


    }

    private void Update()
    {

        mouseMovement();
        simpleMovementPhysics();
        handleJump();
        handleSlopes();
        handleGravity();
        handleCrouching();
        if (!isOnSlope)
            appliedVelocity = force * currentDirection;
        else
            appliedVelocity = force * slopeProduct;

        appliedVelocity = appliedVelocity + Vector3.down * appliedGravitationalForce;

        appliedVelocity = appliedVelocity + (JumpVelocity + jumpDirection);

        cc.Move(appliedVelocity * Time.deltaTime);

    }


    private void OnDrawGizmos()
    {


        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position + new Vector3(0, 1, 0), transform.position + currentDirection * 3 + new Vector3(0, 1, 0));
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + -transform.up);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + normalVector);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position + Vector3.up, transform.position + slopeProduct + Vector3.up);
        
        if (drawToolMesh) {
        Gizmos.color = Color.green;
        if(playerMesh != null)
            Gizmos.DrawMesh(playerMesh, 0, transform.position - Vector3.up, transform.rotation, Vector3.one);
        }
    }


    public void setGroundedValue(bool delta)
    {

        this.grounded = delta;

    }

    //a bit cheaty. just a more fine tunable way to have "physicy" movement
    private void simpleMovementPhysics()
    {

        
        //Check to see if any input keys are down. We don't care about the specific key.
        if ((Input.GetButton("Forward") || Input.GetButton("Backward") || Input.GetButton("Left") || Input.GetButton("Right")))
        {

                force += speedRampUpConstant * Time.deltaTime; //Slowly increase movement speed

        } else
        {

            force -= (force + speedRampDownConstant) * Time.deltaTime; // Slowly decrease movement speed

        }

        //Simply prevents the player from exceeding the max speed and prevents negative speed.
        force = Mathf.Clamp(force, 0, speed);


    }

    private void handleSlopes()
    {

        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, 0.5f))
        {
            if (hit.normal != Vector3.up)
            {
                normalVector = hit.normal;
                //This is the most painful thing ive ever done to my brain :D
                slopeProduct = Vector3.Cross(Vector3.Cross(currentDirection, -transform.up), (hit.normal));
                isOnSlope = true;

            }
            else
            {
                isOnSlope = false;
            }
        }
        else
        {
            isOnSlope = false;
        }
    }
    
    //Just incase it isn't clear this returns whether or not the force is 0.
    //For example its used in the weapon bob script to check if the player is moving.
    public bool forceIsNoneZero()
    {

        if (force > 0)
        {

            return true;

        }

        return false;

    }

    public float getForce()
    {

        return force;

    }
    
    //A lot less versatile than forceIsNoneZero() but is useful when you just wanna make
    //sure the player is pushing a movement key.

    // TODO: consider deprecation.
    public bool playerIsMoving()
    {

        return (Input.GetButton("Forward") || Input.GetButton("Backward") || Input.GetButton("Left") || Input.GetButton("Right"));
        

    }

    public Vector3 getDirection()
    {

        return direction;

    }

    //This is a fix for the "infinite jumping" bug. instead of the player script checking for a boolean value the ground check object calls it itself.
    public void StopJumping()
    {

        Jumping = false;
        JumpVelocity = Vector3.zero;
        jumpDirection = Vector3.zero;

    }

    /*Probably a bad idea but i wish unity would let us give other objects priority so I could fucking
     keep my ground collider ahead of my player script*/
    public void StopGravity()
    {

        appliedGravitationalForce = 0.0f;

    }

    private void handleGravity()
    {

        if(!grounded)
        {

            appliedGravitationalForce += (gravity * fallVelocityRamp) * Time.deltaTime;

        }else
        {

            appliedGravitationalForce = 0.0f;

        }
        appliedGravitationalForce = Mathf.Clamp(appliedGravitationalForce, 0.0f, 98.1f);


    }

    private Vector3 lastDirection = Vector3.zero;
    private Vector3 verticalDirection, horizontalDirection;
    private void calculateDirection()
    {

        if (Input.GetButton("Forward"))
        {


            verticalDirection = transform.forward;
            

        }
        else if (Input.GetButton("Backward"))
        {

            verticalDirection = -transform.forward;

        } else
        {

            verticalDirection = Vector3.zero;
          

        }

        if (Input.GetButton("Left"))
        {

            horizontalDirection = -transform.right;

        }
        else if (Input.GetButton("Right"))
        {

            horizontalDirection = transform.right;

        } else
        {


            horizontalDirection = Vector3.zero;
          
        }

        


        direction = Vector3.Normalize(verticalDirection + horizontalDirection);
        //Update current direction if the input direction changed.
        if (lastDirection != direction)
        {

            //Make sure the input direction is non-zero to ensure the current direction is non-zero
            if (direction != Vector3.zero)
            {

                currentDirection = direction;
                lastDirection = direction;

            }

        }


    }


    private void mouseMovement()
    {

        mouseY += Input.GetAxisRaw("Mouse Y") * MouseSensitivity;
        mouseY = Mathf.Clamp(mouseY, -90, 90);
        mouseX = Input.GetAxisRaw("Mouse X");

        //Rotate player and camera relative to mouse postions
        camera.transform.localRotation = Quaternion.Euler(new Vector3(-mouseY, 0, 0));
        transform.rotation *= Quaternion.Euler(new Vector3(0, mouseX * MouseSensitivity, 0));

    }


    private void handleJump()
    {

        if (Input.GetButton("Jump") && !Jumping)
        {
            JumpVelocity = transform.up * jumpPower;
            Jumping = true;
            if (!isOnSlope)
                jumpDirection = direction;
            else
            {

                if (playerIsMoving())
                    jumpDirection = slopeProduct;
                else
                    jumpDirection = direction;

            }
                
            grounded = false;

        }
       
    }

    private void handleCrouching()
    {

        if (Input.GetButtonDown("Crouch"))
        {

            isCrouched = !isCrouched;

            if (isCrouched)
            {
                cc.height = 1;
                cc.center = new Vector3(0, 0.5f, 0);
                camera.transform.localPosition = Vector3.up * 1.5f / 2;

            }else
            {

                RaycastHit hit;
                if (!Physics.Raycast(transform.position + Vector3.up, Vector3.up, out hit, 1.0f))
                {
                    cc.height = 2;
                    cc.center = new Vector3(0, 1.0f, 0);
                    camera.transform.localPosition = Vector3.up * 1.5f;
                }else
                {

                    isCrouched = true;
                    
                }
            }

        }

    }
    //Houston this boutta get dirty
    private IEnumerator directionCalculator()
    {
        while (true)
        {

            yield return new WaitForSeconds(0.09f);

            calculateDirection();
        }
    }

}

