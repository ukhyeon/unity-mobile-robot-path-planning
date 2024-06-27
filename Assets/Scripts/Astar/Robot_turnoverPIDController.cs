using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using static CustomMath;
using UnityEditor.PackageManager;
using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine.AI;
using System.ComponentModel.Design.Serialization;
using TMPro;


public class Robot_turnoverPIDController : MonoBehaviour
{
    public int targetFrameRate = 60;
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
    //public float maxMotorTorque = 3000f;
    public float maxSteeringAngle = 40f;
    public float MotorTorque = 1500f;

    public string filePath = "/home/hyeonuk/unity_ws/robot_pqr_data.txt";
    public string filePath2 = "/home/hyeonuk/unity_ws/robot_avw_data.txt";
    public string filePath3="/home/hyeonuk/unity_ws/robot_S_data.txt";


    // Center of mass adjustment for more realistic behavior

    // Position where the car is checking if it should steer left/right
    public float centerSteerDifference;

    // Target waypoint for navigation
    public Transform target;
    

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
    private Vector3 prerpreviousWaypoint;
    float roll;
    float pitch; 
    float yaw ;
    float accz ;
    float velz ;
    float angvel ;

    float previousacc;
    float prepreviousacc;
    float prepreviousvel;
     float previousvel;
    float avevel;
    float aveacc;
    float aveangvel;

    float preangvel;
    float prepreangvel;
   
    float minMotorTorque;
    float minaverageAmount;
    float prepreviousroll;
    float prepreviouspitch;
    float prepreviousyaw;

    float prepre_p;
    float pre_p;
    //float ave_p;
    float prepre_q;
    float pre_q;
    //float ave_q;
    float prepre_r;
    float pre_r;
    //float ave_r;

    // Average steering angle to simulate the time it takes to turn the steering wheel
    private float averageSteeringAngle = 0f;

    // PID controller instance
    private PIDController PIDControllerScript;
    private Rigidbody rb;
    float previousyaw ;
    private float timer=0f;


    float preroll;
    float prepitch;

    float Sau;
    float Sal; 
    float Swu; 
    float Swl;
    float pre_Swu=0;
    float pre_Swl=0;
    float averageAmount;
    bool angvel_danger;
    bool angvel_ctrdanger;
    bool angvel_maxdanger;
    bool accz_danger;
    bool accz_ctrdanger;
    bool accz_maxdanger;
    float previous_p;
    float previous_q;
    float previous_r;
   // float averoll;
    //float avepitch;
   // float aveyaw;
    //float preaveroll;
   // float preavepitch;
//    float preaveyaw;
    float alpha_x;
    float alpha_y ;
    float alpha_z;
    float angleDifference_p ;
    float angleDifference_q ;
    float angleDifference_r ;
    float p;
    float q;
    float r;
   // float preave_p;
   // float preave_q;
    //float preave_r;
   
    float Threshold_deg=1f;
    float Threshold_rad=1f*Mathf.Deg2Rad;
    float count;
    float x_zmp_mod ;
    float y_zmp_mod ;

     private float accumulatedDeltaTime = 0.0f;
    private float accumulatedP = 0.0f;
    private float accumulatedQ = 0.0f;
    private float accumulatedR = 0.0f;
    
    private float accumulated_anglediff_p=0.0f;
    private float accumulated_anglediff_q=0.0f;
    private float accumulated_anglediff_r=0.0f;
    public float Threshold_Time=0.2f;

    private float accumulated_aveangvel=0f;

    float preave_angevel=0f;
    float pre_alpha_x=0f;
    float pre_alpha_y=0f;
    float pre_alpha_z=0f;
    float a_Gy=0f;
    private Vector3 previousPosition;
    private float totalDistance=0f;
    private float distance=0f;
    private float S_count;
    float acc_pre_p=0f;
    float acc_pre_q=0f;
    float acc_pre_r=0f;
  
