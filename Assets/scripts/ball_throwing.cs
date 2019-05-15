using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ball_throwing : MonoBehaviour {

    public GameObject ball;
    public bool doCheck;

    private float height;
    private float prevHeight;
    private bool isUpper = false;
    // Use this for initialization
    void Start() {
        height = ball.transform.position.y;
        prevHeight = height;
        Debug.Log("Start height : " + (height - 4.3));
    }

    // Update is called once per frame
    void Update() {
        if (doCheck == true) {
            height = ball.transform.position.y;
            if (height - prevHeight > 0 && isUpper == false) {
                Debug.Log("The lowest height : " + (height - 4.3));
                isUpper = true;
            } else if (height - prevHeight < 0 && isUpper == true) {
                Debug.Log("The highest height : " + (height - 4.3));
                isUpper = false;
            }
            prevHeight = height;
        }        
    }
}
