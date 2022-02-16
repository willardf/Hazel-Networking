using System.Collections.Generic;

namespace Hazel
{
    public static class Extensions
    {
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
