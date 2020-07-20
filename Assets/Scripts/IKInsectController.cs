using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms;

public class IKInsectController : MonoBehaviour
{
    [Tooltip("Ensure you assign the FootIKTargets from fore limbs down to hind limbs without skipping.")]
    public List<FootIKTarget> footIKTargets = new List<FootIKTarget>(4);// the targets for the position of the feet

    public Transform skeleton;//the root of the skeleton
    public Transform rig; //the parent of the IK targets

    private Vector3[] stableFeetPositions = new Vector3[4];//the position of the feet the last time they were touching the ground

    
    private Vector3 defaultBodyPosOffset; // the initial body pos offset, stored because the body pos offset can shange
    private Vector3 bodyPosOffset; //the bodyPos offset is the differnece between the average stable feet position and the body position at the start.
    private Quaternion bodyRotOffset; //the body rotation offset is the differnece between the average stable feet rotation and the body rotation at the start.
    private Quaternion rigRotOffset; //the rig rotation offset is the differnece between the average stable feet rotation and the rig rotation at the start.

    public AnimationCurve stepCurveX;//
    public AnimationCurve stepCurveY;// Animation curves to control foot motion while taking steps
    public AnimationCurve stepCurveZ;//

    public AnimationCurve stepOffsetCurveX;//
    public AnimationCurve stepOffsetCurveY;// Just extra curves to make the animation look nicer. These curves must always start and end at zero
    public AnimationCurve stepOffsetCurveZ;// in order for them to have NO net effect on the animation

    public float stepsPerSecond; //speed of stepping

    public float maxStepHeight;
    public float stepStride; // how far each step goes

    internal bool isMoving;
    internal Vector2 moveDirection;

    [Header("Debug Utils")]
    public LineRenderer debugLine;
    public SpriteRenderer debugSprite;
    public List<Transform> debugSphere;
    [Range(0f, 1f)]
    public float debugTime;


    private void Start()
    {
        GetBodyOffset(); // the body offset is the differnece between the stable feet positions and the body position at the start.
        GetStableFeetPositions(); //Get the position of the feet the last time they were touching the ground

        isMoving = false;

        TestMove(); //Starts the procedural movement
    }

    private void Update()
    {
        //The commented out code below was to enable me record the steps for the video
        //It simply moves the feet with the debugTime slider.
        //Comment out TestMove() in start and then uncomment the code below to test it.

        //for (int i = 2; i < footIKTargets.Count; i++)
        //{
        //    if (debugTime == 0 || debugTime == 1)
        //    {
        //        debugSphere[i].gameObject.SetActive(true);
        //        debugSphere[i].position = footIKTargets[i].position;
        //    }
        //}
        //MoveLimb(0, debugTime);
        //MoveLimb(1, debugTime);
        ////MoveLimb(2, debugTime);
        ////MoveLimb(3, debugTime);
    }

    private void TestMove()
    {
        //Calling the move function with the desired movement direction
        Move(Vector3.forward);
    }

    public void Move(Vector2 direction)
    {
        if (!isMoving)
        {
            isMoving = true;

            float stepDuration = 1 / stepsPerSecond;

            //Two feet are offset at the beginning to make the steps perfectly symmetrical
            //Otherwise, motion will be biased towards the feet that stepped first.
            OffsetFoot(1);
            OffsetFoot(3);

            GetStableFeetPositions(); // Get the position of the feet the last time they were touching the ground

            StartCoroutine(TakeSteps(stepDuration)); // this coroutine to take the first steps. It calls itslef recursively to maintain the motion
        }

        moveDirection = direction;
    }

    public void Stop()
    {
        isMoving = false;
    }

