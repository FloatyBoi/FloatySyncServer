using Microsoft.EntityFrameworkCore;
using FloatySyncServer.Models;

namespace FloatySyncServer.Data
{
	public class SyncDbContext : DbContext
	{
		public SyncDbContext(DbContextOptions<SyncDbContext> options)
			: base(options) { }

		//TODO: Add group information ("key, name, id")
		public DbSet<FileMetadata>? Files { get; set; }
		public DbSet<Group> Groups { get; set; }
		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);
		}
	}
}
