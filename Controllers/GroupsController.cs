using FloatySyncServer.Data;
using FloatySyncServer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloatySyncServer.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class GroupsController : ControllerBase
	{
		private readonly SyncDbContext _syncDbContext;
		private readonly IWebHostEnvironment _env;

		public GroupsController(SyncDbContext syncDbContext, IWebHostEnvironment env)
		{
			_syncDbContext = syncDbContext;
			_env = env;
		}

		[HttpPost]
		public IActionResult CreateGroup([FromBody] CreateGroupRequest request)
		{
			if (string.IsNullOrWhiteSpace(request.Name) ||
				string.IsNullOrWhiteSpace(request.SecretKey))
			{
				return BadRequest("Name and SecretKey are required.");
			}

			string masterKey = System.IO.File.ReadAllText(Path.Combine(_env.ContentRootPath, "key.txt"));

			string encryptedKey = Helpers.EncryptString(request.SecretKey, masterKey);

			var group = new Group
			{
				Name = request.Name,
				EncryptedSecretKey = encryptedKey
			};

			_syncDbContext.Groups.Add(group);
			_syncDbContext.SaveChanges();

			return Ok(new { GroupId = group.Id });
		}

		[HttpGet("{id}")]
		public IActionResult GetGroup(int id)
		{
			var group = _syncDbContext.Groups.Find(id);
			if (group == null)
			{
				return NotFound();
			}
			return Ok(new { group.Id, group.Name });
		}
	}

	public class CreateGroupRequest
	{
		public string Name { get; set; } = default!;
		public string SecretKey { get; set; } = default!;
	}
}
