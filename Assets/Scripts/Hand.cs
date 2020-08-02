using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SteamVR_TrackedObject))]
public class Hand : MonoBehaviour {

    /// <summary>
    /// The point at which to attach the object we pick up on the hands.
    /// </summary>
    public Rigidbody AttachPoint;
    /// <summary>
    /// When true, the object will be picked up based on where the hand
    /// touched the object.  When false, the object will attach to the contact point
    /// assigned.
    /// </summary>
    public bool IgnoreContactPoint = false;
    /// <summary>
    /// The thickness of the teleporting laser.
    /// </summary>
    public float LaserThickness = 0.002f;
    /// <summary>
    /// The color of the laser when the touchpad is touched.
    /// </summary>
    public Color TeleportStartLaserColor = Color.blue;
    /// <summary>
    /// The color of the laser when the touchpad is pressed.
    /// </summary>
    public Color TeleportLaserColor = Color.red;
    /// <summary>
    /// The maximum distance the player can teleport.
    /// </summary>
    public float MaxTeleportDistance = 100.0f;

    /// <summary>
    /// Manages the state of the hand.
    /// </summary>
    private enum State
    {
        EMPTY,
        TOUCHING,
        HOLDING
    };
    /// <summary>
    /// The hands current state.
    /// </summary>
    private State mHandState = State.EMPTY;

    /// <summary>
    /// Our actual controller device.  This is used to handle button presses, velocities, etc.
    /// </summary>
    private SteamVR_Controller.Device mControllerDevice;
    /// <summary>
    /// The tracked object representing our controller.  This is used to get the
    /// controller device.
    /// </summary>
    private SteamVR_TrackedObject mTrackedObj;

    /// <summary>
    /// The object currently being held.
    /// </summary>
    private Rigidbody mHeldObject;
    /// <summary>
    /// A temporary joint connect the object we are holding to our hand.
    /// </summary>
    private FixedJoint mTempJoint;

    /// <summary>
    /// The laser's physical object (just a block).
    /// </summary>
    private GameObject mLaser;

    /// <summary>
    /// Initializes a hand's starting properties.
    /// </summary>
	void Start () {
        mHandState = State.EMPTY;
        mTrackedObj = GetComponent<SteamVR_TrackedObject>();
        mControllerDevice = SteamVR_Controller.Input((int)mTrackedObj.index);

        mLaser = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mLaser.transform.parent = this.transform;
        mLaser.transform.localScale = new Vector3(0f, 0f, 0f);
        mLaser.transform.localPosition = new Vector3(0.0f, 0.0f, 0f);
        mLaser.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Unlit/Color"));
        mLaser.GetComponent<MeshRenderer>().material.SetColor("_Color", TeleportStartLaserColor);
        mLaser.GetComponent<BoxCollider>().enabled = false;
    }

    /// <summary>
    /// Update called in sync with physics.
    /// </summary>
    void FixedUpdate () {
        updateHand();
        updateLaser();
	}

    /// <summary>
    /// Throws the object the hand was holding.
    /// </summary>
    private void throwObject()
    {
        var origin = mTrackedObj.origin ? mTrackedObj.origin : mTrackedObj.transform.parent;
        if (origin != null)
        {
            mHeldObject.velocity = origin.TransformVector(mControllerDevice.velocity);
            mHeldObject.angularVelocity = origin.TransformVector(mControllerDevice.angularVelocity);
        }
        else
        {
            mHeldObject.velocity = mControllerDevice.velocity;
            mHeldObject.angularVelocity = mControllerDevice.angularVelocity;
        }
        mHeldObject.maxAngularVelocity = mHeldObject.angularVelocity.magnitude;
    }

