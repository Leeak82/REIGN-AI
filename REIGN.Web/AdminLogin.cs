using System.Security.Cryptography;
using System.Text;

namespace REIGN.Web;

public static class AdminLogin
{
    public static bool IsValid(string? suppliedUsername, string? suppliedPassword, string expectedUsername, string expectedPassword)
    {
        if (!string.Equals(suppliedUsername?.Trim(), expectedUsername, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return FixedTimeEquals(suppliedPassword ?? string.Empty, expectedPassword);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);

        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
