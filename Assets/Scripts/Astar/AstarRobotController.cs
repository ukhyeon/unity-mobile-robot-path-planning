using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

public class AstarRobotController : MonoBehaviour
{
    public int targetFrameRate = 60;
    public enum Axel
    {
        Front,
        Rear
    }

    [Serializable]
    public struct Wheel
    {
        public GameObject wheelModel;
        public WheelCollider wheelCollider;
        public Axel axel;
    }

    public Transform target;
    private Vector3[] path;
    private int targetIndex;
    public float maxAcceleration = 10.0f;
    public float brakeAcceleration = 50.0f;
    public float turnSensitivity = 1.0f;
    public float maxSteerAngle = 30.0f;
    private float steerAngle = 0.0f;
    private float acceleration = 0.0f;
    private float currentSteerAngle = 0.0f;
    private float currentAcceleration = 0.0f;
    public float steerSpeed = 0.05f; // 조향 속도
    public float accelerationSpeed = 0.05f; // 가속 속도
    public float maxVelocity = 10.0f; // 최대 속도
    public List<Wheel> wheels;
    private Rigidbody rb;

    private float steerInput;
    private Rigidbody carRb;
    public string filePath = "/home/hyeonuk/unity_ws/robot_pqr_data.txt";
    public string filePath2 = "/home/hyeonuk/unity_ws/robot_avw_data.txt";
    float previousvel;

    void Start()
    {
        Application.targetFrameRate = targetFrameRate;
        previousvel=0f;
        carRb = GetComponent<Rigidbody>();
        PathRequestManager.RequestPath(transform.position, target.position, OnPathFound);
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        AnimateWheels();
        GetPathInputs();
        float roll = transform.rotation.eulerAngles.z;
        float pitch = transform.rotation.eulerAngles.x;
        float yaw = transform.rotation.eulerAngles.y;
        float velz=transform.InverseTransformDirection(rb.velocity).z;
        float accz=0f;
        float angvel = transform.InverseTransformDirection(rb.angularVelocity).y*Mathf.Rad2Deg;

        float previousvel=velz;

        LogRobotData(roll, pitch, yaw, velz, accz, angvel);
    }

    void LateUpdate()
    {
        Move();
        Steer();
        Brake();
    }

    void OnPathFound(Vector3[] newPath, bool success)
    {
        if (success)
        {
            path = newPath;
            targetIndex = 0;
        }
        else
        {
            Debug.LogWarning("Path not found!");
        }
    }

    void OnDrawGizmos()
    {
        if (path != null)
        {
            for (int i = targetIndex; i < path.Length; i++)
            {
                Gizmos.color = Color.black;
                Gizmos.DrawCube(path[i], Vector3.one);

                if (i == targetIndex)
                {
                    Gizmos.DrawLine(transform.position, path[i]);
                }
                else
                {
                    Gizmos.DrawLine(path[i - 1], path[i]);
                }
            }
        }
    }

