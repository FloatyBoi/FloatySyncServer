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

			if (!Helpers.IsSafeRelativePath(rel))
				return BadRequest("Invalid Path");

			string phys = FullPath(dto.GroupId, rel);

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
					LastModifiedUtc = DateTime.UtcNow,
					StoredPathOnServer = phys
				});
				_syncDbContext.SaveChanges();
			}

			Directory.CreateDirectory(phys);

			return Ok();
		}

		[HttpPost("rename")]
		public IActionResult RenameDir([FromBody] DirectoryRenameDto dto)
		{
			if (!TryAuthorize(dto.GroupId, dto.GroupKey, out _))
				return Forbid();

			string oldNorm = PathNorm.Normalize(dto.OldPath);
			string newNorm = PathNorm.Normalize(dto.NewPath);

			if (!Helpers.IsSafeRelativePath(oldNorm) || !Helpers.IsSafeRelativePath(newNorm))
				return BadRequest("Invalid path");

			string oldPrefix = oldNorm + "/";
			string newPrefix = newNorm + "/";

			string oldPhys = FullPath(dto.GroupId, oldNorm);
			string newPhys = FullPath(dto.GroupId, newNorm);

			using var tx = _syncDbContext.Database.BeginTransaction();
			try
			{
				if (!Directory.Exists(oldPhys))
					return NotFound("Source directory missing");

				Directory.CreateDirectory(Path.GetDirectoryName(newPhys)!);
				Directory.Move(oldPhys, newPhys);

				AddMissingParents(dto.GroupId, newNorm);

				var rows = _syncDbContext.Files
							  .Where(f => f.GroupId == dto.GroupId.ToString() &&
										 (f.RelativePath == oldNorm ||
										  f.RelativePath.StartsWith(oldPrefix)))
							  .ToList();

				foreach (var row in rows)
				{
					string tail = row.RelativePath == oldNorm
								? string.Empty
								: row.RelativePath.Substring(oldPrefix.Length);

					string newRel = newPrefix + tail;

					row.RelativePath = newRel;
					row.StoredPathOnServer = Path.Combine(GroupRoot(dto.GroupId),
														  PathNorm.ToDisk(newRel));
					row.LastModifiedUtc = DateTime.UtcNow;
				}

				_syncDbContext.SaveChanges();
				tx.Commit();
				return Ok();
			}
			catch (Exception ex)
			{
				tx.Rollback();
				return StatusCode(500, "Internal error");
			}

			void AddMissingParents(int groupId, string leafRel)
			{
				var parts = leafRel.Split('/');
				string running = string.Empty;

				for (int i = 0; i < parts.Length - 1; i++)
				{
					running = (i == 0) ? parts[0] : $"{running}/{parts[i]}";

					if (_syncDbContext.Files.Any(f =>
							f.GroupId == groupId.ToString() &&
							f.RelativePath == running &&
							f.IsDirectory && !f.IsDeleted))
						continue;

					_syncDbContext.Files.Add(new FileMetadata
					{
						GroupId = groupId.ToString(),
						RelativePath = running,
						IsDirectory = true,
						LastModifiedUtc = DateTime.UtcNow,
						StoredPathOnServer = Path.Combine(GroupRoot(groupId),
														  PathNorm.ToDisk(running))
					});
				}
			}

			string GroupRoot(int groupId) =>
				Path.Combine(DataRoot, groupId.ToString());
		}

		[HttpDelete]
		public IActionResult DeleteDir([FromQuery] int groupId,
							   [FromQuery] string groupKeyPlaintext,
							   [FromQuery] string relativePath,
							   [FromQuery] bool hard = true) // Optional flag if you dont want to delete the files
		{
			if (!TryAuthorize(groupId, groupKeyPlaintext, out var _)) return Forbid();

			string rel = PathNorm.Normalize(relativePath);

			if (!Helpers.IsSafeRelativePath(rel))
				return BadRequest("Invalid Path");

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
