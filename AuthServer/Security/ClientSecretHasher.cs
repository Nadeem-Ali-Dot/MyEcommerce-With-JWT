namespace AuthServer.Security
{
    // Concrete implementation of IClientSecretHasher.
    // This class is responsible for hashing and verifying
    // client application secrets (not user passwords).
    public class ClientSecretHasher : IClientSecretHasher
    {
        // Hashes a client secret using BCrypt.
        // - Client secrets must NEVER be stored in plain text.
        // - BCrypt automatically generates a unique salt per hash.
        // - The adaptive cost factor makes brute-force attacks expensive.
        // - The resulting hash safely contains the salt and work factor.
        public string Hash(string secret)
        {
            // The returned value is safe to store in the database.
            // No additional salt management is required.
            return BCrypt.Net.BCrypt.HashPassword(secret);
        }

        // Verifies a raw client secret against the stored BCrypt hash.
        // - BCrypt internally extracts the salt and cost factor
        //   from the stored hash.
        // - The provided secret is re-hashed and securely compared.
        // - Resistant to timing attacks and hash comparison leaks.
        public bool Verify(string secret, string secretHash)
        {
            // Returns true if the secret matches the stored hash.
            // Returns false if the secret is invalid or altered.
            return BCrypt.Net.BCrypt.Verify(secret, secretHash);
        }
    }

}