    void GetPathInputs()
    {
        if (path != null && path.Length > 0 && targetIndex < path.Length)
        {
            Vector3 targetPosition = path[targetIndex];
            Vector3 direction = targetPosition - transform.position;
            float angle = Vector3.Angle(transform.forward, direction);
            steerInput = Mathf.Sign(Vector3.Cross(transform.forward, direction).y);

            float targetSteerAngle = currentSteerAngle;
            float targetAcceleration = currentAcceleration;

            // 현재 속도 확인
            float currentVelocity = rb.velocity.magnitude;

            // 각도에 따른 조향각 및 가속도 계산
            if (angle > 30.0f)
            {
                targetSteerAngle += maxSteerAngle * steerSpeed * Time.deltaTime;
                targetAcceleration += Mathf.Lerp(0f, maxAcceleration, 0.3f) * accelerationSpeed * Time.deltaTime;
                
            }
            else if (angle <= 30.0f && angle > 25.0f)
            {
                targetSteerAngle += Mathf.Lerp(0f, maxSteerAngle, (angle - 25.0f) / 5.0f) * steerSpeed * Time.deltaTime;
                targetAcceleration += Mathf.Lerp(0f, maxAcceleration, 0.5f) * accelerationSpeed * Time.deltaTime;
                
            }
            else if (angle <= 25.0f && angle > 20.0f)
            {
                targetSteerAngle += Mathf.Lerp(0f, maxSteerAngle, (angle - 20.0f) / 5.0f) * steerSpeed * Time.deltaTime;
                targetAcceleration += Mathf.Lerp(0f, maxAcceleration, 0.6f) * accelerationSpeed * Time.deltaTime;
                
            }
            else if (angle <= 20.0f && angle > 15.0f)
            {
                targetSteerAngle += Mathf.Lerp(0f, maxSteerAngle, (angle - 15.0f) / 5.0f) * steerSpeed * Time.deltaTime;
                targetAcceleration += Mathf.Lerp(0f, maxAcceleration, 0.7f) * accelerationSpeed * Time.deltaTime;
            }
            else if (angle <= 15.0f && angle > 10.0f)
            {
                targetSteerAngle += Mathf.Lerp(0f, maxSteerAngle, (angle - 10.0f) / 5.0f) * steerSpeed * Time.deltaTime;
                targetAcceleration += Mathf.Lerp(0f, maxAcceleration, 0.8f) * accelerationSpeed * Time.deltaTime;
            }
            else if (angle <= 10.0f && angle > 5.0f)
            {
                targetSteerAngle += Mathf.Lerp(0f, maxSteerAngle, (angle - 5.0f) / 5.0f) * steerSpeed * Time.deltaTime;
                targetAcceleration += Mathf.Lerp(0f, maxAcceleration, 0.9f) * accelerationSpeed * Time.deltaTime;
            }
            else
            {
                targetSteerAngle -= maxSteerAngle * steerSpeed * Time.deltaTime;
                targetAcceleration += maxAcceleration * accelerationSpeed * Time.deltaTime;
            }

            // 값을 범위 내에서 제한
            currentSteerAngle = Mathf.Clamp(targetSteerAngle, -maxSteerAngle, maxSteerAngle);
            currentAcceleration = Mathf.Clamp(targetAcceleration, 0f, maxAcceleration);

            steerAngle = currentSteerAngle;
            acceleration = currentAcceleration;

            // 목표 지점에 도달하면 다음 지점으로 이동
            if (direction.magnitude < 0.8f)
            {
                targetIndex++;
            }
        }
    }

    void Move()
    {
        foreach (var wheel in wheels)
        {
            wheel.wheelCollider.motorTorque = 300 * acceleration * Time.deltaTime;
        }
    }

    void Steer()
    {
        foreach (var wheel in wheels)
        {
            if (wheel.axel == Axel.Front)
            {
                var _steerAngle = steerInput * turnSensitivity * steerAngle;
                wheel.wheelCollider.steerAngle = Mathf.Lerp(wheel.wheelCollider.steerAngle, _steerAngle, 0.3f); // 더 작은 보간 값
            }
        }
    }

    void Brake()
    {
        if (path == null || path.Length == 0 || targetIndex >= path.Length)
        {
            foreach (var wheel in wheels)
            {
                wheel.wheelCollider.brakeTorque = brakeAcceleration * Time.deltaTime;
            }
        }
        else
        {
            foreach (var wheel in wheels)
            {
                wheel.wheelCollider.brakeTorque = 0;
            }
        }
    }

    void AnimateWheels()
    {
        foreach (var wheel in wheels)
        {
            Quaternion rot;
            Vector3 pos;
            wheel.wheelCollider.GetWorldPose(out pos, out rot);
            wheel.wheelModel.transform.position = pos;
            wheel.wheelModel.transform.rotation = rot;
        }
    }

    void LogRobotData(float roll, float pitch, float yaw, float vel, float acc, float angvel)
    {
        using (StreamWriter writer = new StreamWriter(filePath, true))
        {
            writer.WriteLine("Roll: " + roll + ", Pitch: " + pitch + ", Yaw: " + yaw);
        }
        using (StreamWriter writer = new StreamWriter(filePath2, true))
        {
            writer.WriteLine("Velocity: " + vel + ", Acceleration: " + acc + ", Angular Velocity: " + angvel);
        }
    }
}
