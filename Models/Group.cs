namespace FloatySyncServer.Models
{
	public class Group
	{
		public int Id { get; set; }

		public string Name { get; set; } = default!;

		public string EncryptedSecretKey { get; set; } = default!;

		public string? EncryptionIV { get; set; }
	}
}
