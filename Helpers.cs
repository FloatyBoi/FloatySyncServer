using System.Security.Cryptography;
using FloatySyncServer.Data;
using System.Text.RegularExpressions;
using FloatySyncServer.Models;
using System.Text;
namespace FloatySyncServer
{
	public class Helpers
	{
		public static string ComputeFileChecksum(string filePath)
		{
			// Ensure the file exists
			if (!File.Exists(filePath))
				throw new FileNotFoundException("File not found.", filePath);

			using var sha256 = SHA256.Create();
			using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);

			// Compute hash returns a byte[]
			byte[] hashBytes = sha256.ComputeHash(stream);

			// Convert to a readable hex string
			return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
		}

		public static FileMetadata? TryGetFileMetadata(string relativePath, string groupId, SyncDbContext syncDbContext)
		{
			return syncDbContext.Files.FirstOrDefault(f => f.RelativePath == relativePath && f.GroupId == groupId);
		}

		public static string EncryptString(string plainText, string masterKeyBase64)
		{
			byte[] keyBytes = Convert.FromBase64String(masterKeyBase64);

			using Aes aes = Aes.Create();
			aes.Key = keyBytes;
			aes.GenerateIV();

			using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
			using var ms = new MemoryStream();

			ms.Write(aes.IV, 0, aes.IV.Length);
			using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
			{
				using var sw = new StreamWriter(cs);
				sw.Write(plainText);
			}

			return Convert.ToBase64String(ms.ToArray());
		}

		public static string DecryptString(string cipherTextBase64, string masterKeyBase64)
		{
			byte[] cipherBytes = Convert.FromBase64String(cipherTextBase64);
			byte[] keyBytes = Convert.FromBase64String(masterKeyBase64);

			using Aes aes = Aes.Create();
			aes.Key = keyBytes;

			byte[] iv = new byte[aes.BlockSize / 8];
			Array.Copy(cipherBytes, 0, iv, 0, iv.Length);
			aes.IV = iv;

			using var ms = new MemoryStream(cipherBytes, iv.Length, cipherBytes.Length - iv.Length);
			using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
			using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
			using var sr = new StreamReader(cs);
			return sr.ReadToEnd();
		}
	}
}
