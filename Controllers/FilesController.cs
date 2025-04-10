using FloatySyncServer.Data;
using FloatySyncServer.Models;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Text.RegularExpressions;

namespace FloatySyncServer.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	//TODO: Add encryption
	public class FilesController : ControllerBase
	{
		private readonly SyncDbContext _syncDbContext;
		private readonly IWebHostEnvironment _env;

		public FilesController(SyncDbContext syncDbContext, IWebHostEnvironment env)
		{
			_syncDbContext = syncDbContext;
			_env = env;
		}

		//TODO: Add conflict handling (newer file wins? return the conflict so that client can decide?)
		[HttpPost("upload")]
		public async Task<IActionResult> UploadFile(
			[FromForm] string relativePath,
			[FromForm] IFormFile file,
			[FromForm] string groupId,
			[FromForm] string groupKeyPlaintext)
		{
			if (file == null || file.Length == 0)
				return BadRequest("No file uploaded.");
			if (string.IsNullOrWhiteSpace(relativePath))
				return BadRequest("No relative path provided.");
			if (string.IsNullOrWhiteSpace(groupId))
				return BadRequest("No group id provided.");
			if (string.IsNullOrWhiteSpace(groupKeyPlaintext))
				return BadRequest("No group key provided");

			var group = _syncDbContext.Groups.Find(Convert.ToInt32(groupId));
			if (group == null)
				return NotFound("Group not found");

			string masterKeyBase64 = System.IO.File.ReadAllText(Path.Combine(_env.ContentRootPath, "key.txt"));
			string decryptedKey = Helpers.DecryptString(group.EncryptedSecretKey, masterKeyBase64);

			if (decryptedKey != groupKeyPlaintext)
			{
				return StatusCode(StatusCodes.Status403Forbidden, "Invalid group / key");
			}

			//Figure out the different paths
			string basePath = Path.Combine(_env.ContentRootPath, "SyncData");
			if (!Directory.Exists(basePath))
				Directory.CreateDirectory(basePath);

			var groupFolder = Path.Combine(basePath, groupId);
			Directory.CreateDirectory(groupFolder);

			string fullPath = Path.Combine(groupFolder, relativePath);
			if (!Directory.Exists(fullPath))
				Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

			//Saving the file
			using (var stream = new FileStream(fullPath, FileMode.Create))
			{
				await file.CopyToAsync(stream);
			}

			//Updating the Database
			var existing = Helpers.TryGetFileMetadata(relativePath, groupId, _syncDbContext);

			if (existing != null)
			{
				existing.LastModifiedUtc = DateTime.UtcNow;
				existing.StoredPathOnServer = fullPath;
				existing.Checksum = Helpers.ComputeFileChecksum(fullPath);
			}
			else
			{
				var newFile = new FileMetadata
				{
					RelativePath = relativePath,
					LastModifiedUtc = DateTime.UtcNow,
					GroupId = groupId,
					StoredPathOnServer = fullPath
				};
				_syncDbContext.Files.Add(newFile);
			}
			await _syncDbContext.SaveChangesAsync();

			return Ok("File uploaded successfully.");
		}

		[HttpPost("move")]
		public async Task<IActionResult> MoveFile(
			[FromBody] MoveFileRequest moveRequest)
		{
			if (moveRequest == null
				|| string.IsNullOrEmpty(moveRequest.OldRelativePath)
				|| string.IsNullOrEmpty(moveRequest.NewRelativePath)
				|| string.IsNullOrEmpty(moveRequest.GroupId)
				|| string.IsNullOrEmpty(moveRequest.GroupKeyPlaintext))
			{
				return BadRequest("Invalid move request");
			}

			var group = _syncDbContext.Groups.Find(Convert.ToInt32(moveRequest.GroupId));
			if (group == null)
				return NotFound("Group not found");

			string masterKeyBase64 = System.IO.File.ReadAllText(Path.Combine(_env.ContentRootPath, "key.txt"));
			string decryptedKey = Helpers.DecryptString(group.EncryptedSecretKey, masterKeyBase64);

			if (decryptedKey != moveRequest.GroupKeyPlaintext)
			{
				return StatusCode(StatusCodes.Status403Forbidden, "Invalid group / key");
			}

			var fileMetaData = Helpers.TryGetFileMetadata(moveRequest.OldRelativePath, moveRequest.GroupId, _syncDbContext);

			if (fileMetaData == null)
				return NotFound("File not found in server database");

			var oldPhysicalPath = fileMetaData.StoredPathOnServer;
			if (!System.IO.File.Exists(oldPhysicalPath))
				return NotFound("Physical file not found on server");

			var basePath = Path.Combine(_env.ContentRootPath, "SyncData", moveRequest.GroupId);
			var newFullPath = Path.Combine(basePath, moveRequest.NewRelativePath);

			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(newFullPath));

				System.IO.File.Move(oldPhysicalPath, newFullPath);

				fileMetaData.RelativePath = moveRequest.NewRelativePath;
				fileMetaData.StoredPathOnServer = newFullPath;
				fileMetaData.LastModifiedUtc = DateTime.UtcNow;

				await _syncDbContext.SaveChangesAsync();

				return Ok($"File moved to {moveRequest.NewRelativePath}");
			}
			catch (Exception ex)
			{
				return StatusCode(StatusCodes.Status500InternalServerError, $"File move error");
			}
		}

		[HttpGet("download")]
		public async Task<IActionResult> DownloadFile(
			[FromQuery] string relativePath,
			[FromQuery] string groupId,
			[FromQuery] string groupKeyPlaintext)
		{

			var group = _syncDbContext.Groups.Find(Convert.ToInt32(groupId));
			if (group == null)
				return NotFound("Group not found");

			string masterKeyBase64 = System.IO.File.ReadAllText(Path.Combine(_env.ContentRootPath, "key.txt"));
			string decryptedKey = Helpers.DecryptString(group.EncryptedSecretKey, masterKeyBase64);

			if (decryptedKey != groupKeyPlaintext)
			{
				return StatusCode(StatusCodes.Status403Forbidden, "Invalid group / key");
			}

			var fileMetaData = Helpers.TryGetFileMetadata(relativePath, groupId, _syncDbContext);

			if (fileMetaData == null)
				return NotFound("File not found on server.");

			var filePath = fileMetaData.StoredPathOnServer;
			if (filePath == null || !System.IO.File.Exists(filePath))
				return NotFound("Server path not found or file missing.");

			var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);

			//Return a FileStreamResult
			return File(fileStream, "application/octet-stream", Path.GetFileName(filePath));
		}

		[HttpDelete("delete")]
		public async Task<IActionResult> DeleteFile(
			[FromQuery] string relativePath,
			[FromQuery] string groupId,
			[FromQuery] string groupKeyPlaintext)
		{
			var group = _syncDbContext.Groups.Find(Convert.ToInt32(groupId));
			if (group == null)
				return NotFound("Group not found");

			string masterKeyBase64 = System.IO.File.ReadAllText(Path.Combine(_env.ContentRootPath, "key.txt"));
			string decryptedKey = Helpers.DecryptString(group.EncryptedSecretKey, masterKeyBase64);

			if (decryptedKey != groupKeyPlaintext)
			{
				return StatusCode(StatusCodes.Status403Forbidden, "Invalid group / key");
			}

			var fileMetaData = Helpers.TryGetFileMetadata(relativePath, groupId, _syncDbContext);

			if (fileMetaData == null)
				return NotFound("File not found on server database");

			if (fileMetaData.StoredPathOnServer == null || !System.IO.File.Exists(fileMetaData.StoredPathOnServer))
			{
				return NotFound("File not found on server disk");
			}
			else
			{
				try
				{
					System.IO.File.Delete(fileMetaData.StoredPathOnServer);
					fileMetaData.IsDeleted = true;
					fileMetaData.LastModifiedUtc = DateTime.UtcNow;
				}
				catch (Exception ex)
				{
					return StatusCode(StatusCodes.Status500InternalServerError, $"Error deleting file from disk");
				}
			}

			await _syncDbContext.SaveChangesAsync();

			return Ok("File removed successfully.");
		}

		[HttpGet("changes")]
		public IActionResult GetChanges(
			[FromQuery] string groupId,
			[FromQuery] string groupKeyPlaintext,
			[FromQuery] DateTime lastSyncUtc)
		{
			var group = _syncDbContext.Groups.Find(Convert.ToInt32(groupId));
			if (group == null)
				return NotFound("Group not found");

			string masterKeyBase64 = System.IO.File.ReadAllText(Path.Combine(_env.ContentRootPath, "key.txt"));
			string decryptedKey = Helpers.DecryptString(group.EncryptedSecretKey, masterKeyBase64);

			if (decryptedKey != groupKeyPlaintext)
			{
				return StatusCode(StatusCodes.Status403Forbidden, "Invalid group / key");
			}

			var changedFiles = _syncDbContext.Files
				.Where(f => f.GroupId == groupId && f.LastModifiedUtc > lastSyncUtc)
				.Select(f => new FileChangeInfo
				{
					FileId = f.Id,
					RelativePath = f.RelativePath,
					IsDeleted = f.IsDeleted,
					LastModifiedUtc = f.LastModifiedUtc
				})
				.ToList();

			return Ok(changedFiles);
		}
	}
}
