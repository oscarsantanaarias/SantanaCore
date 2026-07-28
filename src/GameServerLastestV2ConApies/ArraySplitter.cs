namespace Santana
{
    using System;
    using System.Collections.Generic;

    public class ArraySplitter
    {
        public static List<T[]> Split<T>(T[] array, int maxSize)
        {
            var chunks = new List<T[]>();
            var cursor = 0;

            while (cursor != array.Length)
            {
                var remaining = array.Length - cursor;
                var take = remaining > maxSize ? maxSize : remaining;

                var chunk = new T[take];
                Array.Copy(array, cursor, chunk, 0, take);
                chunks.Add(chunk);

                cursor += take;
            }

            return chunks;
        }
    }
}
