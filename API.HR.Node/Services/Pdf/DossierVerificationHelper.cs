using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using API.HR.Node.Data;

namespace API.HR.Node.Services.Pdf;

public static class DossierVerificationHelper
{
    private const string Base62Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    /// <summary>
    /// Generates a 128-bit cryptographically secure, opaque Base62 token (~22 characters).
    /// Safe for direct URL inclusion, non-guessable, non-sequential, and reveals zero metadata.
    /// </summary>
    public static string GenerateOpaqueToken(int byteLength = 16)
    {
        var randomBytes = RandomNumberGenerator.GetBytes(byteLength);

        // Treat bytes as unsigned positive big integer
        var number = new BigInteger(randomBytes, isUnsigned: true, isBigEndian: true);
        var sb = new StringBuilder();

        while (number > 0)
        {
            number = BigInteger.DivRem(number, 62, out var remainder);
            sb.Append(Base62Alphabet[(int)remainder]);
        }

        // Pad if needed to reach consistent length (~22 chars)
        while (sb.Length < 22)
        {
            sb.Append(Base62Alphabet[0]);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Computes a canonical SHA-256 digest of key employee properties.
    /// Stored at export time and re-calculated upon verification scan to detect
    /// if the printed document reflects current system data or subsequent updates.
    /// </summary>
    public static string ComputeEmployeeDataDigest(Employee emp)
    {
        var canonical = string.Join('|',
            emp.EmployeeID?.Trim() ?? string.Empty,
            emp.Name?.Trim() ?? string.Empty,
            emp.Formation?.Name?.Trim() ?? string.Empty,
            emp.JobTitle?.Name?.Trim() ?? string.Empty,
            emp.PositionTitle?.Name?.Trim() ?? string.Empty,
            emp.Grade?.Name?.Trim() ?? string.Empty,
            emp.Stage?.Name?.Trim() ?? string.Empty,
            emp.EmployeeStatus?.Name?.Trim() ?? string.Empty,
            emp.HireDate?.ToString("yyyyMMdd") ?? string.Empty,
            emp.BirthDate?.ToString("yyyyMMdd") ?? string.Empty,
            emp.NationalId?.Trim() ?? string.Empty,
            emp.UnifiedEmployeeNumber?.Trim() ?? string.Empty,
            emp.MotherName?.Trim() ?? string.Empty
        );

        var bytes = Encoding.UTF8.GetBytes(canonical);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
