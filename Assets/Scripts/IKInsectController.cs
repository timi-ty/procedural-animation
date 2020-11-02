using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms;

/// <summary>
/// Most of the implementation stayed the same. I starred (//***//) every new or updated line and every new function.
/// The main working principle was to simply add an implementation to enable the insect turn to any desired direction.
/// The core bit of math that is responsible for making this possible is actually in here <see cref="FootIKTarget.GetRestPosition(Vector3)"/>
/// </summary>

public class IKInsectController : MonoBehaviour
{
    [Tooltip("Ensure you assign the FootIKTargets from fore limbs down to hind limbs without skipping.")]
    public List<FootIKTarget> footIKTargets = new List<FootIKTarget>(4); //the targets for the position of the feet

    public Transform skeleton; //the root of the skeleton
    public Transform rig; //the parent of the IK targets
    
    private Vector3 defaultBodyPosOffset; //the initial body pos offset, stored because the body pos offset can shange
    private Vector3 bodyPosOffset; //the bodyPos offset is the differnece between the average stable feet position and the body position at the start.
    private Quaternion bodyRotOffset; //the body rotation offset is the differnece between the average stable feet rotation and the body rotation at the start.
    private Quaternion rigRotOffset; //the rig rotation offset is the differnece between the average stable feet rotation and the rig rotation at the start.
    private bool isCentered; //***//is only true when the insect is facing the "targetMoveDirection".
    private bool isMiniStepping; //***//is only true while the insect is taking mini-steps to try and face the "targetMoveDirection".

    public AnimationCurve stepCurveX; //
    public AnimationCurve stepCurveY; //animation curves to control foot motion while taking steps
    public AnimationCurve stepCurveZ; //

    public AnimationCurve stepOffsetCurveX; //
    public AnimationCurve stepOffsetCurveY; //just extra curves to make the animation look nicer. These curves must always start and end at zero
    public AnimationCurve stepOffsetCurveZ; //in order for them to have NO net effect on the animation

    public float stepsPerSecond; //speed of stepping

    public float maxStepHeight;
    public float stepStride; //how far each step goes

    private bool isMoving; //***//is only true while the insect is trying to move in a target direction.
    private Vector3 targetMoveDirection; //***//the direction the insect should face before it starts moving forward (local).
    private Vector3 currentMoveDirection; //***//the direction the insect is currently stabilized on.
    private Coroutine moveRoutine; //***//the co-routine that controls forward movement.

    [Header("Debug Utils")]
    public LineRenderer debugLine;
    public SpriteRenderer debugSprite;
    public List<Transform> debugSphere;
    [Range(0f, 1f)]
    public float debugTime;

