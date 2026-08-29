using System.Security.Cryptography;
using System.Text;

namespace FlurNetz.Persistence.Migrations;

public static class MigrationChecksum
{
    public static string Compute(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sql));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
