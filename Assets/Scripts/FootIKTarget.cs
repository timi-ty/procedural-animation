using UnityEngine;
using UnityEngine.Animations.Rigging;

//***//Each foot runs an instance of this script for convenience. The script operates a lot like a basic Transform with a few crucial add ons.
public class FootIKTarget : MonoBehaviour
{
    [HideInInspector]
    public Vector3 position { get { return transform.position; } set { transform.position = value; } }
    [HideInInspector]
    public Quaternion rotation { get { return transform.rotation; } set { transform.rotation = value; } }
    [HideInInspector]
    public Transform mTransform { get { return transform; } }
    public Transform tip { 
        get { 
            if (mTwoBoneIKConstraint) return mTwoBoneIKConstraint.data.tip; 
            else 
            {
                Debug.LogWarning("Tip transform is not yet ready.");
                return null; 
            } 
        } 
    }
    public Vector3 stablePosition { get; set; } //***//the position of this foot the last time it was touching the ground.

    public LimbSide limbSide;

    private TwoBoneIKConstraint mTwoBoneIKConstraint;

    [Tooltip("Must be the same for every FootIKTarget.")]
    public Transform root; //***//the centremost transform (core) of the skeleton holding the feet.
    private Vector3 defaultLimbRelPosVector { get; set; } //***//the initial difference between this foot's positon and the root's position.
    private float theta { get; set; } //***//angle between this limb's position vector and the root's forward vector.

    private void OnEnable()
    {
        //Get the two bone IK component running for the foor.
        mTwoBoneIKConstraint = GetComponent<TwoBoneIKConstraint>();

        //initialize the position of the foot target as the tip of the two bone ik.
        transform.position = tip.position;
    }

    private void Start()
    {
        //***//initialize defaultLimbRelPosVector (a constant).
        defaultLimbRelPosVector = transform.position - root.position;

        //***//initialize theta (a constant).
        theta = Vector2.SignedAngle(new Vector2(defaultLimbRelPosVector.x, defaultLimbRelPosVector.z), new Vector2(root.forward.x, root.forward.z));
    }

    //***//Gets the rest position (floor position) needed for this foot for any given "moveDirection".
    public Vector3 GetRestPosition(Vector3 moveDirection)
    {
        //the absolute planar angle between the root's forward vector and world forward vector.
        float phi = Vector2.SignedAngle(new Vector2(moveDirection.x, moveDirection.z), Vector2.up);
        ////the absolute planar angle between the foot's forward vector and world forward vector.
        float psi = (theta + phi) * Mathf.Deg2Rad;

        float magnitude = new Vector2(defaultLimbRelPosVector.x, defaultLimbRelPosVector.z).magnitude;

        Vector3 raycastOrigin = root.transform.position + new Vector3(Mathf.Sin(psi), 100, Mathf.Cos(psi)) * magnitude;

        bool foundRest = Physics.Raycast(raycastOrigin, Vector3.down, out RaycastHit rest, 500, Utility.groundLayerMask);
        if (foundRest)
        {
            return rest.point;
        }
        return position;
    }
}