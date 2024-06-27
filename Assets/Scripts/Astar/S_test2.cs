using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using static CustomMath;
using UnityEditor.PackageManager;
using System;
using System.IO;

public class S_test2 : MonoBehaviour
{
    // Enum for axle types
    public enum Axel
    {
        Front,
        Rear
    }

    // Struct to hold wheel information
    [System.Serializable]
    public struct Wheel
    {
        public GameObject wheelModel;
        public WheelCollider wheelCollider;
        public Axel axel;
    }

    // List of wheels and their settings
    public List<Wheel> wheels;

    // Car data
    public float maxMotorTorque = 3000f;
    public float maxSteeringAngle = 40f;
    public float maxAcceleration = 5.0f;

    public string filePath = "/home/hyeonuk/unity_ws/robot_pqr_data.txt";
    public string filePath2 = "/home/hyeonuk/unity_ws/robot_avw_data.txt";


    // Center of mass adjustment for more realistic behavior

    // Position where the car is checking if it should steer left/right
    public float centerSteerDifference;

    // Target waypoint for navigation
    public Transform target;
    private float acceleration = 5.0f;

    // PID parameters
    public float gain_P = 0f;
    public float gain_I = 0f;
    public float gain_D = 0f;

    // All waypoints
    private Vector3[] allWaypoints;

    // Index of the current waypoint
    private int currentWaypointIndex = 0;

    // The waypoint we are going towards and the waypoint we are going from
    private Vector3 currentWaypoint;
    private Vector3 previousWaypoint;


    // Average steering angle to simulate the time it takes to turn the steering wheel
    private float averageSteeringAngle = 0f;

    // PID controller instance
    private PIDController PIDControllerScript;
    private Rigidbody rb;
    float moveInput;
    float previousyaw = 0f;
    float mydeltaTime;
    void Start()
    {
        // Request path from current position to target position
        PathRequestManager.RequestPath(transform.position, target.position, OnPathFound);
        previousyaw = transform.rotation.eulerAngles.y;
        rb = GetComponent<Rigidbody>();
        // Initialize PID controller
        PIDControllerScript = new PIDController();
    }

    void OnPathFound(Vector3[] newPath, bool success)
    {
        if (success)
        {
            allWaypoints = newPath;
            currentWaypointIndex = 0;
            currentWaypoint = allWaypoints[currentWaypointIndex];
            previousWaypoint = GetPreviousWaypoint();
        }
        else
        {
            Debug.LogWarning("Path not found!");
        }
    }

