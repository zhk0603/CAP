using System;
using System.Threading.Tasks;
using DotNetCore.CAP.Dashboard.NodeDiscovery;
using DotNetCore.CAP.Messages;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Sample.RabbitMQ.SqlServer
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(Configuration.GetValue<string>("DefaultConnectionString")));

            services.AddCap(x =>
            {
                x.UseEntityFramework<AppDbContext>();
                x.UseRabbitMQ("192.168.1.11");
                x.UseDashboard();
                x.FailedRetryCount = 5;
                x.FailedThresholdCallback = (type, msg) =>
                {
                    Console.WriteLine(
                        $@"A message of type {type} failed after executing {x.FailedRetryCount} several times, requiring manual troubleshooting. Message name: {msg.GetName()}");
                };

                x.UseDiscovery(_ =>
                {
                    _.DiscoveryServerHostName = "192.168.1.11";
                    _.DiscoveryServerPort = 8500;
                    _.CurrentNodeHostName = Configuration.GetValue<string>("ASPNETCORE_HOSTNAME");
                    _.CurrentNodePort = Configuration.GetValue<int>("ASPNETCORE_PORT");
                    _.NodeId = Configuration.GetValue<string>("NodeId");
                    _.NodeName = Configuration.GetValue<string>("NodeName");
                });
            });

            services.AddControllers();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();

            //app.UseHealthChecks("/health");
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            Task.Run(async () =>
            {
                var db = app.ApplicationServices.GetRequiredService<AppDbContext>();
                // await db.Database.EnsureCreatedAsync();
                Console.WriteLine("迁移数据库。"+ db.Database.GetDbConnection().ConnectionString);
                await db.Database.MigrateAsync();
            });
        }
    }
}
