using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * 팔의 길이와 목표 지점 좌표만 아는 상태에서
 * 삼각함수를 이용하여 inverse kinematic구현
 * 현재는 총 3축 구현(팔 전체, 어깨, 팔꿈치)
 * -z방향이 팔의 윗방향이고(초기 누워있는 상태), -x방향에서 활동이 가능하다.
 * z축 회전이 구현되어있으며, 팔 길이만 된다면 -x방향의 모든 점을 잡을 수 있다.
 * 추후, 팔의 축 방향을 수정하여야 한다.(지금은 팔이 XZ면에 누워있는 상태)
 * 흰 상자가 왼쪽, 회색 상자가 오른쪽으로 해야된다.
 * 
 * 2019-05-06
 * 4축 5축 추가
 * 회전의 상속을 검토해봐야함
 * 손목 회전: 손목을 앞뒤로 꺾는 회전
 * 손바닥 회전: 손목을 시계 혹은 반시계 회전
 * 현재 상태: 손목 회전(부모), 손바닥 회전(자식)
 * 고려할 상태: 손바닥 회전(부모), 손목 회전(자식)
 * 두 상태의 기능이 달라진다.
 */
public class follow : MonoBehaviour {

    // 각 GameObject들 선언 및 유니티에서 드래그앤드롭으로 정의
    public GameObject arm;
    public GameObject upperJoint;       // 팔 전체의 회전
    public GameObject upper;            // 어깨 부분
    public GameObject middle;           // 팔꿈치 부분
    public GameObject wrist;            // 손목 x축 부분
    public GameObject lower;            // 손목 y축 부분
    public GameObject tip;              // 손끝 부분
    public GameObject target;           // 목표 지점

    public float lowerTheta = 0;        // 손바닥 회전 각도
    public float wristTheta = 180;      // 손목 회전 각도

    public int speed = 60;

    // 팔의 길이를 구하기 위한 위치정보
    private Vector3 posUpper;           // 어깨 부분 위치
    private Vector3 posMiddle;          // 팔꿈치 부분 위치
    private Vector3 posWrist;           // 손목 부분 위치
    private Vector3 posTip;             // 손끝 부분 위치
    private Vector3 posTarget;          // 목표 지점 위치

    private float rotationOffset;       // 팔 전체가 돌아가 있을 경우에 사용됨

    private float distUpperToMiddle;    // 어깨부터 팔꿈치까지 길이
    private float distMiddleToWrist;    // 팔꿈치부터 손목까지 길이
    private float distWristToTip;       // 손목부터 손끝까지 길이
    private float distUpperToTarget;    // 어깨부터 목표 지점까지 길이

    private float distUpperVertical;    // 어깨 각에 해당하는 수직선(팔꿈치에서 내린 수직선)
    private float distMiddleVertical;   // 팔꿈치 각에 해당하는 수직선(어깨에서 내린 수직선)

    private float angleUpper;           // 어깨의 각도
    private float angleMiddle;          // 팔꿈치의 각도
    private float angleArm;             // 팔 전체의 각도

    private bool isUnset;
    private bool isSet;                 // 준비가 되었을 때만 그리도록(사용되지 않음)
    private bool isFar;                 // 너무 멀지 않을 때만 그리도록

    private bool isSmallMiddle;         // 팔꿈치의 각도가 예각인지
    private bool isSmallUpper;          // 어깨의 각도가 예각인지.

    private int cnt;
    // 첫 프레임이 시작되기 전에 호출되는 함수
    void Start() {

        // 초기화
        isUnset = false;
        isSet = false;
        isFar = false;

        cnt = 0;
    }

