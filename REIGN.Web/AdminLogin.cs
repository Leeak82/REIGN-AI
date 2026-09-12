using System.Security.Cryptography;
using System.Text;

namespace REIGN.Web;

public static class AdminLogin
{
    public static bool IsValid(string? suppliedUsername, string? suppliedPassword, string expectedUsername, string expectedPasswordHash)
    {
        if (!string.Equals(suppliedUsername?.Trim(), expectedUsername, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return VerifyPassword(suppliedPassword ?? string.Empty, expectedPasswordHash);
    }

    public static bool VerifyPassword(string password, string encodedHash)
    {
        var parts = encodedHash.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || !parts[0].Equals("pbkdf2-sha256", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var iterations) || iterations < 100_000)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expected.Length);

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