    void Start()
    {   
        S_count=0f;
        count=0;
        Application.targetFrameRate = targetFrameRate;
        totalDistance=0f;
         
        // Request path from current position to target position
        PathRequestManager.RequestPath(transform.position, target.position, OnPathFound);

        rb = GetComponent<Rigidbody>();

        // Initialize PID controller
        PIDControllerScript = new PIDController();

        previousvel=transform.InverseTransformDirection(rb.velocity).z;
        prepreviousvel=0f;
        
        previousacc=(previousvel-prepreviousvel)/Time.deltaTime;
        prepreviousacc=0f;

        previousPosition=transform.position;
        totalDistance=0f;

    
        preangvel=transform.InverseTransformDirection(rb.angularVelocity).y*Mathf.Rad2Deg;
        prepreangvel=0f;

        preroll=transform.rotation.eulerAngles.z;
        prepitch=transform.rotation.eulerAngles.x;
        previousyaw = transform.rotation.eulerAngles.y;

        prepreviousroll=0f;
        prepreviouspitch=0f;
        prepreviousyaw=0f;
        
        previous_p=0f;
        previous_q=0f;
        previous_r=0f;

       // preaveroll=0;
       // preavepitch=0;
       // preaveyaw=0;

        prepre_p=0f;
        prepre_q=0f;
        prepre_r=0f;

        pre_p=0f;
        pre_q=0f;
        pre_r=0f;

       // preave_p=0f;
       // preave_q=0f;
        //preave_r=0f;

        averageAmount=30f;
        minMotorTorque=0.8f*MotorTorque;
        angvel_danger=false;
        accz_danger=false;
        angvel_ctrdanger=false;
        accz_ctrdanger=false;
        angvel_maxdanger=false;
        accz_maxdanger=false;
        //rb.centerOfMass = new Vector3(0, 0.75f, 0);

    }

