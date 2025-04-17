using FloatySyncServer.Data;
using FloatySyncServer.Models;
using Microsoft.AspNetCore.Mvc;

namespace FloatySyncServer.Controllers
{
	[ApiController]
	[Route("api/directories")]
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
		public IActionResult CreateDir([FromBody] DirectoryCreateDto dto)
		{
			if (!TryAuthorize(dto.GroupId, dto.GroupKey, out var g)) return Forbid();

			string rel = PathNorm.Normalize(dto.RelativePath);

			var exists = _syncDbContext.Files.Any(f => f.GroupId == dto.GroupId.ToString() &&
											f.RelativePath == rel &&
											!f.IsDeleted &&
											f.IsDirectory);
			if (!exists)
			{
				_syncDbContext.Files.Add(new FileMetadata
				{
					GroupId = dto.GroupId.ToString(),
					RelativePath = rel,
					IsDirectory = true,
					LastModifiedUtc = DateTime.UtcNow
				});
				_syncDbContext.SaveChanges();
			}

			string phys = FullPath(dto.GroupId, rel);
			Directory.CreateDirectory(phys);

			return Ok();
		}

		[HttpPost("rename")]
		public IActionResult RenameDir([FromBody] DirectoryRenameDto dto)
		{
			if (!TryAuthorize(dto.GroupId, dto.GroupKey, out var _)) return Forbid();

			string oldNorm = PathNorm.Normalize(dto.OldPath);
			string newNorm = PathNorm.Normalize(dto.NewPath);

			string oldPrefix = oldNorm + "/";
			string newPrefix = newNorm + "/";

			string oldPhys = FullPath(dto.GroupId, oldNorm);
			string newPhys = FullPath(dto.GroupId, newNorm);

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
						f.RelativePath == oldNorm &&
						f.IsDirectory && !f.IsDeleted);
			if (dirRow != null)
			{
				dirRow.RelativePath = newNorm;
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

			string rel = PathNorm.Normalize(relativePath);
			string prefix = rel + "/";

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
				string phys = FullPath(groupId, rel);
				if (Directory.Exists(phys))
					Directory.Delete(phys, recursive: true);
			}
			return Ok();
		}


		private bool TryAuthorize(int groupId, string keyPlain, out Group group)
		{
			group = _syncDbContext.Groups.Find(groupId)!;
			if (group == null) return false;

			var masterKey = System.IO.File.ReadAllText(Path.Combine(_env.ContentRootPath, "key.txt"));
			var decrypted = Helpers.DecryptString(group.EncryptedSecretKey, masterKey);
			return decrypted == keyPlain;
		}

		private string DataRoot => Path.Combine(_env.ContentRootPath, "SyncData");

		private string FullPath(int groupId, string relativePath)
		{
			// group folders named by ID:  DataRoot / {groupId} /  relativePath
			return Path.Combine(DataRoot, groupId.ToString(), PathNorm.ToDisk(relativePath));
		}
	}

	public class DirectoryRenameDto
	{
		public int GroupId { get; set; }
		public string GroupKey { get; set; } = default!;
		public string OldPath { get; set; } = default!;
		public string NewPath { get; set; } = default!;
	}

	public class DirectoryCreateDto
	{
		public int GroupId { get; set; }
		public string GroupKey { get; set; } = default!;
		public string RelativePath { get; set; } = default!; // path of dir to create
	}
}
