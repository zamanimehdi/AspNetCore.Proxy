using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace AspNetCore.Proxy.Tests
{
    public class HttpIntegrationAuthTests : IClassFixture<TestTargetServer.TestTargetServerFixture>
    {
        private readonly TestServer _server;
        private readonly HttpClient _client;

        public HttpIntegrationAuthTests()
        {
            _server = new TestServer(new WebHostBuilder().UseStartup<Startup>());
            _client = _server.CreateClient();
        }

        [Fact]
        public async Task CanProxyToTargetSimpleGet()
        {
            var response = await _client.GetAsync("api/target/SimpleGet");
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            Assert.Equal("SimpleGet response", responseString);
        }

        [Fact]
        public async Task CanProxyToTargetGetWithData()
        {
            var data = Guid.NewGuid().ToString();
            var response = await _client.GetAsync($"api/target/GetWithData?data={data}");
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            Assert.Equal($"GetWithData: {data}", responseString);
        }

        [Fact]
        public async Task CanProxyToTargetSimplePost()
        {
            var response = await _client.PostAsync("api/target/SimplePost", null);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            Assert.Equal("SimplePost response", responseString);
        }

        [Fact]
        public async Task CanProxyToTargetPostWithFormData()
        {
            var data = Guid.NewGuid().ToString();
            var content = new FormUrlEncodedContent(new Dictionary<string, string> { { "data", data } });
            var response = await _client.PostAsync("api/target/PostWithFormData", content);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            Assert.Equal($"PostWithFormData: {data}", responseString);
        }

        [Fact]
        public async Task CanProxyToTargetPostWithJsonData()
        {
            var data = Guid.NewGuid().ToString();
            var content = new StringContent($"{{\"Data\": \"{data}\"}}", Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("api/target/PostWithJsonData", content);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            Assert.Equal($"PostWithJsonData: {data}", responseString);
        }

        [Fact]
        public async Task CanProxyToTargetGetWithDataAndAuth()
        {
            var data = Guid.NewGuid().ToString();
            var response = await _client.GetAsync($"api/target/GetWithDataAuth?data={data}");
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            Assert.Equal($"GetWithDataAuth: {data}", responseString);
        }

        [Fact]
        public async Task CanProxyToTargetPostWithJsonDataAndAuth()
        {
            var data = Guid.NewGuid().ToString();
            var content = new StringContent($"{{\"Data\": \"{data}\"}}", Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("api/target/PostWithJsonDataAuth", content);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            Assert.Equal($"PostWithJsonDataAuth: {data}", responseString);
        }

    }
}
