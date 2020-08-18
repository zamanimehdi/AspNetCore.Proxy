using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using AspNetCore.Proxy;
using AspNetCore.Proxy.Options;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Hosting;

namespace AspNetCore.Proxy.Tests.TestTargetServer
{
    internal class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddAuthentication(Microsoft.AspNetCore.Authentication.Negotiate.NegotiateDefaults.AuthenticationScheme).AddNegotiate();
            services.AddRouting();
            services.AddControllers();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }
    }

    public class TestTargetServerFixture : IDisposable
    {
        private readonly IHost _targetServer;

        public TestTargetServerFixture()
        {
            _targetServer = new HostBuilder()
                .ConfigureWebHost(webHost => webHost
                    .UseStartup<TestTargetServer.Startup>()
                    .UseKestrel()
                    .UseUrls("http://localhost:8123", "https://localhost:4123"))
                .Start();
            //System.Threading.Thread.Sleep(TimeSpan.FromSeconds(300));
        }

        public void Dispose()
        {
            _targetServer.Dispose();
        }
    }

    public class MvcController : ControllerBase
    {
        [Route("targetapi/SimpleGet")]
        [HttpGet]
        public string SimpleGet()
        {
            return "SimpleGet response";
        }

        [Route("targetapi/SimplePost")]
        [HttpPost]
        public string SimplePost()
        {
            return "SimplePost response";
        }

        [Route("targetapi/GetWithData")]
        [HttpGet]
        public string GetWithData(string data)
        {
            return $"GetWithData: {data}";
        }

        [Route("targetapi/PostWithFormData")]
        [HttpPost]
        public string PostWithFormData(string data)
        {
            return $"PostWithFormData: {data}";
        }

        [Route("targetapi/PostWithJsonData")]
        [HttpPost]
        public string PostWithJsonData([FromBody]SimpleModel model)
        {
            return $"PostWithJsonData: {model.Data}";
        }

        [Route("targetapi/GetWithDataAuth")]
        [HttpGet]
        [Authorize]
        public string GetWithDataAuth(string data)
        {
            return $"GetWithDataAuth: {data}";
        }

        [Route("targetapi/PostWithJsonDataAuth")]
        [HttpPost]
        [Authorize]
        public string PostWithJsonDataAuth([FromBody] SimpleModel model)
        {
            return $"PostWithJsonDataAuth: {model.Data}";
        }

    }

    public class SimpleModel
    {
        public string Data { get; set; }
    }

}