    void OnDrawGizmos()
    {
        if (allWaypoints != null)
        {
            for (int i = currentWaypointIndex; i < allWaypoints.Length; i++)
            {
                Gizmos.color = Color.black;
                Gizmos.DrawCube(allWaypoints[i], Vector3.one);

                if (i == currentWaypointIndex)
                {
                    Gizmos.DrawLine(transform.position, allWaypoints[i]);
                }
                else
                {
                    Gizmos.DrawLine(allWaypoints[i - 1], allWaypoints[i]);
                }
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

    void Update()
    {
        my_S_Test();
        mydeltaTime = Mathf.Max(Time.deltaTime, 1f);
        //moveInput = Input.GetAxis("Vertical");
        AnimateWheels();
        // Calculate the position where the car is checking if it should steer left/right
        Vector3 steerPosition = transform.position + transform.forward * centerSteerDifference;

        // Check if we should change waypoint
        if (CustomMath.HasPassedWaypoint(steerPosition, previousWaypoint, currentWaypoint))
        {
            if (currentWaypointIndex == allWaypoints.Length)
            {
                currentWaypointIndex = 0;
            }
            previousWaypoint = GetPreviousWaypoint();
            currentWaypointIndex += 1;
            currentWaypoint = allWaypoints[currentWaypointIndex];
        }

        float roll = transform.rotation.eulerAngles.z;
        float pitch = transform.rotation.eulerAngles.x;
        float yaw = transform.rotation.eulerAngles.y;
        float accz = acceleration;
        float velz = rb.velocity.z;
        float angvel = Mathf.DeltaAngle(previousyaw, yaw) / Time.fixedDeltaTime;

        LogRobotData(roll, pitch, yaw, velz, accz, angvel);
        previousyaw = yaw;
    }

    // Get the waypoint before the current waypoint we are driving towards
    Vector3 GetPreviousWaypoint()
    {
        previousWaypoint = transform.position;

        if (currentWaypointIndex == 0)
        {
            previousWaypoint = transform.position;
        }
        else
        {
            previousWaypoint = allWaypoints[currentWaypointIndex - 1];
        }

        return previousWaypoint;
    }

    void LateUpdate()
    {
        Move();
        Steer();
    }

    void Move()
    {
        foreach (var wheel in wheels)
        {
            wheel.wheelCollider.motorTorque = 300 * acceleration*0.01f;
        }
    }

    public float GetFactorFromPIDController(float gain_P, float gain_I, float gain_D, float error)
    {
        this.gain_P = gain_P;
        this.gain_I = gain_I;
        this.gain_D = gain_D;

        float output = PIDControllerScript.CalculatePIDOutput(error);

        return output;
    }

    void Steer()
    {
        // Calculate the steering angle using PID controller
        float CTE = CustomMath.GetCrossTrackError(transform.position, previousWaypoint, currentWaypoint);
        CTE *= CustomMath.SteerDirection(transform, transform.position + transform.forward * centerSteerDifference, currentWaypoint);
        float steeringAngle = PIDControllerScript.GetFactorFromPIDController(gain_P, gain_I, gain_D, CTE);
        steeringAngle = Mathf.Clamp(steeringAngle, -maxSteeringAngle, maxSteeringAngle);
        float err = GetFactorFromPIDController(gain_P, gain_I, gain_D, CTE);
        //Debug.Log("err: " + err);

        // Average the steering angles to simulate the time it takes to turn the steering wheel
        float averageAmount = 30f;
        averageSteeringAngle = averageSteeringAngle + ((steeringAngle - averageSteeringAngle) / averageAmount);

        // Apply steering angle to the front wheels
        foreach (Wheel wheel in wheels)
        {
            if (wheel.axel == Axel.Front)
            {
                wheel.wheelCollider.steerAngle = averageSteeringAngle;
            }
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
    void my_S_Test(){
        //로봇 모델 변수
        float m = 100; // 로봇의 질량 (kg)
        float g = 9.81f; // 중력 가속도 (m/s^2)
        float roll_terrain = 0;

        float T = 0.8f;
        float L = 1.0f; // 로봇의 길이 (m)
        float H = 0.85f; // 로봇의 무게중심 (m)

        float I_xx = 14.35f;
        float I_yy = 8.1f;
        float I_zz = 10.41f;

        //각도 입력
    
        float roll = 0f*Mathf.Deg2Rad;
        float pitch = 0f*Mathf.Deg2Rad;
        float yaw = 0f*Mathf.Deg2Rad;
        float a_Gx =2f;
        float dt = 1f;

        // 각가속도 계산
        float p = roll / dt;
        float q = pitch / dt;
        float r = yaw / dt;
        float alpha_x = p / dt;
        float alpha_y = q / dt;
        float alpha_z = r / dt;
        float v = 5;
        float vmax = 5;
        float w = r ;
        float a_Gy = v * w;

        // Roll, Pitch, Yaw 회전 행렬 생성
        float[,] R_roll = new float[,]
        {
            {1, 0, 0},
            {0, (float)Math.Cos(roll), -(float)Math.Sin(roll)},
            {0, (float)Math.Sin(roll), (float)Math.Cos(roll)},
        };

        float[,] R_pitch = new float[,]
        {
            {(float)Math.Cos(pitch), 0, (float)Math.Sin(pitch)},
            {0, 1, 0},
            {-(float)Math.Sin(pitch), 0, (float)Math.Cos(pitch)},
        };

        float[,] R_yaw = new float[,]
        {
            {(float)Math.Cos(yaw), -(float)Math.Sin(yaw), 0},
            {(float)Math.Sin(yaw), (float)Math.Cos(yaw), 0},
            {0, 0, 1},
        };

        // 전체 회전 변환 행렬 (Roll, Pitch, Yaw 순으로 적용)
        float[,] R = MatrixMultiply(R_yaw, MatrixMultiply(R_pitch, R_roll));

        // x_zmp와 y_zmp 계산
        float x_zmp = (1f / (2f * m * (-g * (float)Math.Cos(pitch) * (float)Math.Cos(roll)))) * (-2f * I_yy * alpha_y - 2f * (I_xx - I_zz) * p * r + 2f * m * g * H * (float)Math.Sin(pitch) + m * g * T * (float)Math.Abs(Math.Tan(roll - roll_terrain)) * (float)Math.Sin(pitch) + 2f * m * H * a_Gx + m * T * a_Gx * (float)Math.Abs(Math.Tan(roll - roll_terrain)) +
        ((g * (float)Math.Sin(pitch) + a_Gx) * (m * T * (float)Math.Abs(Math.Tan(roll - roll_terrain)) * (-g * (float)Math.Cos(pitch) * (float)Math.Sin(roll) + a_Gy) + 2f * (I_xx * alpha_x - (I_yy - I_zz) * q * r) - m * g * H * (float)Math.Cos(pitch) * (float)Math.Sin(roll) + m * H * a_Gy)) * (float)Math.Tan(roll - roll_terrain)) /
        (g * (float)Math.Cos(pitch) * (float)Math.Cos(roll_terrain) * 1f / (float)Math.Cos(roll - roll_terrain) - a_Gy * (float)Math.Tan(roll - roll_terrain));

        float y_zmp = (m * g * (float)Math.Cos(pitch) * (float)Math.Sin(roll) * (T * (float)Math.Abs(Math.Tan(roll - roll_terrain)) + 2 * H) - m * a_Gy * (T * (float)Math.Abs(Math.Tan(roll - roll_terrain)) + 2 * H) - 2 * I_xx * alpha_x + 2 * (I_yy - I_zz) * q * r) /
        (2 * m * (g * (float)Math.Cos(pitch) * (float)Math.Cos(roll_terrain) * 1 / (float)Math.Cos(roll - roll_terrain) - a_Gy * (float)Math.Tan(roll - roll_terrain)));
        

        // 좌표 변환
        float[,] zmp = new float[,]
        {
            { x_zmp },
            { y_zmp },
            { 0 }
        };
        

        float[,] zmp_centered = zmp;
        float[,] result_zmp_centered = MatrixMultiply(R, zmp_centered);
        float[,] result_zmp = result_zmp_centered;
        float x_zmp_mod = result_zmp[0, 0];
        float y_zmp_mod = result_zmp[1, 0];
        Debug.Log($"yzmp: {y_zmp_mod}");
        Debug.Log($"xzmp: {x_zmp_mod}");

        // 모서리 좌표 설정
        float[,] corners = new float[,]
        {
        { -L / 2, -L / 2, L / 2, L / 2 },
        { -T / 2, T / 2, T / 2, -T / 2 },
        { 0, 0, 0, 0 }
        };

        // 좌표 변환
        float[,] transformed_corners = MatrixMultiply(R, corners);

        // 각 선분의 중점 계산 및 변수 할당
        float[,] Xu = new float[,]
        {
           {L/2}, {zmp[1,0]}, {1}
        };
        float[,] Xl = new float[,]
        {
           {-L/2}, {zmp[1,0]},{1}
        };
         float[,] Yu = new float[,]
        {
           {zmp[0,0]}, {T/2},{1}
        };
         float[,] Yl = new float[,]
        {
           {zmp[0,0]}, {-T/2},{1},
        };
        
        float[,] result_Xu = MatrixMultiply(R, Xu);
        float[,] result_Xl = MatrixMultiply(R, Xl);
        float[,] result_Yu = MatrixMultiply(R, Yu);
        float[,] result_Yl = MatrixMultiply(R, Yl);

        // 최대값과 최소값 계산
        float max_x = result_Xu[0,0];
        float min_x = result_Xl[0,0];
        float max_y = result_Yu[1,0];
        float min_y = result_Yl[1,0];

        // 안정성 한계 계산
        float amax = 56f;
        float wmax = 66f*Mathf.Deg2Rad;
        float vwmax = vmax * wmax;

        float Sau = (1 / 2f) * (1 - (g / (amax * H) * (x_zmp_mod - max_x)));
        float Sal = (1 / 2f) * (1 + (g / (amax * H) * (x_zmp_mod - min_x)));
        float Swu = (1 / 2f) * (1 - (g / (vwmax * H)) * (y_zmp_mod - max_y));
        float Swl = (1 / 2f) * (1 + (g / (vwmax * H)) * (y_zmp_mod - min_y));

        // 결과 출력
        Debug.Log($"Sau: {Sau}");
        Debug.Log($"Sal: {Sal}");
        Debug.Log($"Swu: {Swu}");
        Debug.Log($"Swl: {Swl}");

    }

    // 두 행렬의 곱을 계산하는 메서드
    static float[,] MatrixMultiply(float[,] matrix1, float[,] matrix2)
    {
        int rows1 = matrix1.GetLength(0);
        int cols1 = matrix1.GetLength(1);
        int rows2 = matrix2.GetLength(0);
        int cols2 = matrix2.GetLength(1);

        if (cols1 != rows2)
        {
            throw new ArgumentException("The number of columns in the first matrix must equal the number of rows in the second matrix.");
        }

        float[,] result = new float[rows1, cols2];

        for (int i = 0; i < rows1; i++)
        {
            for (int j = 0; j < cols2; j++)
            {
                float sum = 0;
                for (int k = 0; k < cols1; k++)
                {
                    sum += matrix1[i, k] * matrix2[k, j];
                }
                result[i, j] = sum;
            }
        }

        return result;
    

    }
}