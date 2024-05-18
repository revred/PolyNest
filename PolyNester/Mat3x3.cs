using ClipperLib; // Used for IntPoint conversion
using System;

namespace PolyNester;

public struct Mat3x3
{
    double x11, x12, x13, x21, x22, x23, x31, x32, x33;

    public static Mat3x3 Eye() => Scale(1, 1, 1);
    public static Mat3x3 RotateCounterClockwise(double t)
    {
        Mat3x3 T = new Mat3x3();

        double c = Math.Cos(t);
        double s = Math.Sin(t);
        T.x13 = T.x23 = T.x31 = T.x32 = 0;
        T.x11 = T.x22 = c;
        T.x12 = -s;
        
        T.x21 = s;
        T.x33 = 1;           

        return T;
    }

    public static Mat3x3 Scale(double scale_x, double scale_y, double scale_z = 1.0)
    {
        Mat3x3 I = new Mat3x3();
        I.x12 = I.x13 = I.x21 = I.x23 = I.x31 = I.x32 = 0;
        I.x11 = scale_x;            
        I.x22 = scale_y;            
        I.x33 = scale_z;

        return I;
    }

    public static Mat3x3 Translate(double t_x, double t_y)
    {
        Mat3x3 I = new Mat3x3();
        I.x11 = I.x22 = I.x33 = 1;
        I.x12 = I.x21 = I.x31 = I.x32 = 0;

        I.x13 = t_x;
        I.x23 = t_y;           

        return I;
    }

    public static Mat3x3 operator *(Mat3x3 A, Mat3x3 B)
    {
        Mat3x3 I = new Mat3x3();
        I.x11 = A.x11 * B.x11 + A.x12 * B.x21 + A.x13 * B.x31;
        I.x12 = A.x11 * B.x12 + A.x12 * B.x22 + A.x13 * B.x32;
        I.x13 = A.x11 * B.x13 + A.x12 * B.x23 + A.x13 * B.x33;
        I.x21 = A.x21 * B.x11 + A.x22 * B.x21 + A.x23 * B.x31;
        I.x22 = A.x21 * B.x12 + A.x22 * B.x22 + A.x23 * B.x32;
        I.x23 = A.x21 * B.x13 + A.x22 * B.x23 + A.x23 * B.x33;
        I.x31 = A.x31 * B.x11 + A.x32 * B.x21 + A.x33 * B.x31;
        I.x32 = A.x31 * B.x12 + A.x32 * B.x22 + A.x33 * B.x32;
        I.x33 = A.x31 * B.x13 + A.x32 * B.x23 + A.x33 * B.x33;

        return I;
    }

    public static Vector64 operator *(Mat3x3 A, Vector64 B)
    {
        return new Vector64()
        {
            X = A.x11 * B.X + A.x12 * B.Y + A.x13,
            Y = A.x21 * B.X + A.x22 * B.Y + A.x23
        };
    }

    public static IntPoint operator *(Mat3x3 A, IntPoint B)
    {
        double x = A.x11 * B.X + A.x12 * B.Y + A.x13;
        double y = A.x21 * B.X + A.x22 * B.Y + A.x23;

        return new IntPoint(x, y);
    }

    private double Det2x2(double a11, double a12, double a21, double a22)
        =>  a11 * a22 - a12 * a21;

    public double Determinant()
    {
        return x11 * Det2x2(x22, x23, x32, x33) - 
               x12 * Det2x2(x21, x23, x31, x33) + 
               x13 * Det2x2(x21, x22, x31, x32);
    }

    public Mat3x3 Inverse()
    {   
        double oneByD = 1.0 / Determinant();
        Mat3x3 I = new Mat3x3();
        I.x11 = Det2x2(x22, x23, x32, x33) * oneByD;
        I.x12 = Det2x2(x13, x12, x33, x32) * oneByD;
        I.x13 = Det2x2(x12, x13, x22, x23) * oneByD;
        I.x21 = Det2x2(x23, x21, x33, x31) * oneByD;
        I.x22 = Det2x2(x11, x13, x31, x33) * oneByD;
        I.x23 = Det2x2(x13, x11, x23, x21) * oneByD;
        I.x31 = Det2x2(x21, x22, x31, x32) * oneByD;
        I.x32 = Det2x2(x12, x11, x32, x31) * oneByD;
        I.x33 = Det2x2(x11, x12, x21, x22) * oneByD;

        return I;
    }
}
