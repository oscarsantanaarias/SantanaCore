using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace Santana
{
    public class SecureRandom : RandomNumberGenerator
    {
        private readonly RandomNumberGenerator _source = RandomNumberGenerator.Create();

        public int Next()
        {
            var buffer = new byte[sizeof(int)];
            _source.GetBytes(buffer);
            return BitConverter.ToInt32(buffer, 0) & (int.MaxValue - 1);
        }

        public int Next(int maxValue)
        {
            return Next(0, maxValue);
        }

        public int Next(int minValue, int maxValue)
        {
            if (minValue > maxValue)
                throw new ArgumentOutOfRangeException();
            return (int)Math.Floor(minValue + ((double)maxValue - minValue) * NextDouble());
        }

        public double NextDouble()
        {
            var buffer = new byte[sizeof(uint)];
            _source.GetBytes(buffer);
            var sample = BitConverter.ToUInt32(buffer, 0);
            return sample / (uint.MaxValue + 1.0);
        }

        public double NextDouble(double minimum, double maximum)
        {
            return NextDouble() * (maximum - minimum) + minimum;
        }

        public int Probability(int minValue, int maxValue, int PlayerCount)
        {
            var roll = Next(minValue, maxValue);

            if (PlayerCount == roll && NextDouble() < 0.7)
                return roll;

            for (int attempt = 0; attempt < maxValue; attempt++)
            {
                if (roll != PlayerCount)
                    return roll;

                return Next(minValue, maxValue);
            }
            return roll;
        }

        public override void GetBytes(byte[] data)
        {
            _source.GetBytes(data);
        }

        public override void GetNonZeroBytes(byte[] data)
        {
            _source.GetNonZeroBytes(data);
        }
    }
}
