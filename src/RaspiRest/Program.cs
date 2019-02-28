using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.IO;

namespace RaspiRest
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Build().Run();
        }

        public static IWebHostBuilder BuildWebHost(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            //var useUrls = new List<string>();
            var allowedHosts = config.GetValue<string>("AllowedHosts") ?? "*";
            var value = config.GetValue<string>("HttpPort") ?? "5000";
            var httpPort = int.Parse(value);
            var httpsPort = -1;
            value = config.GetValue<string>("HttpsPort");
            if (!string.IsNullOrWhiteSpace(value))
            {
                httpsPort = int.Parse(value);
                //useUrls.Add($"https://*:{httpsPort}");
            }
            //useUrls.Add($"http://*:{httpPort}");

            return WebHost.CreateDefaultBuilder(args)
                .UseKestrel(options =>
                {
                    options.Listen(System.Net.IPAddress.Any, httpPort);
                    if (httpsPort != -1)
                    {
                        //options.Listen(System.Net.IPAddress.Any,
                        //    httpsPort,
                        //    listenOptions =>
                        //    {
                        //        listenOptions.UseHttps("jhrbjnet.pfx", "<your-password>");
                        //    }
                        //);
                    }
                })
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseConfiguration(config)
                //.UseUrls(useUrls.ToArray())
                .UseStartup<Startup>();
        }
    }
}
