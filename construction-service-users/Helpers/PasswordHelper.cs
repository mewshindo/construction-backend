using System.Security.Cryptography;

namespace ConstructionServiceUsers.Helpers;

public static class PasswordHelper
{
    public static string HashPassword(string password)
    {
        // Generate a random salt
        byte[] salt = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        // Hash the password with the salt
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
        byte[] hash = pbkdf2.GetBytes(32);

        // Combine salt and hash
        byte[] hashBytes = new byte[48];
        Array.Copy(salt, 0, hashBytes, 0, 16);
        Array.Copy(hash, 0, hashBytes, 16, 32);

        // Convert to base64 string
        return Convert.ToBase64String(hashBytes);
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
            return false;

        try
        {
            Console.WriteLine($"Verifying password. Stored hash: {storedHash}");
            
            // Convert from base64 string
            byte[] hashBytes = Convert.FromBase64String(storedHash);

        // Get the salt
        byte[] salt = new byte[16];
        Array.Copy(hashBytes, 0, salt, 0, 16);

        // Compute hash from password with salt
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
        byte[] hash = pbkdf2.GetBytes(32);

        // Compare the results
        bool isMatch = true;
        for (int i = 16; i < 48; i++)
        {
            if (hashBytes[i] != hash[i - 16])
            {
                isMatch = false;
                break;
            }
        }
        
        Console.WriteLine($"Password verification result: {isMatch}");
        return isMatch;
        }
        catch
        {
            return false;
        }
    }
}