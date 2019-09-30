﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ranger.Logging;
using Ranger.Services.Templates.Data;

namespace Ranger.Services.Templates {
    public class Program {
        public static void Main (string[] args) {
            var config = new ConfigurationBuilder ()
                .SetBasePath (Directory.GetCurrentDirectory ())
                .AddJsonFile ("appsettings.json", optional : false, reloadOnChange : true)
                .AddJsonFile ($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional : false, reloadOnChange : true)
                .AddEnvironmentVariables ()
                .Build ();

            var host = CreateWebHostBuilder (config["serverBindingUrl"], args).Build ();

            using (var scope = host.Services.CreateScope ()) {
                var dbInitializer = scope.ServiceProvider.GetRequiredService<ITemplatesDbContextInitializer> ();
                var env = scope.ServiceProvider.GetRequiredService<IHostingEnvironment> ();

                dbInitializer.Migrate ();
                dbInitializer.EnsureRowLevelSecurityApplied ();
            }
            host.Run ();
        }

        public static IWebHostBuilder CreateWebHostBuilder (string serverBindingUrl, string[] args) =>
            WebHost.CreateDefaultBuilder (args)
            .UseUrls (serverBindingUrl)
            .UseLogging ()
            .UseStartup<Startup> ()
            .ConfigureServices (services => services.AddAutofac ());
    }
}