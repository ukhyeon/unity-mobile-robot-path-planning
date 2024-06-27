using System;

class Program
{
    static void Main()
    {
        //로봇 모델 변수
        float m = 70; // 로봇의 질량 (kg)
        float g = 9.81f; // 중력 가속도 (m/s^2)
        float roll_terrain = 0;

        float T = 0.7f;
        float L = 0.85f; // 로봇의 길이 (m)
        float W = 0.5f; // 로봇의 너비 (m)
        float H = 0.3f + 0.2f + 0.5f; // 로봇의 무게중심 (m)

        float I_xx = 10.0475f;
        float I_yy = 5.67291f;
        float I_zz = 7.29166f;
        

        //각도 입력
        Console.Write("roll 값을 입력하세요: ");
        float roll = float.Parse(Console.ReadLine());
        Console.Write("pitch 값을 입력하세요: ");
        float pitch = float.Parse(Console.ReadLine());
        Console.Write("yaw 값을 입력하세요: ");
        float yaw = float.Parse(Console.ReadLine());
        Console.Write("x방향 가속도를 입력하세요: ");
        float a_Gx = float.Parse(Console.ReadLine());
        float dt = 1;

        // 각가속도 계산
        float p = roll / dt;
        float q = pitch / dt;
        float r = yaw / dt;
        float alpha_x = p / dt;
        float alpha_y = q / dt;
        float alpha_z = r / dt;
        float v = 3;
        float vmax = 5;
        float w = r * (float)Math.PI / 180;
        float a_Gy = v * w;

        // Roll, Pitch, Yaw 회전 행렬 생성
        float[,] R_roll = new float[,]
        {
            {1f, 0, 0},
            {0, (float)Math.Cos(roll), -(float)Math.Sin(roll)},
            {0, (float)Math.Sin(roll), (float)Math.Cos(roll)}
        };

        float[,] R_pitch = new float[,]
        {
            {(float)Math.Cos(pitch), 0, (float)Math.Sin(pitch)},
            {0, 1f, 0},
            {-(float)Math.Sin(pitch), 0, (float)Math.Cos(pitch)}
        };

        float[,] R_yaw = new float[,]
        {
            {(float)Math.Cos(yaw), -(float)Math.Sin(yaw), 0},
            {(float)Math.Sin(yaw), (float)Math.Cos(yaw), 0},
            {0, 0, 1f}
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

        // 모서리 좌표 설정
        float[,] corners = new float[,]
        {
            { -L / 2, -W / 2, 0 },
            { -L / 2, W / 2, 0 },
            { L / 2, W / 2, 0 },
            { L / 2, -W / 2, 0 }
        };

        // 좌표 변환
        float[,] transformed_corners = MatrixMultiply(R, corners);

        // 각 선분의 중점 계산 및 변수 할당
        float[,] midpoints = new float[3, 4];
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4; // 다음 점의 인덱스 (순환)
            midpoints[0, i] = (transformed_corners[0, i] + transformed_corners[0, j]) / 2;
            midpoints[1, i] = (transformed_corners[1, i] + transformed_corners[1, j]) / 2;
            midpoints[2, i] = (transformed_corners[            2, i] + transformed_corners[2, j]) / 2;
        }

        // 각 중점 계산
        float[] m1 = new float[] { midpoints[0, 0], midpoints[1, 0] };
        float[] m2 = new float[] { midpoints[0, 1], midpoints[1, 1] };
        float[] m3 = new float[] { midpoints[0, 2], midpoints[1, 2] };
        float[] m4 = new float[] { midpoints[0, 3], midpoints[1, 3] };

        // 최대값과 최소값 계산
        float max_x = m3[0];
        float min_x = m1[0];
        float max_y = m2[1];
        float min_y = m4[1];

        // 안정성 한계 계산
        float amax = 8;
        float wmax = (float)Math.PI / 2;
        float vwmax = vmax * wmax;

        float Sau = (1 / 2f) * (1 - (g / (amax * H) * (x_zmp_mod - max_x)));
        float Sal = (1 / 2f) * (1 + (g / (amax * H) * (x_zmp_mod - min_x)));
        float Swu = (1 / 2f) * (1 - (g / (vwmax * H)) * (y_zmp_mod - max_y));
        float Swl = (1 / 2f) * (1 + (g / (vwmax * H)) * (y_zmp_mod - min_y));

        // 결과 출력
        Console.WriteLine($"Sau: {Sau}");
        Console.WriteLine($"Sal: {Sal}");
        Console.WriteLine($"Swu: {Swu}");
        Console.WriteLine($"Swl: {Swl}");
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

