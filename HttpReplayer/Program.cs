using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace HttpReplayer
{
    class ReplayRequest
    {
        private readonly string courseName;
        private readonly string url;
        private readonly string content;

        public ReplayRequest(string courseName, string url, string content)
        {
            this.courseName = courseName;
            this.url = url;
            this.content = content;
        }

        private void Log(string log)
        {
            Console.WriteLine($"{DateTime.Now} {courseName,-15} {log}");
        }

        public HttpRequestMessage BuildRequest()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Referrer = new Uri(url);
            request.Content = new StringContent(content);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");
            return request;
        }

        public async Task<bool>  ShouldContinue(HttpResponseMessage response)
        {
            var resText = await response.Content.ReadAsStringAsync();
            using (var streamReader = new StringReader(resText))
            {
                var firstLine = await streamReader.ReadLineAsync();
                Log(firstLine);
                return !firstLine.Contains("成功");
            }
        }

        public bool HandleException(Exception e)
        {
            Log($"{e.GetType()} {e.Message}");
            return true;
        }
    }

    class Replayer
    {
        private readonly HttpClient httpClient = new HttpClient();
        private readonly ReplayRequest replayRequest;

        public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(1);

        public Replayer(ReplayRequest replayRequest)
        {
            this.replayRequest = replayRequest;
            httpClient.Timeout = TimeSpan.FromMinutes(10);
        }
        public async Task ReplayAsync()
        {
            while(true)
            {
                try
                {
                    var result = await httpClient.SendAsync(replayRequest.BuildRequest());
                    if (!await replayRequest.ShouldContinue(result))
                    {
                        break;
                    }
                }
                catch (Exception e)
                {
                    if (!replayRequest.HandleException(e))
                    {
                        throw;
                    }
                }
                
                await Task.Delay(Delay);
            }
            
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var json = await File.ReadAllTextAsync("courses.json");
            var obj = JsonConvert.DeserializeObject(json) as JArray;

            List<Task> tasks = new List<Task>();
            foreach (var c in obj)
            {
                var request = new ReplayRequest(c.Value<string>("name"), c.Value<string>("url"), c.Value<string>("content"));
                var replayer = new Replayer(request);
                tasks.Add(replayer.ReplayAsync());
            }

            await Task.WhenAll(tasks);

            Console.WriteLine("Finished");
            Console.ReadLine();
        }
    }
}
