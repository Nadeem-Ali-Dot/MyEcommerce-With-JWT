namespace AuthServer.Security
{
    // Concrete implementation of IPasswordHasher using BCrypt.
    // BCrypt is a strong, adaptive hashing algorithm designed
    // specifically for password storage and verification.
    public class BcryptPasswordHasher : IPasswordHasher
    {
        // Hashes a plain-text password using BCrypt.
        // - BCrypt automatically generates a unique salt for each password.
        // - The hashing process is intentionally slow, making brute-force
        //   and rainbow-table attacks extremely difficult.
        // - The returned value includes both the salt and cost factor,
        //   so, no additional metadata needs to be stored.
        public string Hash(string password)
        {
            // NEVER store or log plain-text passwords.
            // This method returns a one-way hash that is safe to persist.
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        // Verifies a plain-text password against a stored BCrypt hash.
        // - BCrypt extracts the salt and cost factor from the stored hash.
        // - The input password is re-hashed and compared securely.
        // - This comparison is resistant to timing attacks.
        public bool Verify(string password, string passwordHash)
        {
            // Returns true if the password matches the hash,
            // false otherwise (no exception thrown for mismatches).
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
    }

}
