using HP.Omnicept.Messaging.Messages;
using HP.Omnicept.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GazeBehaviour : MonoBehaviour
{
    public Canvas canvas;
    public GameObject gazePointer;
    public TMPro.TextMeshProUGUI[] textMeshPro;
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
        foreach (var i in textMeshPro)
        {
            i.text = $"{Mathf.Round(gaze.x * 100) / 100}, {Mathf.Round(gaze.y * 100) / 100}, {Mathf.Round(gaze.z * 100) / 100}";
            Vector2 position;
            position.x = gaze.x * canvas.GetComponent<RectTransform>().rect.width / 2;
            position.y = gaze.y * canvas.GetComponent<RectTransform>().rect.height / 2;
            i.GetComponent<RectTransform>().anchoredPosition3D = position;
        }
    }
    public void CalculateGaze(EyeTracking eyeTracking)
    {
        gaze = new Vector3(
            -eyeTracking.CombinedGaze.X,
            eyeTracking.CombinedGaze.Y,
            eyeTracking.CombinedGaze.Z);
        //Gizmos.DrawSphere(gaze, 5f);
        gazePointer.transform.position = new Vector3(0, 1, 0) + (gaze);
    }
}
