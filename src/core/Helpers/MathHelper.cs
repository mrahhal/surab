using System.Diagnostics;

namespace Surab;

internal class MathHelper
{
    public static bool IsPowerOfTwo(int n)
    {
        Debug.Assert(n > 0);
        return (n & (n - 1)) == 0;
    }
}
