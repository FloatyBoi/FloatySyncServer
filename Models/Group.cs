using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FloatySyncServer.Models
{
	public class Group
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }

		public string Name { get; set; } = default!;

		public string EncryptedSecretKey { get; set; } = default!;

		public string? EncryptionIV { get; set; }
	}
}
