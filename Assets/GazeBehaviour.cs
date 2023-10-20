using HP.Omnicept.Messaging.Messages;
using HP.Omnicept.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class GazeBehaviour : MonoBehaviour
{
    public float raycastMaxDistance;
    public Camera cam;
    private GliaBehaviour _gliaBehaviour = null;
    private GliaBehaviour gliaBehaviour
    {
        get
        {
            if (_gliaBehaviour == null)
            {
                _gliaBehaviour = FindObjectOfType<GliaBehaviour>();
            }
            return _gliaBehaviour;
        }
    }
    Vector3 gaze;
    // Start is called before the first frame update
    void Start()
    {
        gliaBehaviour.OnEyeTracking.AddListener(CalculateGaze);
    }

    // Update is called once per frame.
    void Update()
    {
        Debug.Log(gaze);
        RaycastHit hit;
        var isHit = Physics.Raycast(new Ray(cam.transform.position, cam.transform.rotation * gaze), out hit, raycastMaxDistance, LayerMask.GetMask("EyeTracking"));
        //Gizmos.DrawLine(cam.transform.position, cam.transform.rotation * gaze);
        if (isHit) {
            print("HIT SUCCESSFUL");
            var color = hit.collider.GetComponent<MeshRenderer>().material.color;
            color = Vector4.MoveTowards(color, UnityEngine.Color.red, Time.deltaTime);
            hit.collider.GetComponent<MeshRenderer>().material.color = color;
        }
    }
    public void CalculateGaze(EyeTracking eyeTracking)
    {
        gaze = new Vector3(
            -eyeTracking.CombinedGaze.X,
            eyeTracking.CombinedGaze.Y,
            eyeTracking.CombinedGaze.Z);
    }
}
