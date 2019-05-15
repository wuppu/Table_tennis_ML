using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CreateBall : MonoBehaviour {
    public GameObject newBall;
    public Vector3 posBall;
    
    // Start is called before the first frame update
    void Start() {

    }

    // Update is called once per frame
    void Update() {
        if (Input.GetKeyDown("z")) {
            Instantiate(newBall, posBall, Quaternion.identity);
        }
    }
}
