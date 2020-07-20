using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleCameraFollow : MonoBehaviour
{
    public Transform follow;

    internal Vector3 offset;

    void Start()
    {
        offset = transform.position - follow.position;
    }

    void Update()
    {
        transform.position = Vector3.Lerp(transform.position, offset + follow.position, Time.deltaTime);
    }
}