    private IEnumerator TakeSteps(float duration)
    {
        //Forgive me, but instead of taking the steps one at a time like a normal person, I took them all together because I felt it would look more natural :P
        //Thus, I move the different feet with different "time" inputs to make them move in a specific pattern
        //Read the MoveFoot() function to undestand what the "time" input does

        float nTime = 0;// normalized time i.e time from (0 - 1), but really, mine goes from (0 - 2) because I joined the two steps together
        float delay = 0.425f; // Instead of moving two feet at the exact same time, to form the zig-zag pattern, I add small delay, because I felt it lookd better!
        bool cycled = false; // monitors when a new cycle starts. A cycle means when all four legs have completed their step.

        //I know this while loop might seem confusing, but it's really not. Basically, a single input "nTime" is manipulated
        //so that it passes different values to the different feet during movement.
        //When "nTime" is between 0 - 1, foot 0 and foot 2 are in motion but when "nTime" is between 1 - 2, foot 1 and foot 3 are in motion
        while (isMoving)
        {
            MoveFoot(0, Mathf.Clamp(nTime < 2 ? cycled ? 0 : nTime : nTime - 2, 0, 1));
            MoveFoot(2, Mathf.Clamp(nTime - delay, 0, 1));
            MoveFoot(1, Mathf.Clamp(nTime - 1, 0, 1));
            MoveFoot(3, Mathf.Clamp(nTime - delay - 1, 0, 1));

            if (nTime > 1 && !cycled)
            {
                UpdateStableFootPosition(0);
                cycled = true;
            }
            if (nTime >= 2 + delay)
            {
                nTime = delay;
                UpdateStableFootPosition(1);
                UpdateStableFootPosition(2);
                UpdateStableFootPosition(3);
                cycled = false;
            }

            nTime += Time.fixedDeltaTime / duration;

            yield return new WaitForFixedUpdate();
        }
    }

    private void MoveFoot(int limbIndex, float time)
    {
        FootIKTarget footIKTarget = footIKTargets[limbIndex]; // select the foot we are trying to control

        Vector3 referencePosition = stableFeetPositions[limbIndex]; // get the grounded position of that foot

        //Decide where to step using raycast
        RaycastHit nextStep;
        Vector3 raycastOrigin = referencePosition + transform.forward * stepStride + transform.up * 100;
        bool foundStep = Physics.Raycast(raycastOrigin, Vector3.down, out nextStep);
        if (nextStep.point.y - referencePosition.y > maxStepHeight)
        {
            raycastOrigin = referencePosition + transform.up * stepStride - transform.forward * 100;
            foundStep = Physics.Raycast(raycastOrigin, transform.forward, out nextStep);
        }

        if (!foundStep) return;


        /****NOTE!!! The rest of this function is just to control the animation. I did a lot of work to get a nice animation which you can AVOID!****/
        /***You could just use Vector3.MoveTowards() to move the feet like in the simpler alternative I worte below to save you from a lot of trouble****/

        //*** The simpler alternative code *** P.S. I didn't design the system for this, so the animation looks a bit weird, but technically, it works!

        //if (time > 0)
        //{
        //    if (time < 0.1f)
        //    {
        //        footIKTarget.position = referencePosition + Vector3.up * 0.2f;
        //    }
        //    else
        //    {
        //        footIKTarget.position = Vector3.MoveTowards(footIKTarget.position, nextStep.point, Time.deltaTime * 5);
        //        if (time == 1) footIKTarget.position = nextStep.point;
        //    }
        //}


        //*** The more complex but better looking animation code ***///

        //Obtain the non-animated world step
        Vector3 worldStepVector = nextStep.point - referencePosition;
        float horizontalStepMagnitude = new Vector2(worldStepVector.x, worldStepVector.z).magnitude;

        //Convert world step vector to a local step vector with local forward direction as reference forward direction
        float stepAngleOffset = Vector2.SignedAngle(new Vector2(worldStepVector.x, worldStepVector.z),
            new Vector2(transform.forward.x, transform.forward.z));
        Vector3 localStepVector = new Vector3(Mathf.Sin(stepAngleOffset * Mathf.Deg2Rad) * horizontalStepMagnitude,
            worldStepVector.y, Mathf.Cos(stepAngleOffset * Mathf.Deg2Rad) * horizontalStepMagnitude);


        //Sample animation curves taking note of the limb side. *The animation curves are designed with the world forward direction as reference forward direction
        int sideSign = footIKTarget.limbSide == LimbSide.Left ? 1 : -1;
        Vector3 stepProgress = new Vector3(stepCurveX.Evaluate(time) * sideSign, stepCurveY.Evaluate(time), stepCurveZ.Evaluate(time));
        Vector3 stepOffset = new Vector3(stepOffsetCurveX.Evaluate(time) * sideSign, stepOffsetCurveY.Evaluate(time), stepOffsetCurveZ.Evaluate(time));

        // The commented out code below simply enables the step spheres and lines you see in the video

        //if (stepProgress.magnitude > 1.5f)
        //{
        //debugLine.SetPosition(0, raycastOrigin);
        //debugLine.SetPosition(1, nextStep.point);
        //debugSphere[0].gameObject.SetActive(true);
        //debugSphere[0].position = nextStep.point;
        //}

        //if (t == 0 || t == 1)
        //{
        //    debugSphere[limbIndex].gameObject.SetActive(true);
        //    debugSphere[limbIndex].position = footIKTargets[limbIndex].position;
        //}

        //compute the animated step vector from the local step vector
        Vector3 animatedLocalStepVector = Vector3.Scale(localStepVector, stepProgress) + stepOffset;

        float animatedHorizontalStepMagnitude = new Vector2(animatedLocalStepVector.x, animatedLocalStepVector.z).magnitude;
        float animationAngleOffset = Vector2.SignedAngle(new Vector2(animatedLocalStepVector.x, animatedLocalStepVector.z),
            new Vector2(localStepVector.x, localStepVector.z));

        //Obtain the angle between the world forward direction and the local forward direction
        float forwardAngleOffset = Vector2.SignedAngle(new Vector2(transform.forward.x, transform.forward.z), Vector2.up);

        float totalAngleOffset = stepAngleOffset + animationAngleOffset + forwardAngleOffset;

        Vector3 animatedWorldStepVector = new Vector3(Mathf.Sin(totalAngleOffset * Mathf.Deg2Rad) * animatedHorizontalStepMagnitude,
            animatedLocalStepVector.y, Mathf.Cos(totalAngleOffset * Mathf.Deg2Rad) * animatedHorizontalStepMagnitude);

        footIKTarget.position = referencePosition + animatedWorldStepVector;

        BodyControl();
    }

