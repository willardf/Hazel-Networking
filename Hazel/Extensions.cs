using System.Collections.Generic;

namespace Hazel
{
    public static class Extensions
    {
        public static void Swap<T>(this IList<T> self, int idx0, int idx1)
        {
            var temp = self[idx0];
            self[idx0] = self[idx1];
            self[idx1] = temp;
        }

        public static int ClampToInt(this float value, int min, int max)
        {
            int output = (int)value;
            if (output < min) output = min;
            else if (output > max) output = max;
            return output;
        }

        public static bool TryDequeue<T>(this Queue<T> self, out T item)
        {
            if (self.Count > 0)
            {
                item = self.Dequeue();
                return true;
            }

            item = default;
            return false;
        }
    }
}
