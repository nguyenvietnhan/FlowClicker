using System;

namespace FlowClicker.Core.Engine
{
    public sealed class Humanizer
    {
        private readonly Random _random = new();

        /// <summary>
        /// Generates human-like delay variation using the Box-Muller Gaussian (Normal) Distribution.
        /// </summary>
        public double GenerateGaussianJitter(double mean, double standardDeviation)
        {
            if (standardDeviation <= 0) return mean;

            double u1 = 1.0 - _random.NextDouble();
            double u2 = 1.0 - _random.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            double result = mean + (standardDeviation * randStdNormal);

            return Math.Max(0.5, result);
        }

        /// <summary>
        /// Generates small coordinate jitter offsets using uniform disk sampling.
        /// </summary>
        public (int offsetX, int offsetY) GenerateCoordinateJitter(int maxRadius)
        {
            if (maxRadius <= 0) return (0, 0);

            double angle = _random.NextDouble() * 2.0 * Math.PI;
            double radius = Math.Sqrt(_random.NextDouble()) * maxRadius;

            int dx = (int)Math.Round(radius * Math.Cos(angle));
            int dy = (int)Math.Round(radius * Math.Sin(angle));
            return (dx, dy);
        }
    }
}