    // 매 프레임마다 호출되는 함수
    void Update() {
        rotationOffset = arm.transform.eulerAngles.y;

        posUpper = upper.transform.position;
        posMiddle = middle.transform.position;
        posWrist = wrist.transform.position;
        posTip = tip.transform.position;
        posTarget = target.transform.position;

        // test
        posTarget.z = posUpper.z + 1;

        // 삼각형 삼변의 길이
        distUpperToMiddle = Vector3.Distance(posUpper, posMiddle);
        distMiddleToWrist = Vector3.Distance(posMiddle, posWrist);
        distWristToTip = Vector3.Distance(posWrist, posTip);
        distUpperToTarget = Vector3.Distance(posUpper, posTarget);

        // posTarget은 아래에서 수정되기때문에 실제 target의 위치정보를 따로 저장
        Vector3 posTrueTarget = target.transform.position;
        
        if (isSet == false) {
            /*
             * ----- 팔 전체 각도 계산(수정) -----
             * 수정된 부분
             * 임의적으로 팔 자체의 각도를 돌려놨을 경우에도 계산에 문제 없도록 수정하였다.
             * 팔 자체의 각도(y축)가 바뀌게 되면 x, z축의 방향이 바뀌게 되므로, 이에 대응하여 좌표축을 변경해주어야 한다.
             * 
             * 1. Target 점을 XZ평면에 정사하여 그 점과 upper의 길이를 이용한다.(upper의 y와 같게 정사한다.)
             * 2. 팔의 정면방향에 해당하는 직선과 target과의 거리를 계산한다.
             * 3. 삼각함수를 통해 팔이 움직여야하는 각도를 계산한다.
             * (sin함수이므로 0 ~ 90도를 반복한다.)
             * 4. 팔의 수평방향의 직선과 수직방향의 직선을 축으로 고정시킨다.
             * 5. 일정 각도가 돌아갈 때, 축도 같이 움직이는 것처럼 계산한다.
             */

            // 방법 1
            //float distUpperToTargetXZ = Vector3.Distance(posUpper, new Vector3(posTarget.x, posUpper.y, posTarget.z));
            //angleArm = Mathf.Asin((posTarget.z - posUpper.z) / distUpperToTargetXZ) * 180 / Mathf.PI;

            //if (posTrueTarget.x / Mathf.Abs(posTrueTarget.z) > Mathf.Tan(rotationOffset / 180 * Mathf.PI)) {
            //    angleArm = 90 - angleArm - rotationOffset;
            //} else {
            //    angleArm = angleArm - 90 - rotationOffset;
            //}
            //Debug.Log(angleArm);

            // 방법 2
            //float distUpperToTargetXZ = Vector3.Distance(posUpper, new Vector3(posTarget.x, posUpper.y, posTarget.z));
            //float gradient = -Mathf.Tan(rotationOffset / 180 * Mathf.PI);
            //float tempDist = Mathf.Abs((gradient * posTrueTarget.z) + posTrueTarget.x) / Mathf.Sqrt(Mathf.Pow(gradient, 2) + 1);
            //angleArm = Mathf.Asin(tempDist / distUpperToTargetXZ) * 180 / Mathf.PI;

            //if (Mathf.Tan(rotationOffset / 180 * Mathf.PI) * posTrueTarget.z < posTrueTarget.x) {

            //    if (rotationOffset < 90 && rotationOffset >= 0) {
            //        if (Mathf.Tan((rotationOffset + 90) / 180 * Mathf.PI) * posTrueTarget.z < posTrueTarget.x) {
            //        } else {
            //            angleArm = 90 + (90 - angleArm);
            //        }
            //    } else if (rotationOffset >= 90 && rotationOffset < 180) {
            //        if (Mathf.Tan((rotationOffset + 90) / 180 * Mathf.PI) * posTrueTarget.z < posTrueTarget.x) {
            //            angleArm *= -1;
            //        } else {
            //            angleArm = -90 - (90 - angleArm);
            //        }
            //    } else if (rotationOffset >= 180 && rotationOffset < 270) {
            //        if (Mathf.Tan((rotationOffset + 90) / 180 * Mathf.PI) * posTrueTarget.z > posTrueTarget.x) {
            //            angleArm *= -1;
            //        } else {
            //            angleArm = -90 - (90 - angleArm);
            //        }
            //    } else {
            //        if (Mathf.Tan((rotationOffset + 90) / 180 * Mathf.PI) * posTrueTarget.z > posTrueTarget.x) {
            //        } else {
            //            angleArm = 90 + (90 - angleArm);
            //        }
            //    }
            //} else {

            //    if (rotationOffset < 90 && rotationOffset >= 0) {
            //        if (Mathf.Tan((rotationOffset + 90) / 180 * Mathf.PI) * posTrueTarget.z < posTrueTarget.x) {
            //            angleArm *= -1;
            //        } else {
            //            angleArm = -90 - (90 - angleArm);
            //        }
            //    } else if (rotationOffset >= 90 && rotationOffset < 180) {
            //        if (Mathf.Tan((rotationOffset + 90) / 180 * Mathf.PI) * posTrueTarget.z < posTrueTarget.x) {
            //        } else {
            //            angleArm = 90 + (90 - angleArm);
            //        }
            //    } else if (rotationOffset >= 180 && rotationOffset < 270) {
            //        if (Mathf.Tan((rotationOffset + 90) / 180 * Mathf.PI) * posTrueTarget.z > posTrueTarget.x) {
            //        } else {
            //            angleArm = 90 + (90 - angleArm);
            //        }
            //    } else {
            //        if (Mathf.Tan((rotationOffset + 90) / 180 * Mathf.PI) * posTrueTarget.z > posTrueTarget.x) {
            //            angleArm *= -1;
            //        } else {
            //            angleArm = -90 - (90 - angleArm);
            //        }
            //    }
            //}

            // 방법 3
            float distUpperToTargetXZ = Vector3.Distance(posUpper, new Vector3(posTarget.x, posUpper.y, posTarget.z));
            float gradient = -Mathf.Tan(rotationOffset / 180 * Mathf.PI);
            float tempDist = Mathf.Abs((gradient * posTrueTarget.z) + posTrueTarget.x + (gradient * posUpper.z) - posUpper.x) / Mathf.Sqrt(Mathf.Pow(gradient, 2) + 1);
            angleArm = Mathf.Asin(tempDist / distUpperToTargetXZ) * 180 / Mathf.PI;

            if (Mathf.Tan(rotationOffset / 180 * Mathf.PI) * (posTrueTarget.z - posUpper.z) < (posTrueTarget.x - posUpper.x)) {

                if (rotationOffset < 90 && rotationOffset >= 0) {
                    if (Mathf.Tan((rotationOffset + 90) / 180 * Mathf.PI) * (posTrueTarget.z - posUpper.z) < (posTrueTarget.x - posUpper.x)) {
                    } else {
                        angleArm = 90 + (90 - angleArm);
                    }
                } else if (rotationOffset >= 90 && rotationOffset < 180) {
                    if (Mathf.Tan((rotationOffset + 90) / 180 * Mathf.PI) * (posTrueTarget.z - posUpper.z) < (posTrueTarget.x - posUpper.x)) {
                        angleArm *= -1;
                    } else {
                        angleArm = -90 - (90 - angleArm);
                    }
                } else if (rotationOffset >= 180 && rotationOffset < 270) {
                    if (Mathf.Tan((rotationOffset + 90) / 180 * Mathf.PI) * (posTrueTarget.z - posUpper.z) > (posTrueTarget.x - posUpper.x)) {
                        angleArm *= -1;
                    } else {
                        angleArm = -90 - (90 - angleArm);
                    }
                } else {
                    if (Mathf.Tan((rotationOffset + 90) / 180 * Mathf.PI) * (posTrueTarget.z - posUpper.z) > (posTrueTarget.x - posUpper.x)) {
                    } else {
                        angleArm = 90 + (90 - angleArm);
                    }
                }
            } else {

                if (rotationOffset < 90 && rotationOffset >= 0) {
                    if (Mathf.Tan((rotationOffset + 90) / 180 * Mathf.PI) * (posTrueTarget.z - posUpper.z) < (posTrueTarget.x - posUpper.x)) {
                        angleArm *= -1;
                    } else {
                        angleArm = -90 - (90 - angleArm);
                    }
                } else if (rotationOffset >= 90 && rotationOffset < 180) {
                    if (Mathf.Tan((rotationOffset + 90) / 180 * Mathf.PI) * (posTrueTarget.z - posUpper.z) < (posTrueTarget.x - posUpper.x)) {
                    } else {
                        angleArm = 90 + (90 - angleArm);
                    }
                } else if (rotationOffset >= 180 && rotationOffset < 270) {
                    if (Mathf.Tan((rotationOffset + 90) / 180 * Mathf.PI) * (posTrueTarget.z - posUpper.z) > (posTrueTarget.x - posUpper.x)) {
                    } else {
                        angleArm = 90 + (90 - angleArm);
                    }
                } else {
                    if (Mathf.Tan((rotationOffset + 90) / 180 * Mathf.PI) * (posTrueTarget.z - posUpper.z) > (posTrueTarget.x - posUpper.x)) {
                        angleArm *= -1;
                    } else {
                        angleArm = -90 - (90 - angleArm);
                    }
                }
            }
            /* 
             * ----- 팔 부분 각도 계산 ----- 
             * 1. 헤론 공식을 이용해 만들어질 삼각형의 넓이를 구한다.
             * (삼각형의 3변의 길이를 알고 있을 때, 공식을 통해 삼각형의 넓이를 구할 수 있다.)
             * 2. 만들어질 삼각형의 각 꼭지점에 해당하는 수직선의 길이를 넓이를 통해 구한다.
             * 3. Arcsin 함수를 이용해 각 꼭지점의 각도를 구한다.
             * 4. Target의 높이(z)와 target까지의 길이를 통해 삼각형의 위치할 각도를 구할 수 있다.
             * 5. SettingAngle() 함수를 이용하여 매 프레임마다 로봇 팔을 위치한다.
             * 
             * ----- 추가 -----
             * 손목 부분의 각도를 지정하였을 경우, 팔의 방향과 손목의 각도를 통해
             * Target 지점을 수정(구의 형태로 잡는다.)
             * Target 지점을 수정한 상태로 팔을 움직인다.
             */

            // 손목의 각도를 통해 공의 y점 위치를 변경
            float temp = distWristToTip * Mathf.Sin(-wristTheta / 180 * Mathf.PI);

            // 변경된 y을 토대로 팔의 방향에 맞게 x, z 점 변경
            float temp1 = distWristToTip * Mathf.Cos(-wristTheta / 180 * Mathf.PI) * Mathf.Sin((angleArm + rotationOffset) / 180 * Mathf.PI);
            float temp2 = distWristToTip * Mathf.Cos(-wristTheta / 180 * Mathf.PI) * Mathf.Cos((angleArm + rotationOffset) / 180 * Mathf.PI);

            posTarget.y -= temp;
            posTarget.x -= temp1;
            posTarget.z -= temp2;

            // Upper부터 변경된 target까지 거리 재정의
            distUpperToTarget = Vector3.Distance(posUpper, posTarget);

            // 넓이 계산
            float size = HeronFormula(distUpperToMiddle, distMiddleToWrist, distUpperToTarget);

            // upper 각에 대응하는 수직선의 길이
            // middle 각에 대응하는 수직선의 길이
            distUpperVertical = size * 2 / distUpperToTarget;
            distMiddleVertical = size * 2 / distMiddleToWrist;

            // Arcsin 함수를 통해 각도를 구한다.
            angleUpper = Mathf.Asin(distUpperVertical / distUpperToMiddle) * 180 / Mathf.PI;
            angleMiddle = Mathf.Asin(distMiddleVertical / distUpperToMiddle) * 180 / Mathf.PI;

            // upper과 target하고의 각도 계산(y축)
            // y는 절대값
            float angleRealUpper = Mathf.Asin(Mathf.Abs(posTarget.y - posUpper.y) / distUpperToTarget) * 180 / Mathf.PI;

            // wrist각도가 정해져있을 경우 예외처리(upper와 target각도가 90도를 넘어가는 경우)
            if (Vector3.Distance(new Vector3(posTrueTarget.x, 0, posTrueTarget.z), new Vector3(posUpper.x, 0, posUpper.z)) <
                Vector3.Distance(new Vector3(0, 0, 0), new Vector3(temp1, 0, temp2))) {
                angleRealUpper = 180 - angleRealUpper;
            }


            // angleUpper = Mathf.Asin(distUpperVertical / distUpperToMiddle) * 180 / Mathf.PI;
            // 위 식을 계산하게 되면, 90 degree를 반복하게 된다.
            // 180 degree는 가능하지만, 그 이상은 다시 180 degree를 반복하는 상태.
            // (절대값의 sin함수 그래프 형태)
            // Middle의 각이 예각인지 확인
            if (Mathf.Sqrt(Mathf.Pow(distUpperToMiddle, 2) + Mathf.Pow(distMiddleToWrist, 2)) > distUpperToTarget) {
                isSmallMiddle = true;
            } else {
                isSmallMiddle = false;
            }

            // Upper의 각이 예각인지 확인
            if (Mathf.Sqrt(Mathf.Pow(distUpperToMiddle, 2) + Mathf.Pow(distUpperToTarget, 2)) > distMiddleToWrist) {
                isSmallUpper = true;
            } else {
                isSmallUpper = false;
            }

            // upper가 둔각일때
            if (isSmallUpper == false) {
                angleUpper = 180f - angleUpper;
            }

            // middle이 예각일때
            if (isSmallMiddle == true) {
                angleMiddle = 180f - angleMiddle;
            }

            // Target의 높이(y)에 따라 최종 upper의 각도가 달라진다.
            // y가 양수이면, upper의 각도 = 삼각형이 위치할 각도 + 삼각형의 각도
            // y가 음수이면, upper의 각도 = 삼각형의 위치할 각도 - 삼각형의 각도
            // 그림을 그려보는 것이 이해에 빠르다.
            if (posTarget.y < posUpper.y) {
                angleUpper = angleRealUpper - angleUpper;
            } else {
                angleUpper = -(angleUpper + angleRealUpper);
            }

            angleUpper = -(90 + angleUpper);
            angleMiddle = -angleMiddle;

            //Debug.Log(angleMiddle);
            if (distUpperToMiddle + distMiddleToWrist > distUpperToTarget) {
                //SettingAngle(angleUpper, angleMiddle, angleArm, wristTheta + 90, lowerTheta);
            } else {
                Debug.Log("So Far : " + distUpperToTarget);
            }
        } else {
            if (isUnset == false) {
                cnt++;
                if (cnt <= speed) {
                    SettingAngle(angleUpper / speed * cnt,
                        angleMiddle / speed * cnt,
                        angleArm / speed * cnt,
                        (wristTheta + 90) / speed * cnt,
                        lowerTheta / speed * cnt);
                } else {
                    isSet = false;
                }
            } else {
                cnt--;
                if (cnt >= 0) {
                    SettingAngle(angleUpper / speed * cnt,
                       angleMiddle / speed * cnt,
                       angleArm / speed * cnt,
                       (wristTheta + 90) / speed * cnt,
                       lowerTheta / speed * cnt);
                } else {
                    isSet = false;
                    isUnset = false;
                }
            }
        }

        // 버튼 사용하려면 이부분을 주석처리
        SettingAngle(angleUpper, angleMiddle, angleArm, wristTheta + 90, lowerTheta);

        // 버튼 (수동)
        if (Input.GetKeyDown("c") == true) {
            if (isSet == false) {
                isSet = true;
                cnt = 0;
            }
        }
        if (Input.GetKeyDown("x") == true) {
            if (isSet == false) {
                isSet = true;
                isUnset = true;
                cnt = speed;
            }
        }

    }

