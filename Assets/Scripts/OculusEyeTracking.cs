using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.XR.Oculus;
public class OculusEyeTracking : MonoBehaviour
{
    public Camera cam;
    public GameObject leftEye;
    public GameObject rightEye;
    public float raycastMaxDistance = 500;
    // Start is called before the first frame update
    void Start()
    {
        //OculusEyeTracking.
    }

    // Update is called once per frame
    void Update()
    {
        var gaze = CalculateGaze();
        RaycastHit hit;
        var isHit = Physics.Raycast(new Ray(cam.transform.position, cam.transform.rotation * gaze), out hit, raycastMaxDistance, LayerMask.GetMask("EyeTracking"));
        //Gizmos.DrawLine(cam.transform.position, cam.transform.rotation * gaze);
        if (isHit)
        {
            print("HIT SUCCESSFUL");
            var color = hit.collider.GetComponent<MeshRenderer>().material.color;
            color = Vector4.MoveTowards(color, Color.red, Time.deltaTime);
            hit.collider.GetComponent<MeshRenderer>().material.color = color;
        }
    }
    // Calculating the Gaze by getting mean of left and right eye rotation
    Vector3 CalculateGaze()
    {
        return ((rightEye.transform.rotation * Vector3.forward) + (leftEye.transform.rotation * Vector3.forward)) / 2;
    }
}
