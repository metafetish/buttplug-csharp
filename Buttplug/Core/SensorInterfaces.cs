using System;
using System.Collections.Generic;
using System.Text;

namespace Buttplug.Core
{
    [Flags]
    public enum StandardSensorModalities
    {
        None = 0x00,
        Pressure = 0x01,
        Position = 0x02,
        Acceleration = 0x04,
        AngularVelocity = 0x08,
        Inclination = 0x10,
        Magnetometer = 0x20,
        AngularAcceleration = 0x40,
        All = 0xFF,
    }

    [Serializable]
    public struct Vector3 : IVector3
    {
        public Vector3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; set; }

        public double Y { get; set; }

        public double Z { get; set; }

        /// <summary>
        /// Return a copy of the vector with absolute values for each component
        /// </summary>
        /// <returns></returns>
        public Vector3 Abs()
        {
            return new Vector3(Math.Abs(X), Math.Abs(Y), Math.Abs(Z));
        }

        public override string ToString()
        {
            return string.Format("{0:F05} {1:F05} {2:F05}", X, Y, Z);
        }

        public static Vector3 operator +(Vector3 A, Vector3 B)
        {
            return new Vector3()
            {
                X = A.X + B.X,
                Y = A.Y + B.Y,
                Z = A.Z + B.Z,
            };
        }

        public static Vector3 operator -(Vector3 A, Vector3 B)
        {
            return new Vector3()
            {
                X = A.X - B.X,
                Y = A.Y - B.Y,
                Z = A.Z - B.Z,
            };
        }

        public static Vector3 operator *(Vector3 A, Vector3 B)
        {
            return new Vector3()
            {
                X = A.X * B.X,
                Y = A.Y * B.Y,
                Z = A.Z * B.Z,
            };
        }

        public static Vector3 operator /(Vector3 A, Vector3 B)
        {
            return new Vector3()
            {
                X = A.X / B.X,
                Y = A.Y / B.Y,
                Z = A.Z / B.Z,
            };
        }

        public static Vector3 operator +(Vector3 A, double B)
        {
            return new Vector3()
            {
                X = A.X + B,
                Y = A.Y + B,
                Z = A.Z + B,
            };
        }

        public static Vector3 operator -(Vector3 A, double B)
        {
            return new Vector3()
            {
                X = A.X - B,
                Y = A.Y - B,
                Z = A.Z - B,
            };
        }

        public static Vector3 operator *(Vector3 A, double B)
        {
            return new Vector3()
            {
                X = A.X * B,
                Y = A.Y * B,
                Z = A.Z * B,
            };
        }

        public static Vector3 operator /(Vector3 A, double B)
        {
            return new Vector3()
            {
                X = A.X / B,
                Y = A.Y / B,
                Z = A.Z / B,
            };
        }

        public double Magnitude
        {
            get
            {
                return Math.Sqrt((X * X) + (Y * Y) + (Z * Z));
            }
        }

        public Vector3 Sqrt()
        {
            return new Vector3(Math.Sqrt(X), Math.Sqrt(Y), Math.Sqrt(Z));
        }
    }

    public interface IVector3
    {
        double X { get; }

        double Y { get; }

        double Z { get; }
    }
}
