using FloatySyncServer.Data;
using FloatySyncServer.Models;
using Microsoft.AspNetCore.Mvc;

namespace FloatySyncServer.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class DirectoriesController : ControllerBase
	{
		private readonly SyncDbContext _syncDbContext;
		private readonly IWebHostEnvironment _env;

		public DirectoriesController(SyncDbContext syncDbContext, IWebHostEnvironment env)
		{
			_syncDbContext = syncDbContext;
			_env = env;
		}

		[HttpPost("create")]
		public async Task<IActionResult> CreateDirectory(
			[FromBody] string relativePath,
			[FromBody] string groupId,
			[FromBody] string groupKeyPlaintext)
		{
			if (string.IsNullOrEmpty(relativePath)
				|| string.IsNullOrEmpty(groupId)
				|| string.IsNullOrEmpty(groupKeyPlaintext))
			{ return BadRequest("Please enter all necessary information"); }

			var group = _syncDbContext.Groups.Find(Convert.ToInt32(groupId));
			if (group == null)
				return NotFound("Group not found");

			string masterKeyBase64 = System.IO.File.ReadAllText(Path.Combine(_env.ContentRootPath, "key.txt"));
			string decryptedKey = Helpers.DecryptString(group.EncryptedSecretKey, masterKeyBase64);

			if (decryptedKey != groupKeyPlaintext)
			{
				return StatusCode(StatusCodes.Status403Forbidden, "Invalid group / key");
			}

			string basePath = Path.Combine(_env.ContentRootPath, "SyncData");
			if (!Directory.Exists(basePath))
				Directory.CreateDirectory(basePath);

			var groupFolder = Path.Combine(basePath, groupId);
			Directory.CreateDirectory(groupFolder);

			string fullPath = Path.Combine(groupFolder, relativePath);
			if (!Directory.Exists(fullPath))
				Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

			FileMetadata direcotryMetaData = new FileMetadata();
			direcotryMetaData.GroupId = groupId;
			direcotryMetaData.Checksum = null;
			direcotryMetaData.StoredPathOnServer = fullPath;
			direcotryMetaData.RelativePath = relativePath;
			direcotryMetaData.IsDirectory = true;

			await _syncDbContext.Files.AddAsync(direcotryMetaData);
			await _syncDbContext.SaveChangesAsync();

			return Ok("Created Directory");
		}

		[HttpPost("rename")]
		public IActionResult RenameDir([FromBody] DirectoryRenameDto dto)
		{
			if (!TryAuthorize(dto.GroupId, dto.GroupKey, out var _)) return Forbid();

			string oldPrefix = dto.OldPath.TrimEnd('/') + "/";
			string newPrefix = dto.NewPath.TrimEnd('/') + "/";

			string oldPhys = FullPath(dto.GroupId, dto.OldPath);
			string newPhys = FullPath(dto.GroupId, dto.NewPath);

			if (Directory.Exists(oldPhys))
			{
				Directory.CreateDirectory(Path.GetDirectoryName(newPhys)!);
				Directory.Move(oldPhys, newPhys);
			}

			var rows = _syncDbContext.Files.Where(f => f.GroupId == dto.GroupId.ToString() &&
											f.RelativePath.StartsWith(oldPrefix))
								.ToList();

			foreach (var row in rows)
			{
				string tail = row.RelativePath.Substring(oldPrefix.Length);
				row.RelativePath = newPrefix + tail;
				row.LastModifiedUtc = DateTime.UtcNow;
			}

			var dirRow = _syncDbContext.Files.FirstOrDefault(f =>
						f.GroupId == dto.GroupId.ToString() &&
						f.RelativePath == dto.OldPath &&
						f.IsDirectory && !f.IsDeleted);
			if (dirRow != null)
			{
				dirRow.RelativePath = dto.NewPath;
				dirRow.LastModifiedUtc = DateTime.UtcNow;
			}

			_syncDbContext.SaveChanges();
			return Ok();
		}

		[HttpDelete]
		public IActionResult DeleteDir([FromQuery] int groupId,
							   [FromQuery] string groupKeyPlaintext,
							   [FromQuery] string relativePath,
							   [FromQuery] bool hard = true) // Optional flag if you dont want to delete the files
		{
			if (!TryAuthorize(groupId, groupKeyPlaintext, out var _)) return Forbid();

			string prefix = relativePath.TrimEnd('/') + "/";

			var rows = _syncDbContext.Files.Where(f => f.GroupId == groupId.ToString() &&
											(f.RelativePath == relativePath || f.RelativePath.StartsWith(prefix)))
								.ToList();
			foreach (var r in rows)
			{
				r.IsDeleted = true;
				r.LastModifiedUtc = DateTime.UtcNow;
			}
			_syncDbContext.SaveChanges();

			if (hard)
			{
				string phys = FullPath(groupId, relativePath);
				if (Directory.Exists(phys))
					Directory.Delete(phys, recursive: true);
			}
			return Ok();
		}


		private bool TryAuthorize(int groupId, string keyPlain, out Group group)
		{
			group = _syncDbContext.Groups.Find(groupId)!;
			if (group == null) return false;

			var masterKey = System.IO.File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "key.txt"));
			var decrypted = Helpers.DecryptString(group.EncryptedSecretKey, masterKey);
			return decrypted == keyPlain;
		}

		private string DataRoot => Path.Combine(_env.ContentRootPath, "SyncData");

		private string FullPath(int groupId, string relativePath)
		{
			// group folders named by ID:  DataRoot / {groupId} /  relativePath
			return Path.Combine(DataRoot, groupId.ToString(), relativePath.Replace('/', Path.DirectorySeparatorChar));
		}
	}

	public class DirectoryRenameDto
	{
		public int GroupId { get; set; }
		public string GroupKey { get; set; } = default!;
		public string OldPath { get; set; } = default!;
		public string NewPath { get; set; } = default!;
	}
}