    private void OffsetFoot(int limbIndex)
    {
        //Offsets foot by basically taking half a step

        FootIKTarget footIKTarget = footIKTargets[limbIndex];

        Vector3 referencePosition = stableFeetPositions[limbIndex];

        //Decide where to step
        RaycastHit nextStep;
        Vector3 raycastOrigin = referencePosition + transform.forward * stepStride/2 + transform.up * 100;
        bool foundStep = Physics.Raycast(raycastOrigin, Vector3.down, out nextStep);

        if (!foundStep) return;

        footIKTarget.position = nextStep.point;

        BodyControl();
    }

    private void GetStableFeetPositions() // Should only be called when you're certain that all feet are on the ground
    {
        for (int i = 0; i < footIKTargets.Count; i++)
        {
            stableFeetPositions[i] = footIKTargets[i].position;
        }
    }
    private void UpdateStableFootPosition(int index) // Called to update a single foot's stable position when you're not sure that the other feet are on the ground.
    {
        stableFeetPositions[index] = footIKTargets[index].position;
    }

    private void GetBodyOffset() // called at the beginning to get both the positional and the rotational offset between the body and the feet.
    {
        Vector3 cummulativeStableFeetPos = Vector3.zero;

        for (int i = 0; i < footIKTargets.Count; i++)
        {
            cummulativeStableFeetPos += footIKTargets[i].position;
        }

        Vector3 averageStableFeetPos = cummulativeStableFeetPos / (footIKTargets.Count);

        defaultBodyPosOffset = transform.position - averageStableFeetPos;

        bodyPosOffset = defaultBodyPosOffset;

        bodyRotOffset = skeleton.rotation;

        rigRotOffset = rig.rotation;
    }

    
    private void BodyControl() // Simply updates the position and rotation of the body based on the new feet positions
    {
        //Store all foot target positions before moving their parent, which is this transform
        Vector3[] tempTargetPositions = new Vector3[footIKTargets.Count];

        //Calculate the average stable feet position
        Vector3 cummulativeStableFeetPos = Vector3.zero;

        // Calculating the averages
        for (int i = 0; i < footIKTargets.Count; i++)
        {
            tempTargetPositions[i] = footIKTargets[i].position;

            cummulativeStableFeetPos += footIKTargets[i].position;
        }

        Vector3 averageStableFeetPos = cummulativeStableFeetPos / (footIKTargets.Count);

        //The position of the body is the average stable feet positions + the body offset
        transform.position = bodyPosOffset + averageStableFeetPos;

        //Uncomment the code below to draw a line that shows how the body moves realtive to the feet
        //debugLine.SetPosition(0, averageStableFeetPos);
        //debugLine.SetPosition(1, bodyPosOffset + averageStableFeetPos);

        //Restore foot target positions to cancel out parent's movement
        for (int i = 0; i < footIKTargets.Count; i++)
        {
            footIKTargets[i].position = tempTargetPositions[i];
        }

        RotationControl(); // updates the rotation of the body
    }