    private void Start()
    {
        SetStableFeetPositions(); //set the position of the feet the last time they were touching the ground.
        GetBodyOffset(); // the body offset is the differnece between the stable feet positions and the body position at the start.
        StartCoroutine(TakeMiniStepsToCentre()); //initially take ministeps to ensure that the insect is facing the "targetMoveDirection".
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

    //***//this function can recieve any direction (ignores y component) to tell the insect to try to move in that direction.
    //***//if this funtion is called too rapidly with varying directions, the insect will remain mostly still.
    public void AgentMove(Vector3 moveDirection)
    {

        if (moveDirection.sqrMagnitude > Mathf.Epsilon) //insect should only try to move if the direction is a non zero vector.
        {
            //the insect will only react if it isn't already moving in the desired move direction.
            if (!IsForwardFacing(moveDirection) || !isMoving)
            {
                targetMoveDirection = moveDirection;
                Stop(); //tells the insect to stop moving immediately.
                Move(); //tells the insect to start moving as soon as it has been able to achieve the "targetMoveDirection".
            }
        }
        else
        {
            isMoving = false; //tells the insect to stop moving as soon as possible (as soon as it is in a rest pose).
        }
    }

    public void Move()
    {
        if (!isMoving)
        {
            float stepDuration = 1 / stepsPerSecond;

            moveRoutine = StartCoroutine(MoveForward(stepDuration)); //this coroutine to take the first steps. It calls itslef recursively to maintain the motion.

            isMoving = true;
        }
    }

    public void Stop()
    {
        if(moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
        }
        isMoving = false;
    }

    private IEnumerator MoveForward(float duration)
    {
        yield return new WaitWhile(() => isMiniStepping); //wait for any old recentre attempt to safely exit.

        //Forgive me, but instead of taking the steps one at a time like a normal person, I took them all together because I felt it would look more natural :P
        //Thus, I move the different feet with different "time" inputs to make them move in a specific pattern
        //Read the MoveFoot() function to understand what the "time" input does

        float nTime = 0; //normalized time i.e time from (0 - 1), but really, mine goes from (0 - 2) because I joined the two steps together.
        float delay = 0.425f; //instead of moving two feet at the exact same time, to form the zig-zag pattern, I add small delay, because I felt it looked better!
        bool cycled = false; //monitors when a new cycle starts. A cycle means when all four legs have completed their step.
        isCentered = false; //assumes the insect is not centered.

        StartCoroutine(TakeMiniStepsToCentre()); //tells the insect to centre itself to the "targetMoveDirection".

        yield return new WaitUntil(() => isCentered); //waits until the insect has comletely centered itself to the "targetMoveDirection".

        if (isMoving)
        {
            BodyControl(); //adjusts the body to match the current feet placement.
            //Two feet are offset at the beginning to make the steps perfectly symmetrical
            //Otherwise, motion will be biased towards the feet that stepped first.
            float time = 0;
            //gradually moves feet 1 & 3 to their offset positions (required for natural looking motion).
            while(time < 1)
            {
                time += Time.fixedDeltaTime / duration;
                OffsetFoot(1, Mathf.Clamp(time, 0, 1));
                OffsetFoot(3, Mathf.Clamp(time, 0, 1));
                yield return new WaitForFixedUpdate();
            }
            //at this point, feet 1 & 3 are stable in their offset positions.
            MarkFootAsStable(1); ///marks foot 1 as stable <see cref="FootIKTarget.stablePosition"/>
            MarkFootAsStable(3); ///marks foot 3 as stable <see cref="FootIKTarget.stablePosition"/>
        }

        //I know this while loop might seem confusing, but it's really not. Basically, a single input "nTime" is manipulated
        //so that it passes different values to the different feet during movement.
        //When "nTime" is between 0 - 1, foot 0 and foot 2 are in motion but when "nTime" is between 1 - 2, foot 1 and foot 3 are in motion
        while (true)
        {
            if (isMoving) //only maintains the motion while insect is flagged to keep moving.
            {
                isCentered = false;

                MoveFoot(0, Mathf.Clamp(nTime < 2 ? cycled ? 0 : nTime : nTime - 2, 0, 1));
                MoveFoot(2, Mathf.Clamp(nTime - delay, 0, 1));
                MoveFoot(1, Mathf.Clamp(nTime - 1, 0, 1));
                MoveFoot(3, Mathf.Clamp(nTime - delay - 1, 0, 1));

                if (nTime > 1 && !cycled)
                {
                    MarkFootAsStable(0);
                    cycled = true;
                }
                if (nTime >= 2 + delay)
                {
                    nTime = delay;
                    MarkFootAsStable(1);
                    MarkFootAsStable(2);
                    MarkFootAsStable(3);
                    cycled = false;
                }

                nTime += Time.fixedDeltaTime / duration;
            }
            else if (!isCentered) //if insect is no longer moving and it isn't centered, tell the insect to recentre and wait for it to finish.
            {
                StartCoroutine(TakeMiniStepsToCentre());
                yield return new WaitUntil(() => isCentered);
            }
            yield return new WaitForFixedUpdate();
        }
    }

    private void MoveFoot(int limbIndex, float time, bool offsetFoot = false)
    {
        FootIKTarget footIKTarget = footIKTargets[limbIndex]; //select the foot we are trying to control

        Vector3 referencePosition = footIKTarget.stablePosition; //get the grounded position of that foot

        //Decide where to step using raycast
        Vector3 raycastOrigin = referencePosition + transform.forward * (offsetFoot ? stepStride / 2.0f : stepStride) + transform.up * 100;
        bool foundStep = Physics.Raycast(raycastOrigin, Vector3.down, out RaycastHit nextStep, 500, Utility.groundLayerMask);
        if (nextStep.point.y - referencePosition.y > maxStepHeight)
        {
            foundStep = false;
        }

        if (!foundStep) return;


        /****NOTE!!! The rest of this function is just to control the animation. I did a lot of work to get a nice animation which you can AVOID!****/
        /***You could just use Vector3.MoveTowards() to move the feet like in the simpler alternative I worte below to save you from a lot of trouble****/

        //*** The simpler alternative code *** P.S. I didn't design the system for this, so the animation looks a bit weird, but technically, it works!

        //if (time > 0)
        //{
        //    if (time < 0.2f)
        //    {
        //        footIKTarget.position = referencePosition + Vector3.up * 2 * time;
        //    }
        //    else
        //    {
        //        footIKTarget.position = Vector3.MoveTowards(footIKTarget.position, nextStep.point, Time.deltaTime * 5);
        //        if (time == 1) footIKTarget.position = nextStep.point;
        //    }
        //}
        //BodyControl();


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

    private void OffsetFoot(int limbIndex, float time)
    {
        //Offsets foot by basically taking half a step
        MoveFoot(limbIndex, time, true); //***//updated to offset gradually with normalized time input instead of all at once.
    }

    //***//this coroutine is the centre of the new implementation.
    //***//it controls the way the insect recentres itself.
    private IEnumerator TakeMiniStepsToCentre()
    {
        isMiniStepping = true; //tell the whole controller that a recentre attempt is in progress.
        SetStableFeetPositions(); //mark the final stable postions needed for the feet after this recentre is complete.
        float delay = 0.075f; //small delay between the movement of a pair of feet while taking ministeps.
        float speed = 2 * stepsPerSecond; //speed of the ministeps.
        float stepLift = 0.5f; //foot elevation for the ministeps.
        float recentreProgress; //tracks the progress of each recentre stage.


        //Step 1: Divides the entire turn needed to face the "targetMoveDirection" into several smaller turns each with a stabilized resting pose.
        float miniStepAngle = 30; //the angle to turn for each sub-division.

        List<Vector3[]> wayPoints = new List<Vector3[]>(); //the stable positions of all feet for the resting pose of each turn sub-division.

        float recentreAngle = Vector3.SignedAngle(transform.forward, targetMoveDirection, Vector3.up); //the entire turn angle needed to face the "targetMoveDirection".

        int wayPointCount = (int) Mathf.Abs(recentreAngle / miniStepAngle); //the number of turn sub-divisions which is also the number of resting poses.

        Vector3[] interDirections = new Vector3[wayPointCount]; //the array of vectors facing the turn sub-divisions. 

        for (int i = 1; i <= wayPointCount; i++)
        {
            Vector3[] wayPoint = new Vector3[footIKTargets.Count]; //initializes the "wayPoint" array with the number of feet.
            //the "interAngle" is the turn sub-division angle measured from the target move directiion. 
            float interAngle = -(recentreAngle - i * (recentreAngle < 0 ? -miniStepAngle : miniStepAngle));
            //the "interdirection" is the "targetMoveDirection" rotated by "interAngle".
            Vector3 interDirection = Utility.RotateByQuaternion(targetMoveDirection.normalized, Quaternion.Euler(0, interAngle, 0));
            for (int j = 0; j < footIKTargets.Count; j++)
            {
                wayPoint[j] = footIKTargets[j].GetRestPosition(interDirection);
            }
            interDirections[i - 1] = interDirection;
            wayPoints.Add(wayPoint);
        }

        //Step 2: Gradually move each foot to the next "wayPoint" position for that foot.
        bool feetStablized;
        for (int j = 0; j < wayPoints.Count; j++) //for every "wayPoint" in ascending order.
        {
            Vector3[] wayPoint = wayPoints[j];

            for(int k = 0; k < 2; k++) //when k == 0, we are moving all even indexed feet. When k == 1, we are moving all odd indexed feet.
            {
                feetStablized = false; //initialize all feet as not beig stabilized.
                recentreProgress = 0; //set recentre progress to 0 for the current "wayPoint" stage.
                while (!feetStablized)
                {
                    feetStablized = true; //assume all feet are stabilized.
                    for (int i = 0; i < footIKTargets.Count; i++) //for every foot.
                    {
                        if (k >= i % 2) //when k == 0, we are moving only even indexed feet. When k == 1, we are moving only odd indexed feet.
                        {
                            if (recentreProgress >= (i / 2) * delay) //move feet with higher indeces only when their delay has elapsed.
                            {
                                float planarDisToWayPoint = new Vector2((footIKTargets[i].position - wayPoint[i]).x, 
                                    (footIKTargets[i].position - wayPoint[i]).z).magnitude;
                                //add upward motion to feet if they are far enough from the "wayPoint".
                                if (planarDisToWayPoint > 0.2f && footIKTargets[i].position.y < (wayPoint[i] + Vector3.up * stepLift).y)
                                {
                                    footIKTargets[i].position = Utility.MoveTo(footIKTargets[i].position, wayPoint[i] + Vector3.up * stepLift,
                                        Time.fixedDeltaTime, speed);
                                }
                                else //move feet directly to "wayPoint".
                                {
                                    footIKTargets[i].position = Utility.MoveTo(footIKTargets[i].position, wayPoint[i], Time.fixedDeltaTime, speed);
                                }
                            }
                            feetStablized &= footIKTargets[i].position == wayPoint[i]; //feet are only stabilized when every updated foot has reached its "wayPoint".
                        }
                    }
                    BodyControl(); //body is adjusted to fit the current feet positions.
                    recentreProgress += Time.fixedDeltaTime;
                    yield return new WaitForFixedUpdate();
                }
            }
            currentMoveDirection = interDirections[j]; //current move direction is updated only if the insect has successfully stabilized on the "interDirection".
            if (!isMoving) //safely exit at the "currentMoveDirection" if the insect is no longer trying to move.
            {
                isMiniStepping = false;
                yield break;
            }
        }


        //Step 3: Gradually move each foot to the final needed stable position for that foot.
        for (int k = 0; k < 2; k++)
        {
            feetStablized = false; //initialize all feet as not beig stabilized.
            recentreProgress = 0; //set recentre progress to 0 for the final stage.
            while (!feetStablized)
            {
                feetStablized = true; //assume all feet are stabilized.
                for (int i = 0; i < footIKTargets.Count; i++) //for every foot.
                {
                    if (k >= i % 2) //when k == 0, we are moving only even indexed feet. When k == 1, we are moving only odd indexed feet.
                    {
                        if (recentreProgress >= (i / 2) * delay) //move feet with higher indeces only when their delay has elapsed.
                        {
                            float planarDisToWayPoint = new Vector2((footIKTargets[i].position - footIKTargets[i].stablePosition).x,
                            (footIKTargets[i].position - footIKTargets[i].stablePosition).z).magnitude;
                            //add upward motion to feet if they are far enough from the "wayPoint".
                            if (planarDisToWayPoint > 0.2f && footIKTargets[i].position.y < (footIKTargets[i].stablePosition + Vector3.up * stepLift).y)
                            {
                                footIKTargets[i].position = Utility.MoveTo(footIKTargets[i].position, footIKTargets[i].stablePosition + Vector3.up * stepLift,
                                        Time.fixedDeltaTime, speed);
                            }
                            else //move feet directly to "wayPoint".
                            {
                                footIKTargets[i].position = Utility.MoveTo(footIKTargets[i].position, footIKTargets[i].stablePosition, Time.fixedDeltaTime, speed);
                            }
                        }
                        feetStablized &= footIKTargets[i].position == footIKTargets[i].stablePosition;
                    }
                }
                BodyControl(); //body is adjusted to fit the current feet positions.
                recentreProgress += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
        }
        
        //ensure all feet are firmly on their corresponding stable positions
        for (int i = 0; i < footIKTargets.Count; i++)
        {
            footIKTargets[i].position = footIKTargets[i].stablePosition;
        }

        BodyControl(); //body is adjusted to fit the current feet positions.
        currentMoveDirection = targetMoveDirection;
        isMiniStepping = false;
        isCentered = true;
    }

    //***//sets the stable position of all feet as their rest position.
    private void SetStableFeetPositions()
    {
        for (int i = 0; i < footIKTargets.Count; i++)
        {
            footIKTargets[i].stablePosition = footIKTargets[i].GetRestPosition(targetMoveDirection); //***//now uses rest position instead of current position.
        }
    }

    //***//called to update a single foot's stable position when you're not sure that the other feet are on the ground.
    private void MarkFootAsStable(int index)
    {
        footIKTargets[index].stablePosition = footIKTargets[index].position; ///marks foot as stable <see cref="FootIKTarget.stablePosition"/>
    }

    private void GetBodyOffset() // called at the beginning to get both the positional and the rotational offset between the body and the feet.
    {
        Vector3 cummulativeStablePos = Vector3.zero;

        for (int i = 0; i < footIKTargets.Count; i++)
        {
            cummulativeStablePos += footIKTargets[i].stablePosition;
        }

        Vector3 averageStableFeetPos = cummulativeStablePos / (footIKTargets.Count);

        defaultBodyPosOffset = transform.position - averageStableFeetPos;

        bodyPosOffset = defaultBodyPosOffset;

        bodyRotOffset = skeleton.rotation;

        rigRotOffset = rig.rotation;
    }

    
    private void BodyControl() // Simply updates the position and rotation of the body based on the new feet positions
    {
        //Store all foot target positions before moving their parent, which is this transform
        Vector3[] tempTargetPositions = new Vector3[footIKTargets.Count];

        //Calculate the average feet position
        Vector3 cummulativeFeetPos = Vector3.zero;

        // Calculating the averages
        for (int i = 0; i < footIKTargets.Count; i++)
        {
            tempTargetPositions[i] = footIKTargets[i].position;

            cummulativeFeetPos += footIKTargets[i].position;
        }

        Vector3 averageStableFeetPos = cummulativeFeetPos / (footIKTargets.Count);

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

        Vector2 cummulativeFrontFeetPlanarPos = Vector3.zero;
        Vector2 cummulativeHindFeetPlanarPos = Vector3.zero;

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

            if (i < footIKTargets.Count / 2)
            {
                cummulativeFrontFeetPlanarPos += new Vector2(footIKTargets[i].position.x, footIKTargets[i].position.z);
            }
            else if (i >= footIKTargets.Count / 2)
            {
                cummulativeHindFeetPlanarPos += new Vector2(footIKTargets[i].position.x, footIKTargets[i].position.z);
            }
        }

        Vector3 averageLeftFeetPos = cummulativeLeftFeetPos / (footIKTargets.Count/2);
        Vector3 averageRightFeetPos = cummulativeRightFeetPos / (footIKTargets.Count/2);

        Vector3 averageFrontFeetPos = cummulativeFrontFeetPos / (footIKTargets.Count / 2);
        Vector3 averageHindFeetPos = cummulativeHindFeetPos / (footIKTargets.Count / 2);

        Vector3 averageFrontFeetPlanarPos = cummulativeFrontFeetPlanarPos / (footIKTargets.Count / 2);
        Vector3 averageHindFeetPlanarPos = cummulativeHindFeetPlanarPos / (footIKTargets.Count / 2);

        //The roll vector is like the z rotation which is gotten by knowing the height difference between the average left feet positions and the average right feet positions
        Vector3 rollVector = (averageLeftFeetPos - averageRightFeetPos);
        //The pitch vector is like the x rotation which is gotten by knowing the height difference between the average front feet positions and the average hind feet positions
        Vector3 pitchVector = (averageFrontFeetPos - averageHindFeetPos);
        //***//The yaw vector is like the y rotation which is gotten by knowing planar difference between the average front feet positions and the average hind feet positions
        Vector2 yawVector = (averageFrontFeetPlanarPos - averageHindFeetPlanarPos);

        float rollAngle = Vector3.SignedAngle(transform.right, rollVector, transform.forward);
        float pitchAngle = Vector3.SignedAngle(transform.forward, pitchVector, transform.right);
        float yawAngle = Vector2.SignedAngle(yawVector, Vector2.up); //***//the angle the insect should be facing

        //Uncomment to three lines below see how the average front and hind feet positions affect the elevation
        //debugLine.SetPosition(0, averageFrontFeetPos);
        //debugLine.SetPosition(1, averageHindFeetPos);
        //Debug.Log("elevation: " + elevationAngle);

        Quaternion roll = Quaternion.AngleAxis(rollAngle, transform.forward);
        Quaternion pitch = Quaternion.AngleAxis(pitchAngle, transform.right);

        transform.rotation = Quaternion.Euler(transform.eulerAngles.x, yawAngle, transform.eulerAngles.z); //***//sets the transform rotation to allow the insect to face different directions.
        skeleton.rotation = Quaternion.Lerp(skeleton.rotation, roll * pitch * transform.rotation * bodyRotOffset, Time.deltaTime); //***//added transform.rotation to destiniation rotation.
        rig.rotation = Quaternion.Lerp(rig.rotation, roll * pitch * transform.rotation * rigRotOffset, Time.deltaTime); //***//added transform.rotation to destiniation rotation.

        //2 lines below is just some fine tuning, it can be ignored.
        Vector3 afterRotation = rig.up;
        bodyPosOffset = Utility.RotateByQuaternion(defaultBodyPosOffset, Quaternion.FromToRotation(Vector3.up, afterRotation));

        //Restore foot target positions to cancel out parent's movement
        for (int i = 0; i < footIKTargets.Count; i++)
        {
            footIKTargets[i].position = tempTargetPositions[i];
        }
    }

    //***//checks if the "inputVector" is in the same general direction as the "currentMoveDirection"
    private bool IsForwardFacing(Vector3 inputVector)
    {
        return (inputVector.normalized - currentMoveDirection.normalized).sqrMagnitude < 0.1f;
    }
}