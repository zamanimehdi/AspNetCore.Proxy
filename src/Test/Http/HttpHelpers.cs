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

[assembly: SuppressMessage("Readability", "RCS1090", Justification = "Not a library, so no need for `ConfigureAwait`.")]

namespace AspNetCore.Proxy.Tests
{
    internal class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRouting();
            services.AddProxies();
            services.AddControllers();
            services.AddHttpClient("TimeoutClient", c => c.Timeout = TimeSpan.FromMilliseconds(0.001));
            services.AddHttpClient("BaseAddressClient", c => c.BaseAddress = new Uri("https://jsonplaceholder.typicode.com"));
            services.AddHttpClient("TargetHttpClient")
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
                {
                    UseDefaultCredentials = true
                })
                .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://localhost:4123"));
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseMiddleware<FakeIpAddressMiddleware>();
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());

            app.UseProxies(proxies =>
            {
                proxies.Map("echo/post", proxy => proxy.UseHttp("https://postman-echo.com/post"));

                proxies.Map("api/comments/contextandargstotask/{postId}", proxy => proxy.UseHttp((_, args) => new ValueTask<string>($"https://jsonplaceholder.typicode.com/comments/{args["postId"]}")));

                proxies.Map("api/comments/contextandargstostring/{postId}", proxy => proxy.UseHttp((_, args) => $"https://jsonplaceholder.typicode.com/comments/{args["postId"]}"));
            });
        }
    }

    public class FakeIpAddressMiddleware
    {
        private readonly RequestDelegate next;
            private static readonly Random rand = new Random();

        public FakeIpAddressMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            var r = rand.NextDouble();

            if(r < .33)
            {
                httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.168.1.31");
                httpContext.Connection.LocalIpAddress = IPAddress.Parse("127.168.1.32");
            }
            else if (r < .66)
            {
                httpContext.Connection.RemoteIpAddress = IPAddress.Parse("2001:db8:85a3:8d3:1319:8a2e:370:7348");
                httpContext.Connection.LocalIpAddress = IPAddress.Parse("2001:db8:85a3:8d3:1319:8a2e:370:7349");
            }

            await this.next(httpContext);
        }
    }

    public class MvcController : ControllerBase
    {

        [Route("api/posts")]
        public Task ProxyPostRequest()
        {
            return this.HttpProxyAsync("https://jsonplaceholder.typicode.com/posts");
        }

        [Route("api/multipart")]
        public Task ProxyPostMultipartRequest()
        {
            return this.HttpProxyAsync("https://httpbin.org/post");
        }

        [Route("api/catchall/{**rest}")]
        public Task ProxyCatchAll(string rest)
        {
            return this.HttpProxyAsync($"https://jsonplaceholder.typicode.com/{rest}");
        }

        [Route("api/controller/posts/{postId}")]
        public Task GetPosts(int postId)
        {
            return this.HttpProxyAsync($"https://jsonplaceholder.typicode.com/posts/{postId}");
        }

        [Route("api/controller/intercept/{postId}")]
        public Task GetWithIntercept(int postId)
        {
            var options = HttpProxyOptionsBuilder.Instance
                .WithIntercept(async c =>
                {
                    c.Response.StatusCode = 200;
                    await c.Response.WriteAsync("This was intercepted and not proxied!");

                    return true;
                })
                .Build();

            return this.HttpProxyAsync($"https://jsonplaceholder.typicode.com/posts/{postId}", options);
        }

        [Route("api/controller/customrequest/{postId}")]
        public Task GetWithCustomRequest(int postId)
        {
            var options = HttpProxyOptionsBuilder.Instance
                .WithBeforeSend((_, hrm) =>
                {
                    hrm.RequestUri = new Uri("https://jsonplaceholder.typicode.com/posts/2");
                    return Task.CompletedTask;
                })
                .WithShouldAddForwardedHeaders(false)
                .Build();

            return this.HttpProxyAsync($"https://jsonplaceholder.typicode.com/posts/{postId}", options);
        }

        [Route("api/controller/customresponse/{postId}")]
        public Task GetWithCustomResponse(int postId)
        {
            var options = HttpProxyOptionsBuilder.Instance
                .WithAfterReceive((_, hrm) =>
                {
                    var newContent = new StringContent("It's all greek...er, Latin...to me!");
                    hrm.Content = newContent;
                    return Task.CompletedTask;
                })
                .Build();

            return this.HttpProxyAsync($"https://jsonplaceholder.typicode.com/posts/{postId}", options);
        }

        [Route("api/controller/timeoutclient/{postId}")]
        public Task GetWithTimeoutClient(int postId)
        {
            var options = HttpProxyOptionsBuilder.Instance
                .WithHttpClientName("TimeoutClient")
                .Build();

            return this.HttpProxyAsync($"https://jsonplaceholder.typicode.com/posts/{postId}", options);
        }

        [Route("api/controller/baseaddressclient/{postId}")]
        public Task GetWithBaseAddressClient(int postId)
        {
            var options = HttpProxyOptionsBuilder.Instance
                .WithHttpClientName("BaseAddressClient")
                .Build();

            return this.HttpProxyAsync($"posts/{postId}", options);
        }

        [Route("api/controller/badresponse/{postId}")]
        public Task GetWithBadResponse(int postId)
        {
            var options = HttpProxyOptionsBuilder.Instance
                .WithAfterReceive((_, hrm) =>
                {
                    if(hrm.StatusCode == HttpStatusCode.NotFound)
                    {
                        var newContent = new StringContent("I tried to proxy, but I chose a bad address, and it is not found.");
                        hrm.Content = newContent;
                    }

                    return Task.CompletedTask;
                })
                .Build();

            return this.HttpProxyAsync($"https://jsonplaceholder.typicode.com/badpath/{postId}", options);
        }

        [Route("api/controller/fail/{postId}")]
        public Task GetWithGenericFail(int postId)
        {
            var options = HttpProxyOptionsBuilder.Instance
                .WithBeforeSend((_, hrm) =>
                {
                    var a = 0;
                    var b = 1 / a;
                    return Task.CompletedTask;
                })
                .Build();

            return this.HttpProxyAsync($"https://jsonplaceholder.typicode.com/posts/{postId}", options);
        }

        [Route("api/controller/customfail/{postId}")]
        public Task GetWithCustomFail(int postId)
        {
            var options = HttpProxyOptionsBuilder.Instance
                .WithBeforeSend((_, hrm) =>
                {
                    var a = 0;
                    var b = 1 / a;
                    return Task.CompletedTask;
                })
                .WithHandleFailure((c, e) =>
                {
                    c.Response.StatusCode = 403;
                    return c.Response.WriteAsync("Things borked.");
                })
                .Build();

            return this.HttpProxyAsync($"https://jsonplaceholder.typicode.com/posts/{postId}", options);
        }

        [Route("api/target/{**rest}")]
        public Task TargetProxyCatchAll(string rest)
        {
            var queryString = this.Request.QueryString.Value;
            var url = $"targetapi/{rest}{queryString}";
            return this.HttpProxyAsync(url, TargetOptions());
        }

        private HttpProxyOptions TargetOptions()
        {
            var httpOptions = HttpProxyOptionsBuilder.Instance
                .WithHttpClientName("TargetHttpClient")
                .WithAfterReceive((c, hrm) =>
                {
                    // Alter the content in  some way before sending back to client.
                    return Task.CompletedTask;
                })
                .WithHandleFailure(async (c, e) =>
                {
                    // Return a custom error response.
                    //c.Response.StatusCode = 403;
                    await c.Response.WriteAsync(e.Message);
                }).Build();

            return httpOptions;
        }

    }
}