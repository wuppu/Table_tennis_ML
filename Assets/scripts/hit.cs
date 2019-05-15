using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class hit : MonoBehaviour {
    public GameObject racket;
    public float waitingTime;
    public float power;
    public bool autoHit = false;

    private Rigidbody racketRig;
    private Vector3 startPos;
    private float timer = 0;
    private bool startTimer = false;

    // Start is called before the first frame update
    void Start() {
        racketRig = racket.GetComponent<Rigidbody>();
        startPos = racket.transform.position;
    }

    // Update is called once per frame
    void Update() {
        if (Input.GetKeyDown("x")) {
            startTimer = true;
            racketRig.AddForce(new Vector3(0, power, power), ForceMode.Impulse);
            
        } 
        if (startTimer == true) {
            timer += Time.deltaTime;
            if (timer > waitingTime) {
                racketRig.isKinematic = true;
                //racketRig.AddForce(new Vector3(0, 0, 0), ForceMode.VelocityChange);
                racket.transform.position = startPos;
                startTimer = false;
                timer = 0;
                racketRig.isKinematic = false;
            }
        }
    }
    void hitRacket() {
        startTimer = true;
        racketRig.AddForce(new Vector3(0, power, power), ForceMode.Impulse);
    }
    void OnCollisionEnter(Collision col) {
        Debug.Log("Enter");
    }
    void OnTriggerEnter(Collider col) {
        if (autoHit == true)
            hitRacket();
    }
}
