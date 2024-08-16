using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace JsonToPdf
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        private readonly IWebHostEnvironment _env;

        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
            _env = env;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            string dbPath = GetDatabasePath();

            services.AddHttpsRedirection(options =>
            {
                options.HttpsPort = 7171;
            });

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}"));

            services.AddControllers().AddNewtonsoftJson();

            services.AddScoped<DynamicJsonToPdfConverter>(sp =>
                new DynamicJsonToPdfConverter(
                    sp.GetRequiredService<AppDbContext>(),
                    Path.Combine(_env.WebRootPath, "images", "logo.png")
                ));

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "JSON to PDF API", Version = "v1" });
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime lifetime, ILogger<Startup> logger)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            EnsureDatabaseCreated(app);

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "JSON to PDF API v1");
                c.RoutePrefix = string.Empty; // Damit Swagger unter der Root-URL verfügbar ist
            });

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            lifetime.ApplicationStarted.Register(async () =>
            {
                var addresses = app.ServerFeatures.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()?.Addresses ?? new List<string>();
                foreach (var address in addresses)
                {
                    var uri = new Uri(address);
                    var port = uri.Port;
                    var dockerIp = GetDockerContainerIp();
                    var localIp = GetLocalIpAddress();
                    var externalIp = await GetExternalIp();

                    logger.LogInformation($"Application is accessible at:");
                    logger.LogInformation($"- From host: http://localhost:{port}");
                    logger.LogInformation($"- Docker internal: http://{dockerIp}:{port}");
                    logger.LogInformation($"- Local network: http://{localIp}:{port}");
                    logger.LogInformation($"- External (if configured): http://{externalIp}:{port}");
                    logger.LogInformation($"Swagger UI is available at:");
                    logger.LogInformation($"- From host: http://localhost:{port}/index.html");
                    logger.LogInformation($"- Docker internal: http://{dockerIp}:{port}/index.html");
                    logger.LogInformation($"- Local network: http://{localIp}:{port}/index.html");
                    logger.LogInformation($"- External (if configured): http://{externalIp}:{port}/index.html");
                }
            });
        }


        private void EnsureDatabaseCreated(IApplicationBuilder app)
        {
            string dbPath = GetDatabasePath();
            string directory = Path.GetDirectoryName(dbPath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            try
            {
                using var serviceScope = app.ApplicationServices.CreateScope();
                var dbContext = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Überprüfen, ob die Datenbankverbindung hergestellt werden kann
                if (dbContext.Database.CanConnect())
                {
                    Console.WriteLine("Datenbankverbindung erfolgreich hergestellt.");
                }
                else
                {
                    Console.WriteLine("Fehler beim Herstellen der Datenbankverbindung.");
                }

                // Sicherstellen, dass die Datenbank erstellt wurde
                dbContext.Database.EnsureCreated();
                Console.WriteLine("Database created successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while ensuring the database was created: {ex.Message}");
            }
        }

        private string GetDatabasePath()
        {
            string basePath = _env.IsDevelopment()
                ? Configuration["DatabasePath:Local"]
                : Configuration["DatabasePath:Docker"];

            string dbName = Configuration.GetConnectionString("DefaultConnection");

            return Path.Combine(basePath, dbName);
        }

        private string GetDockerContainerIp()
        {
            try
            {
                return Dns.GetHostEntry(Dns.GetHostName())
                    .AddressList
                    .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)?
                    .ToString() ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        private string GetLocalIpAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            return "Unable to determine local IP Address";
        }

        private async Task<string> GetExternalIp()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    return await client.GetStringAsync("https://api.ipify.org");
                }
            }
            catch (Exception ex)
            {
                return $"Unable to determine (Error: {ex.Message})";
            }
        }
    }
}