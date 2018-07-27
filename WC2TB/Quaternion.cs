using System;
using System.Numerics;

namespace Extensions
{
    public static class QuaternionExtension
    {
        public static Vector3 Euler(this Quaternion q)
        {
            float sqx = (float)Math.Pow(q.X, 2);
            float sqy = (float)Math.Pow(q.Y, 2);
            float sqz = (float)Math.Pow(q.Z, 2);
            float sqw = (float)Math.Pow(q.W, 2);

            // Yaw, Pitch, Roll 
            var euler = new Vector3
            (
                (float)(Math.Asin(2 * q.X * q.Y - + 2 * q.Z * q.W) * (180.0 / Math.PI)),
                (float)(Math.Atan2(2 * q.Y * q.W - 2 * q.X * q.Z, 1 - 2 * sqy - 2 * sqz) * (180.0 / Math.PI)),
                (float)(Math.Atan2(2 * q.X * q.W - 2 * q.Y * q.Z, 1 - 2 * sqx - 2 * sqz) * (180.0 / Math.PI))
            );

            return euler;
        }
    }
}