    // Heson's Formula
    // 삼각형 3변의 길이를 통해 삼각형의 넓이를 구하는 공식
    float HeronFormula(float a, float b, float c) {
        float size = 0;
        float s = (a + b + c) / 2;

        size = Mathf.Sqrt(s * (s - a) * (s - b) * (s - c));

        return size;
    }

    // 예전 버전.(Global형식)
    void SettingAngle(float a, float b) {
        //upper.transform.Rotate(new Vector3(0, a, 0));
        //middle.transform.Rotate(new Vector3(0, b - a, 0));

        upper.transform.rotation = Quaternion.Slerp(Quaternion.Euler(0, 0, 0),
            Quaternion.Euler(0, a, 0), 20000f * Time.deltaTime);

        middle.transform.rotation = Quaternion.Slerp(Quaternion.Euler(0, 0, 0),
            Quaternion.Euler(0, b, 0), 20000f * Time.deltaTime);
        //isSet = true;
    }

    // 최종적으로 사용하고 있는 함수
    void SettingAngle(float a, float b, float c, float d, float e) {

        // Global이 아닌 local각을 사용하여, 자식 객체의 영향을 생각하지 않도록 함.
        // 팔꿈치의 각도에서 어깨의 각도를 고려하지 않아도 된다.
        // void SettingAngle(float a, float b) 이 함수는 global형식이다.
        if (float.IsNaN(a) || float.IsNaN(b) || float.IsNaN(c)) {
            return;
        }
        lower.transform.localEulerAngles = new Vector3(0, -e, 0);
        wrist.transform.localEulerAngles = new Vector3(0, -d - (a + b));
        upper.transform.localEulerAngles = new Vector3(0, a, 0);
        middle.transform.localEulerAngles = new Vector3(0, b, 0);
        upperJoint.transform.localEulerAngles = new Vector3(0, c, 0);
    }

    // 이 함수는 확인용으로 현재는 사용되지 않음.
    void SettingAngle(float c) {

        upper.transform.localEulerAngles = new Vector3(0, 0, -c);
        middle.transform.localEulerAngles = new Vector3(0, 0, 0);
    }
}
