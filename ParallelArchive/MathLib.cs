using System;
using System.Collections.Generic;

namespace MathLib
{
    public sealed class Mathl
    {
        /// <summary>
        /// Clamp value between min and max
        /// </summary>
        /// <param name="value">value to clamp</param>
        /// <param name="min">minimum</param>
        /// <param name="max">maximum</param>
        /// <returns>If value between min and max, returns value, else returns min or max respectively</returns>
        public static int Clamp(int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }
        /// <summary>
        /// Clamp value between min and max
        /// </summary>
        /// <param name="value">value to clamp</param>
        /// <param name="min">minimum</param>
        /// <param name="max">maximum</param>
        /// <returns>If value between min and max, returns value, else returns min or max respectively</returns>
        public static float Clamp(float value, float min, float max)
        {
            return value < min ? min : value > max ? max : value;
        }
        /// <summary>
        /// Clamp value between min and max
        /// </summary>
        /// <param name="value">value to clamp</param>
        /// <param name="min">minimum</param>
        /// <param name="max">maximum</param>
        /// <returns>If value between min and max, returns value, else returns min or max respectively</returns>
        public static decimal Clamp(decimal value, decimal min, decimal max)
        {
            return value < min ? min : value > max ? max : value;
        }
        /// <summary>
        /// Clamp value between min and max
        /// </summary>
        /// <param name="value">value to clamp</param>
        /// <param name="min">minimum</param>
        /// <param name="max">maximum</param>
        /// <returns>If value between min and max, returns value, else returns min or max respectively</returns>
        public static double Clamp(double value, double min, double max)
        {
            return value < min ? min : value > max ? max : value;
        }
        /// <summary>
        /// Clamp value between min and max
        /// </summary>
        /// <param name="value">value to clamp</param>
        /// <param name="min">minimum</param>
        /// <param name="max">maximum</param>
        /// <returns>If value between min and max, returns value, else returns min or max respectively</returns>
        public static long Clamp(long value, long min, long max)
        {
            return value < min ? min : value > max ? max : value;
        }

    }
}