    private void RotationControl()
    {
        //Store all foot target positions before moving their parent, which is this transform
        Vector3[] tempTargetPositions = new Vector3[footIKTargets.Count];

        Vector3 cummulativeLeftFeetPos = Vector3.zero;
        Vector3 cummulativeRightFeetPos = Vector3.zero;

        Vector3 cummulativeFrontFeetPos = Vector3.zero;
        Vector3 cummulativeHindFeetPos = Vector3.zero;

        // Calculating the averages
        for (int i = 0; i < footIKTargets.Count; i++)
        {
            tempTargetPositions[i] = footIKTargets[i].position;

            if (footIKTargets[i].limbSide == LimbSide.Left)
            {
                cummulativeLeftFeetPos += footIKTargets[i].position;
            }
            else if (footIKTargets[i].limbSide == LimbSide.Right)
            {
                cummulativeRightFeetPos += footIKTargets[i].position;
            }

            if (i < footIKTargets.Count / 2)
            {
                cummulativeFrontFeetPos += footIKTargets[i].position;
            }
            else if (i >= footIKTargets.Count / 2)
            {
                cummulativeHindFeetPos += footIKTargets[i].position;
            }
        }

        Vector3 averageLeftFeetPos = cummulativeLeftFeetPos / (footIKTargets.Count/2);
        Vector3 averageRightFeetPos = cummulativeRightFeetPos / (footIKTargets.Count/2);

        Vector3 averageFrontFeetPos = cummulativeFrontFeetPos / (footIKTargets.Count / 2);
        Vector3 averageHindFeetPos = cummulativeHindFeetPos / (footIKTargets.Count / 2);

        //The azimuth vector is like the z rotation which is gotten by knowing wether the height difference between the average left feet positions and the average right feet positions
        Vector3 azimuthVector = (averageLeftFeetPos - averageRightFeetPos);
        //The elevation vector is like the x rotation which is gotten by knowing wether the height difference between the average front feet positions and the average hind feet positions
        Vector3 elevationVector = (averageFrontFeetPos - averageHindFeetPos);

        float azimuthAngle = Vector3.SignedAngle(transform.right, azimuthVector, transform.forward);
        float elevationAngle = Vector3.SignedAngle(transform.forward, elevationVector, transform.right);

        //Uncomment to three lines below see how the average front and hind feet positions affect the elevation
        //debugLine.SetPosition(0, averageFrontFeetPos);
        //debugLine.SetPosition(1, averageHindFeetPos);
        //Debug.Log("elevation: " + elevationAngle);

        Quaternion azimuth = Quaternion.AngleAxis(azimuthAngle, transform.forward);
        Quaternion elevation = Quaternion.AngleAxis(elevationAngle, transform.right);

        skeleton.rotation = Quaternion.Lerp(skeleton.rotation, azimuth * elevation * bodyRotOffset, Time.deltaTime);
        rig.rotation = Quaternion.Lerp(rig.rotation, azimuth * elevation * rigRotOffset, Time.deltaTime);

        //2 lines below is just some fine tuning, it can be ignored.
        Vector3 afterRotation = rig.up;
        bodyPosOffset = RotateByQuaternion(defaultBodyPosOffset, Quaternion.FromToRotation(Vector3.up, afterRotation));


        //Restore foot target positions to cancel out parent's movement
        for (int i = 0; i < footIKTargets.Count; i++)
        {
            footIKTargets[i].position = tempTargetPositions[i];
        }
    }


    Vector3 RotateByQuaternion(Vector3 vector, Quaternion quaternion)// A helper function that I wrote to rotate a vector by a quaternion. I almost never use it!
    {
        Quaternion inverseQ = Quaternion.Inverse(quaternion);
        Quaternion compVector = new Quaternion(vector.x, vector.y, vector.z, 0);
        Quaternion qNion = quaternion * compVector * inverseQ;
        return new Vector3(qNion.x, qNion.y, qNion.z);
    }
}