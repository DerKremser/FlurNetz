using System.Security.Cryptography;
using System.Text;

namespace FlurNetz.Persistence.Migrations;

/// <summary>
/// Erzeugt den technischen Integritätsnachweis für einen Migration-SQL-Text.
/// </summary>
public static class MigrationChecksum
{
    /// <summary>
    /// Berechnet den SHA-256-Hash der UTF-8-Bytes des exakten SQL-Texts als kleingeschriebene Hexadezimalzahl.
    /// </summary>
    /// <param name="sql">Der SQL-Text einschließlich seiner bewussten Leerzeichen und Zeilenumbrüche.</param>
    /// <returns>Eine deterministische 64-stellige SHA-256-Hexadezimalzahl.</returns>
    /// <exception cref="ArgumentNullException">Wenn <paramref name="sql"/> fehlt.</exception>
    public static string Compute(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sql));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
