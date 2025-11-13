using System.Security.Cryptography;
using System.Text;

namespace Ditado.Aplicacao.Services;

public class PasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public string Hash(string senha)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(senha),
            salt,
            Iterations,
            Algorithm,
            KeySize
        );

        return $"{Convert.ToHexString(salt)}-{Convert.ToHexString(hash)}";
    }

    public bool Verify(string senha, string hashArmazenado)
    {
        var parts = hashArmazenado.Split('-');
        if (parts.Length != 2)
            return false;

        var salt = Convert.FromHexString(parts[0]);
        var hashOriginal = Convert.FromHexString(parts[1]);

        var hashTeste = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(senha),
            salt,
            Iterations,
            Algorithm,
            KeySize
        );

        return CryptographicOperations.FixedTimeEquals(hashOriginal, hashTeste);
    }
}