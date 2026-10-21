using System.Security.Cryptography;

namespace JastipApp.Helpers;

/// <summary>
/// Hash password dengan PBKDF2-SHA256 + salt acak (bawaan .NET, tanpa paket tambahan).
/// Hasil disimpan sebagai teks: pbkdf2$iterasi$salt(base64)$hash(base64).
/// </summary>
public static class PasswordHasher
{
    private const int Iterasi = 100_000;
    private const int PanjangSalt = 16;
    private const int PanjangHash = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(PanjangSalt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterasi, HashAlgorithmName.SHA256, PanjangHash);
        return $"pbkdf2${Iterasi}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verifikasi(string password, string tersimpan)
    {
        var bagian = tersimpan.Split('$');
        if (bagian.Length != 4 || bagian[0] != "pbkdf2" || !int.TryParse(bagian[1], out var iterasi))
            return false;

        try
        {
            var salt = Convert.FromBase64String(bagian[2]);
            var hashAsli = Convert.FromBase64String(bagian[3]);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterasi, HashAlgorithmName.SHA256, hashAsli.Length);
            return CryptographicOperations.FixedTimeEquals(hash, hashAsli);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
