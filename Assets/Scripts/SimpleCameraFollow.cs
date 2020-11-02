using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleCameraFollow : MonoBehaviour
{
    public Transform follow;

    internal Vector3 offset;

    Vector3 followVector;

    int frameCount;

    void Start()
    {
        offset = transform.position - follow.position;

        followVector = follow.position;
    }

    void FixedUpdate()
    {
        transform.position = Vector3.Lerp(transform.position, offset + followVector, Time.fixedDeltaTime);

        if(frameCount >= 5)
        {
            frameCount = 0;
            followVector = follow.position;
        }

        frameCount++;
    }
}
