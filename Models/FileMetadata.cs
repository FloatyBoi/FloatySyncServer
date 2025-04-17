using System.ComponentModel.DataAnnotations;

namespace FloatySyncServer.Models
{
	public class FileMetadata
	{
		[Key]
		public int Id { get; set; }

		// "Notes/File.txt"
		public string RelativePath { get; set; } = default!;

		// For conflict detection
		public DateTime LastModifiedUtc { get; set; }

		public string? Checksum { get; set; }
		public string? GroupId { get; set; }

		public bool IsDeleted = false;

		public bool IsDirectory { get; set; }

		public string? StoredPathOnServer { get; set; }
	}
}
