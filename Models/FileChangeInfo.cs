namespace FloatySyncServer.Models
{
	public class FileChangeInfo
	{
		public int FileId { get; set; }
		public string RelativePath { get; set; }
		public bool IsDeleted { get; set; }
		public DateTime LastModifiedUtc { get; set; }
	}
}
