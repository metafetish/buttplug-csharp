using System;
using System.Collections.Generic;
using System.Text;

namespace Buttplug.Core
{
    [Serializable]
    public class SensorData : Dictionary<StandardSensorModalities, Vector3>
    {
        public DateTime TimeStamp { get; private set; } = DateTime.UtcNow;

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var key in this.Keys)
            {
                sb.Append($"{key}: {this[key]}\n");
            }

            return sb.ToString();
        }

        public new Vector3 this[StandardSensorModalities mode]
        {
            get
            {
                if (this.ContainsKey(mode) == false)
                {
                    return new Vector3() { X = 0, Y = 0, Z = 0 };
                }

                return base[mode];
            }

            set
            {
                base[mode] = value;
            }
        }

        private static SensorData ApplyOperation(SensorData A, SensorData B, Func<Vector3, Vector3, Vector3> function)
        {
            var output = new SensorData() { TimeStamp = A.TimeStamp };
            SortedSet<StandardSensorModalities> keys = new SortedSet<StandardSensorModalities>(A.Keys);
            keys.UnionWith(B.Keys);

            foreach (var key in keys)
            {
                bool inA = A.ContainsKey(key);
                bool inB = B.ContainsKey(key);
                if (inA && inB)
                {
                    output[key] = function(A[key], B[key]);
                }
                else if ((inA || inB) == false)
                {
                    continue;
                }
                else
                {
                    output[key] = inA ? A[key] : B[key];
                }
            }

            return output;
        }

        private static SensorData ApplyScalarOperation(SensorData A, double scalar, Func<Vector3, double, Vector3> function)
        {
            var output = new SensorData() { TimeStamp = A.TimeStamp };

            foreach (var key in A.Keys)
            {
               output[key] = function(A[key], scalar);
            }

            return output;
        }

        public SensorData Sqrt()
        {
            var output = new SensorData() { TimeStamp = this.TimeStamp };

            foreach (var key in this.Keys)
            {
                output[key] = this[key].Sqrt();
            }

            return output;
        }

        public SensorData Abs()
        {
            var output = new SensorData() { TimeStamp = this.TimeStamp };

            foreach (var key in this.Keys)
            {
                output[key] = this[key].Abs();
            }

            return output;
        }

        public static SensorData operator -(SensorData A, SensorData B)
        {
           return ApplyOperation(A, B, (a_val, b_val) => a_val - b_val);
        }

        public static SensorData operator +(SensorData A, SensorData B)
        {
            return ApplyOperation(A, B, (a_val, b_val) => a_val + b_val);
        }

        public static SensorData operator *(SensorData A, SensorData B)
        {
            return ApplyOperation(A, B, (a_val, b_val) => a_val * b_val);
        }

        public static SensorData operator /(SensorData A, SensorData B)
        {
            return ApplyOperation(A, B, (a_val, b_val) => a_val / b_val);
        }

        public static SensorData operator +(SensorData A, double B)
        {
            return ApplyScalarOperation(A, B, (a_val, b_val) => a_val + b_val);
        }

        public static SensorData operator -(SensorData A, double B)
        {
            return ApplyScalarOperation(A, B, (a_val, b_val) => a_val - b_val);
        }

        public static SensorData operator *(SensorData A, double B)
        {
            return ApplyScalarOperation(A, B, (a_val, b_val) => a_val * b_val);
        }

        public static SensorData operator /(SensorData A, double B)
        {
            return ApplyScalarOperation(A, B, (a_val, b_val) => a_val / b_val);
        }
    }
}