    /// <summary>
    /// Updates the hand based on its state.
    /// </summary>
    private void updateHand()
    {
        switch (mHandState)
        {
            case State.TOUCHING:
                if (mTempJoint == null && mControllerDevice.GetPress(SteamVR_Controller.ButtonMask.Grip))
                {
                    mHeldObject.velocity = Vector3.zero;
                    mTempJoint = mHeldObject.gameObject.AddComponent<FixedJoint>();
                    if (IgnoreContactPoint)
                    {
                        mHeldObject.transform.position = AttachPoint.transform.position;
                    }
                    mTempJoint.connectedBody = AttachPoint;
                    mHandState = State.HOLDING;
                }
                break;
            case State.HOLDING:
                if (mTempJoint != null && mControllerDevice.GetPressUp(SteamVR_Controller.ButtonMask.Grip))
                {
                    Object.DestroyImmediate(mTempJoint);
                    mTempJoint = null;
                    throwObject();
                    mHandState = State.EMPTY;
                }
                break;
        }
    }

    /// <summary>
    /// Updates the teleport laser in each hand.
    /// </summary>
    private void updateLaser()
    {
        if (mControllerDevice != null)
        {
            Ray raycast = new Ray(transform.position, transform.forward);
            RaycastHit hitInfo;
            bool hit = Physics.Raycast(raycast, out hitInfo, MaxTeleportDistance);
            float distance = hit ? hitInfo.distance : MaxTeleportDistance;
            if (mControllerDevice.GetTouch(SteamVR_Controller.ButtonMask.Touchpad))
            {
                mLaser.transform.localScale = new Vector3(LaserThickness, LaserThickness, distance);
                mLaser.transform.localPosition = new Vector3(0f, 0f, distance / 2f);
            }
            else
            {
                mLaser.transform.localScale = new Vector3(0f, 0f, 0f);
                mLaser.transform.localPosition = new Vector3(0f, 0f, 0f);
            }
            if (mControllerDevice.GetPress(SteamVR_Controller.ButtonMask.Touchpad))
            {
                if (hit && hitInfo.collider.gameObject.layer == LayerMask.NameToLayer("teleportable"))
                {
                    mLaser.GetComponent<MeshRenderer>().material.SetColor("_Color", TeleportLaserColor);
                }
                else
                {
                    mLaser.GetComponent<MeshRenderer>().material.SetColor("_Color", TeleportStartLaserColor);
                }
            }
            if (mControllerDevice.GetPressUp(SteamVR_Controller.ButtonMask.Touchpad))
            {
                if (hit && hitInfo.collider.gameObject.layer == LayerMask.NameToLayer("teleportable"))
                {
                    float originalY = transform.parent.position.y;
                    transform.parent.position = transform.parent.position + raycast.direction * distance;
                    transform.parent.position = new Vector3(transform.parent.position.x, originalY, transform.parent.position.z);
                }
                mLaser.GetComponent<MeshRenderer>().material.SetColor("_Color", TeleportStartLaserColor);
            }
        }
    }

    /// <summary>
    /// Checks for a collision with the hand and
    /// changes the state based on it, also storing
    /// the object we touched.
    /// </summary>
    /// <param name="collider">The object we touched.</param>
    void OnTriggerEnter(Collider collider)
    {
        if (mHandState == State.EMPTY)
        {
            GameObject temp = collider.gameObject;
            while (temp.GetComponent<Rigidbody>() == null && temp.transform.parent != null)
            {
                temp = temp.transform.parent.gameObject;
            }
            if (temp != null && temp.layer == LayerMask.NameToLayer("grabbable") && 
                temp.GetComponent<Rigidbody>() != null)
            {
                mHeldObject = temp.GetComponent<Rigidbody>();
                mHandState = State.TOUCHING;
                mControllerDevice.TriggerHapticPulse(2000);
            }
        }
    }

    /// <summary>
    /// When we are no longer touching an object or holding
    /// and object, clean up the current state and remove the object
    /// we let go of/stopped touching.
    /// </summary>
    /// <param name="collider">The object we are no longer touching.</param>
    void OnTriggerExit(Collider collider)
    {
        if (mHandState != State.HOLDING)
        {
            if (collider.gameObject.layer == LayerMask.NameToLayer("grabbable"))
            {
                mHeldObject = null;
                mHandState = State.EMPTY;
            }
        }
    }
}
