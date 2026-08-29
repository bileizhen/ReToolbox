using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace ReToolbox.Utils
{
    public static class ArtifactIntegrity
    {
        public static async Task<bool> HasExpectedSha256Async(
            Stream stream,
            string expectedSha256,
            CancellationToken cancellationToken = default)
        {
            if (!stream.CanRead || expectedSha256.Length != 64)
            {
                return false;
            }

            byte[] expected;
            try
            {
                expected = Convert.FromHexString(expectedSha256);
            }
            catch (FormatException)
            {
                return false;
            }

            byte[] actual = await SHA256.HashDataAsync(stream, cancellationToken);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
    }
}
