
using FloatySyncServer.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace FloatySyncServer
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

			// Add services to the container.
			builder.Services.AddDbContext<SyncDbContext>(options =>
			{
				options.UseSqlite(builder.Configuration.GetConnectionString("SyncDatabase"));
			});
			builder.Services.AddControllers();
			// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
			builder.Services.AddOpenApi();

			var app = builder.Build();

			// Configure the HTTP request pipeline.
			if (app.Environment.IsDevelopment())
			{
				app.MapOpenApi();
			}

			app.UseHttpsRedirection();

			app.UseAuthorization();


			app.MapControllers();

			if (!File.Exists(Path.Combine(builder.Environment.ContentRootPath, "key.txt")))
			{
				byte[] keyBytes = new byte[32];
				using (var rng = RandomNumberGenerator.Create())
				{
					rng.GetBytes(keyBytes);
				}

				string newKeyBase64 = Convert.ToBase64String(keyBytes);

				File.WriteAllText(Path.Combine(builder.Environment.ContentRootPath, "key.txt"), newKeyBase64);
				Console.WriteLine("No master key found. Generated a new one. (All group keys are now invalid)");
			}
			else
				Console.WriteLine("Master key found.");
			app.Run();
		}
	}
}
