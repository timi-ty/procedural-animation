using UnityEngine;
using System.Collections;

public class Utility
{

    public static int groundLayerMask
    {
        get
        {
            return LayerMask.GetMask("Ground");
        }
    }

    public static Vector3 RotateByQuaternion(Vector3 vector, Quaternion quaternion)// A helper function that I wrote to rotate a vector by a quaternion. I almost never use it!
    {
        Quaternion inverseQ = Quaternion.Inverse(quaternion);
        Quaternion compVector = new Quaternion(vector.x, vector.y, vector.z, 0);
        Quaternion qNion = quaternion * compVector * inverseQ;
        return new Vector3(qNion.x, qNion.y, qNion.z);
    }

    public static Vector3 MoveTo(Vector3 current, Vector3 target, float timeDelta, float speed)
    {
        float distance = (target - current).magnitude;
        if(distance <= speed * timeDelta)
        {
            return target;
        }
        else
        {
            return Vector3.MoveTowards(current, target, speed * timeDelta);
        }
    }
}