    void OnPathFound(Vector3[] newPath, bool success)
    {
        if (success)
        {
            allWaypoints = newPath;
            currentWaypointIndex = 0;
            currentWaypoint = allWaypoints[currentWaypointIndex];
            previousWaypoint = GetPreviousWaypoint();
            prerpreviousWaypoint=previousWaypoint;
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
        Debug.Log("totla_time: " + timer);

        Vector3 currentPosition = transform.position;

        distance= Vector3.Distance(previousPosition,currentPosition);
        totalDistance +=distance;
        previousPosition=currentPosition;


        timer += Time.deltaTime;
        accumulatedDeltaTime +=Time.deltaTime;
        
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

            if (allWaypoints != null)
            {
                Vector3 direction1 = (previousWaypoint-prerpreviousWaypoint).normalized;
                Vector3 direction2 = (currentWaypoint - previousWaypoint).normalized;

                // 각도 계산
               float angleInDegrees = Vector3.Angle(direction1, direction2);
                //Debug.Log("Angle between waypoints: " + angleInDegrees);
            }

            prerpreviousWaypoint=previousWaypoint;
        }




        //Debug.Log("roll_DIFF: " + Mathf.Abs(CalculateAngleDifference(roll,360)));

        roll = transform.rotation.eulerAngles.z;
        pitch = transform.rotation.eulerAngles.x;
        yaw = transform.rotation.eulerAngles.y;

        //averoll=(prepreviousroll+preroll+roll)/3f;

        //float averoll_C=0;
        if (Mathf.Abs(CalculateAngleDifference(roll,360)) < Threshold_deg){ roll=0f;}
        

        //avepitch=(prepreviouspitch+prepitch+pitch)/3f;

        if (Mathf.Abs(CalculateAngleDifference(pitch,360))<Threshold_deg){ pitch=0f;}
       
       // aveyaw=(prepreviousyaw+previousyaw+yaw)/3f;

        if (Mathf.Abs(CalculateAngleDifference(yaw,360))<Threshold_deg){ yaw=0f;}
        

        
        velz=transform.InverseTransformDirection(rb.velocity).z;
        avevel=(prepreviousvel+previousvel+velz)/(3f);

        accz=(velz-previousvel)/Time.deltaTime;
        aveacc=(prepreviousacc+previousacc+accz)/(3f);
        
        angvel = transform.InverseTransformDirection(rb.angularVelocity).y*Mathf.Rad2Deg;
        aveangvel=(prepreangvel+preangvel+angvel)/(3f);
        accumulated_aveangvel +=aveangvel ;
    

       if(Mathf.Abs(yaw-previousyaw)<Threshold_deg){aveangvel=0f;}

        angleDifference_p = CalculateAngleDifference(roll, preroll)*Mathf.Deg2Rad;
        angleDifference_q = CalculateAngleDifference(pitch, prepitch)*Mathf.Deg2Rad;
        angleDifference_r = CalculateAngleDifference(yaw, previousyaw)*Mathf.Deg2Rad;

       // p = (angleDifference_p) / Time.deltaTime;
        //q = (angleDifference_q) / Time.deltaTime;
        //r = (angleDifference_r) / Time.deltaTime;

       // ave_p=(prepre_p+pre_p+p)/3f;
       // ave_q=(prepre_q+pre_q+q)/3f;
       // ave_r=(prepre_r+pre_r+r)/3f;

        //Debug.Log("angle_Diff: " + angleDifference_p);
        if (Mathf.Abs(angleDifference_p)<Threshold_rad){ p=0f; alpha_x=0f;} 
        if (MathF.Abs(angleDifference_q)<Threshold_rad){ q=0f; alpha_y=0f;} 
        if (MathF.Abs(angleDifference_r)<Threshold_rad){ r=0f; alpha_z=0f;}
        
        //alpha_x = (p-pre_p) / Time.deltaTime;
        //alpha_y = (q-pre_q) / Time.deltaTime;
        //alpha_z = (r-pre_r) / Time.deltaTime;

        //if (Mathf.Abs(p-pre_p)<Threshold_rad){alpha_x=0f;} 
        //if (MathF.Abs(q-pre_q)<Threshold_rad){alpha_y=0f;} 
        //if (MathF.Abs(r-pre_r)<Threshold_rad){alpha_z=0f;}


        accumulated_anglediff_p += angleDifference_p;
        accumulated_anglediff_q += angleDifference_q;
        accumulated_anglediff_r += angleDifference_r;

        accumulatedP += (p -pre_p);
        accumulatedQ += (q- pre_q);
        accumulatedR += (r- pre_r);
        

       if (accumulatedDeltaTime >= Threshold_Time)
        {
            alpha_x= (accumulated_anglediff_p-acc_pre_p)/ accumulatedDeltaTime;
            alpha_y= (accumulated_anglediff_q-acc_pre_q)/ accumulatedDeltaTime;
            alpha_z= (accumulated_anglediff_r-acc_pre_r)/ accumulatedDeltaTime;

            p=accumulated_anglediff_p/ accumulatedDeltaTime;
            q=accumulated_anglediff_q/ accumulatedDeltaTime;
            r=accumulated_anglediff_r/ accumulatedDeltaTime;

            //aveangvel=accumulated_aveangvel/ accumulatedDeltaTime;

            accumulatedDeltaTime=0.0f;
            accumulated_anglediff_p=0.0f;
            accumulated_anglediff_q=0.0f;
            accumulated_anglediff_r=0.0f;

            acc_pre_p=0f;
        

            accumulatedP=0.0f;
            accumulatedQ=0.0f;
            accumulatedR=0.0f;
            
            pre_alpha_x=alpha_x;
            pre_alpha_y=alpha_y;
            pre_alpha_z=alpha_z;

            pre_p=p; pre_q=q; pre_r=r;
        }
        else{
            //aveangvel=preave_angevel;
            alpha_x=pre_alpha_x;
            alpha_y=pre_alpha_y;
            alpha_z=pre_alpha_z;
            p=pre_p;
            q=pre_q;
            r=pre_r;
            //=0f;
            
            acc_pre_p=accumulated_anglediff_p;
            acc_pre_q=accumulated_anglediff_q;
            acc_pre_r=accumulated_anglediff_r;
        }

         if(aveangvel>200f || aveangvel<-200f){
            aveangvel=preave_angevel;
        }


        accz_danger=false; angvel_danger=false; accz_ctrdanger=false; angvel_ctrdanger=false;  accz_maxdanger=false; angvel_maxdanger=false;

        my_S_Test();
        turnover_void(Sau,Sal,Swu,Swl);
        Move(timer);
        Steer();

        
    
        prepreviousvel=previousvel;
        previousvel=velz;

        prepreviousacc=previousacc;
        previousacc=accz;
        
        prepreangvel=preangvel;
        preangvel=angvel;

        prepreviousroll=preroll;
        prepreviouspitch=prepitch;
        prepreviousyaw=previousyaw;

        preroll=roll;
        prepitch=pitch;
        previousyaw=yaw;

        //preaveroll=averoll;
        //preavepitch=avepitch;
        //preaveyaw=aveyaw;
        
        prepre_p=pre_p;
        prepre_q=pre_q;
        prepre_r=pre_r;

        pre_p=p;
        pre_q=q;
        pre_r=r;

        preave_angevel=aveangvel;
        pre_Swl=Swl;
        pre_Swu=Swu;
        
       // preave_p=ave_p;
       // preave_q=ave_q;
       // preave_r=ave_r;

    
        //Debug.Log("p: " + p);
        //Debug.Log("q: " + q);
        //Debug.Log("r: " + r);
       
       //Debug.Log("alpha-X: " + alpha_x);
       //Debug.Log("alpha-Y: " + alpha_y);
       //Debug.Log("alpha-Z: " + alpha_z);
       //Debug.Log("a_Gy: " + a_Gy);
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
        LogRobotData();
    }

    void Move(float timer)
    {
        float coefficient;
        if (accz_maxdanger==true){ 
            coefficient=0.4f; //0.3
            //Brake(); 
            //return;
        }
        else if (accz_ctrdanger==true){coefficient=0.5f;} //0.5 0.6
        else if (accz_danger==true){coefficient=0.65f;} //0.65 0.7
        else{coefficient=1f; }

        if (timer<1)
        {
        foreach (var wheel in wheels)
        {
            wheel.wheelCollider.motorTorque =MotorTorque*(timer/1f)*Time.deltaTime;
            
        }
        }
        else{
            foreach (var wheel in wheels){
                wheel.wheelCollider.motorTorque =MotorTorque*coefficient*Time.deltaTime;
            }
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
        if (angvel_maxdanger==true){ averageAmount=60f;} //35
        else if(angvel_ctrdanger==true){averageAmount=50f;} //20
        else if(angvel_danger==true){averageAmount=35f;} //15
        else { averageAmount=20f;} // 10 또는  20
        
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
     void Brake()
    {
            foreach (var wheel in wheels)
            {
                wheel.wheelCollider.brakeTorque = 2000;
            }

        }

    void LogRobotData()
    {
        using (StreamWriter writer = new StreamWriter(filePath, true))
        {
            writer.WriteLine("Roll: " + roll + " Pitch: " + pitch + " Yaw: " + yaw + " Total_distance: "+ totalDistance);
        }
        using (StreamWriter writer = new StreamWriter(filePath2, true))
        {
            writer.WriteLine("Velocity: " + velz + " Acceleration: " + accz + " Angular Velocity: " + angvel+" a_Gy: "+a_Gy);
        }
        using (StreamWriter writer = new StreamWriter(filePath3, true))
        {
            //writer.WriteLine("Sau: " + Sau + ", Sal:  " + Sal + ", Swu: "+ Swu + ", Swl: "+Swl);
            writer.WriteLine("Swu: "+ Swu + " Swl: "+Swl+ " S_count: "+S_count);
        }
        
    }

    void my_S_Test(){
        //로봇 모델 변수
        float m = 100; // 로봇의 질량 (kg)
        float g = 9.81f; // 중력 가속도 (m/s^2)
        float roll_terrain = 0;

        float T = 0.8f;
        float L = 1.0f; // 로봇의 길이 (m)
        float H = 0.7f; // 로봇의 무게중심 (m)

        float I_xx = 14.35f;
        float I_yy = 8.1f;
        float I_zz = 10.41f;

        //각도 입력
    
        float a_Gx =accz;

        
        //Debug.Log($"p: {r}");
        
        
        //Debug.Log($"alpha_x: {alpha_z}");

        //Debug.Log($"ave_p: {ave_r}");
        //Debug.Log($"preave_p: {preave_r}");


        //Debug.Log($"p: {r}");
        //Debug.Log($"pre_p: {previous_r}");        
        
        float v = avevel;
        //float vmax = 3;
        float w = aveangvel*Mathf.Deg2Rad ;
        a_Gy = velz * angvel*Mathf.Deg2Rad;

        //Debug.Log($"a_Gy: {a_Gy}");

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
        

    

        //Debug.Log($"yzmp: {y_zmp}");
        //Debug.Log($"ave_p: {p}");

        // 좌표 변환
        float[,] zmp = new float[,]
        {
            { x_zmp },
            { y_zmp },
            { 0 }
        };
        
        float x_zmp_mod=0 ;
        float y_zmp_mod=0;

        float[,] zmp_centered = zmp;
        float[,] result_zmp_centered = MatrixMultiply(R, zmp_centered);
        float[,] result_zmp = result_zmp_centered;

        yaw = (yaw % 360 + 360) % 360;
        

        if((yaw>=0 && yaw<=45)||(yaw>=135 && yaw<=225)){
            x_zmp_mod = result_zmp[0, 0];
            y_zmp_mod = result_zmp[1, 0];
        }
        else if((yaw>45 && yaw<135) || (yaw>225 && yaw<315)){
            x_zmp_mod = result_zmp[1, 0];
            y_zmp_mod = result_zmp[0, 0];
        }
        
        //Debug.Log($"xzmp_mod {x_zmp_mod}");
        //Debug.Log($"yzmp_mod {y_zmp_mod}");


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
        float max_x =0;
        float min_x =0;
        float max_y =0;
        float min_y =0;


        // 안정성 한계 계산
        float amax = 15f;
        //float wmax = 120f*Mathf.Deg2Rad;
        float vwmax = 4*6.32f;


        if((yaw>=0 && yaw<=45) || (yaw>315 && yaw<=360)){
            max_x = result_Xu[0,0];
            min_x = result_Xl[0,0];
            max_y = result_Yu[1,0];
            min_y = result_Yl[1,0];
        }
        else if(yaw>135 && yaw<=225){
            max_x = result_Xu[0,0];
            min_x = result_Xl[0,0];
            max_y = result_Yu[1,0];
            min_y = result_Yl[1,0];
        }
        else if(yaw>45 && yaw<=135){
            max_x = result_Xu[1,0];
            min_x = result_Xl[1,0];
            max_y = -result_Yu[0,0];
            min_y = -result_Yl[0,0];
        }
        else{max_x= -result_Xu[1,0];
            min_x = -result_Xl[1,0];
            max_y = result_Yu[0,0];
            min_y = result_Yl[0,0];
        }

        float change;
        if (min_x> max_x){ change=min_x;  max_x=min_x; min_x=change;}
        if(min_y> max_y){change=min_y; max_y=min_y; min_y=change; }
        
        //Debug.Log("Xu: " + max_x + ", Xl: " + min_x + ", Yu: " + max_y + ", Yl: " + min_y);
        Sau = (1 / 2f) * (1 - ((g / (amax * H)) * (x_zmp_mod - max_x)));
        Sal = (1 / 2f) * (1 + ((g / (amax * H)) * (x_zmp_mod - min_x)));
        Swu = (1 / 2f) * (1 - ((g / (vwmax * H))) * (y_zmp_mod - max_y));
        Swl = (1 / 2f) * (1 + ((g / (vwmax * H))) * (y_zmp_mod - min_y));

        //if (Swu >2f || Swu <-2f){
        //    Swu=pre_Swu;
        //}
        if (Swu< -2f || Swl <-2f){
           // Swl=pre_Swl;
        }

        if(Swl<0f || Swu <0f){ S_count++;}
       // if (Math.Abs(Sau) > 2.5f || Math.Abs(Sal) > 2.5f || Math.Abs(Swu) > 2.5f || Math.Abs(Swl) > 2.5f)
        //{
         //   Debug.Log("zmp "+ zmp[1,0] + " " + zmp[0,0]);
            //Debug.Log("MAXMIN "+ max_x + " " + min_x + " " + max_y + " " + min_y);
        // Debug.Log("Sau: "+ Sau + "Sal: " + Sal + "Swu: " + Swu + "Swl: " + Swl + "\n");
        //}
        
        // 결과 출력
        //Debug.Log($"Sau: {Sau}");
        //Debug.Log($"Sal: {Sal}");
       // Debug.Log($"Swu: {Swu}");
        //Debug.Log($"Swl: {Swl}");
        

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
    void turnover_void(float Sau, float Sal, float Swu, float Swl){
        
        //if (Sal<0.6f || Sau<0.6f){ //0.5
        //    accz_danger=false;
        //}
        //else if(Sal<0.4f || Sau<0.4f){ //0.1 
        //    accz_ctrdanger=false;
        //}
        //else if(Sal<0.1f || Sau<0.1f){ //-0.1 
        //    accz_maxdanger=false;
        //}
 

        if (Swl<0.6f || Swu<0.6f){
            accz_danger=true;
            angvel_danger=true;
        }
        else if(Swl<0.35f || Swu<0.35f){
            accz_ctrdanger=true;
            angvel_ctrdanger=true;
        }
        else if(Swl<0.2f || Swu<0.2f)
        {
            accz_maxdanger=true;
            angvel_maxdanger=true;
        }
    
        
    }

    static float NormalizeAngle(float angle)
    {
        return (angle % 360 + 360) % 360;
    }

    static float CalculateAngleDifference(float angle1, float angle2)
    {
        float normalizedAngle1 = NormalizeAngle(angle1);
        float normalizedAngle2 = NormalizeAngle(angle2);

        float difference = normalizedAngle2 - normalizedAngle1;

        if (difference > 180)
        {
            difference -= 360;
        }
        else if (difference < -180)
        {
            difference += 360;
        }

        return difference;
    }

}
