﻿using Newtonsoft.Json;
using PuppeteerExtraSharp.Plugins.ExtraStealth.Evasions;
using PuppeteerExtraSharp.Plugins.ExtraStealth;
using PuppeteerExtraSharp;
using PuppeteerSharp;
using System.Diagnostics;
using CharacterAI.Models;
using static CharacterAI.Services.CommonService;

namespace CharacterAI.Services
{
    public class PuppeteerService
    {
        public IBrowser? Browser { get; set; }
        internal IPage? SearchPage { get; set; }
        public string BROWSER_TYPE { get; }
        public string WORKING_DIR { get; }
        public string EXEC_PATH { get; }
        internal bool IsReloading { get; set; } = true;

        private readonly bool _caiPlusMode;
        private readonly string _caiToken;
        private readonly List<int> _requestQueue;

        public PuppeteerService(bool caiPlusMode, string caiToken, string browserType, string? customBrowserDirectory, string? customBrowserExecutablePath)
        {
            _caiPlusMode = caiPlusMode;
            _caiToken = caiToken;
            var dir = string.IsNullOrEmpty(customBrowserDirectory) ? null : customBrowserDirectory;
            var exe = string.IsNullOrEmpty(customBrowserExecutablePath) ? null : customBrowserExecutablePath;
            BROWSER_TYPE = browserType;
            WORKING_DIR = dir ?? $"{CD}{SC}puppeteer-{browserType}";
            EXEC_PATH = exe ?? TryToDownloadBrowser(WORKING_DIR, BROWSER_TYPE).Result;
            _requestQueue = new();
        }

        /// <param name="killDuplicates">Kill all other browser processes launched from this folder</param>
        public async Task LaunchBrowserAsync(bool killDuplicates)
        {
            IsReloading = true;
            if (killDuplicates) KillBrowser();

            Log("\nLaunching browser... ");
            var pex = new PuppeteerExtra();
            var stealthPlugin = new StealthPlugin(new StealthHardwareConcurrencyOptions(1));

            var chromeArgs = new[]
            {
                "--no-default-browser-check", "--no-sandbox", "--disable-setuid-sandbox", "--no-first-run", "--single-process",
                "--disable-default-apps", "--disable-features=Translate", "--disable-infobars", "--disable-dev-shm-usage",
                "--mute-audio", "--ignore-certificate-errors", "--use-gl=egl",
                "--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/109.0.0.0 Safari/537.36"
            };

            Browser = await pex.Use(stealthPlugin).LaunchAsync(new()
            {
                Headless = true,
                ExecutablePath = EXEC_PATH,
                IgnoredDefaultArgs = new[] { "--disable-extensions" }, // https://github.com/puppeteer/puppeteer/blob/main/docs/troubleshooting.md#chrome-headless-doesnt-launch-on-windows
                Args = BROWSER_TYPE == "chrome" ? chromeArgs : null,
                Timeout = 1_200_000 // 15 minutes
            });

            Success("OK");

            SearchPage = await Browser.NewPageAsync();

            Log("Opening character.ai page... ");
            await TryToOpenCaiPage();

            IsReloading = false;
            Log("OK", ConsoleColor.Green);
            if (_caiPlusMode) Log($" [c.ai+ Mode Enabled]\n", ConsoleColor.Yellow);
            Log("\n");
        }

        public void KillBrowser()
        {
            IsReloading = true;
            try
            {
                var runningProcesses = Process.GetProcesses();
                foreach (var process in runningProcesses)
                {
                    bool isPuppeteerChrome = process.ProcessName == BROWSER_TYPE &&
                                             process.MainModule != null &&
                                             process.MainModule.FileName == EXEC_PATH;

                    if (isPuppeteerChrome) process.Kill();
                }

                Browser = null;
                SearchPage = null;
            }
            catch(Exception e) { Failure("Failed to kill browser.", e: e); }
        }

        internal async Task<PuppeteerResponse> RequestGetAsync(string url)
        {
            if (Browser is null)
            {
                Failure("You need to launch a browser first!");
                return new PuppeteerResponse(null, false);
            }

            var page = await Browser.NewPageAsync();
            await page.SetRequestInterceptionAsync(true);

            page.Request += (s, e) => ContinueRequest(e, null, HttpMethod.Get, "application/json");

            var response = await page.GoToAsync(url);
            var content = await response.TextAsync();
            _ = page.CloseAsync();

            return new PuppeteerResponse(content, response.Ok);
        }

        internal async Task<PuppeteerResponse> RequestPostAsync(string url, dynamic? data = null, string contentType = "application/json")
        {
            if (Browser is null)
            {
                Failure("You need to launch a browser first!");
                return new PuppeteerResponse(null, false);
            }

            var page = await Browser.NewPageAsync();
            await page.SetRequestInterceptionAsync(true);

            page.Request += (s, e) => ContinueRequest(e, data, HttpMethod.Post, contentType);

            var response = await page.GoToAsync(url);
            var content = await response.TextAsync();
            _ = page.CloseAsync();

            return new PuppeteerResponse(content, response.Ok);
        }

        internal async Task<PuppeteerResponse> RequestPostWithDownloadAsync(string url, dynamic? data = null)
        {
            if (Browser is null)
            {
                Failure("You need to launch a browser first!");
                return new PuppeteerResponse(null, false);
            }

            int? requestId = await WaitForTurnAsync();
            if (requestId is null) return new PuppeteerResponse(null, false);

            string downloadPath = $"{CD}{SC}puppeteer-temps{SC}{requestId}";
            if (Directory.Exists(downloadPath)) Directory.Delete(downloadPath, true);
            Directory.CreateDirectory(downloadPath);

            var page = await Browser.NewPageAsync();
            await page.SetRequestInterceptionAsync(true);
            await page.Client.SendAsync("Page.setDownloadBehavior", new { behavior = "allow", downloadPath });

            page.Request += (s, e) => ContinueRequest(e, data, HttpMethod.Post, "application/json");

            try { await page.GoToAsync(url); } // it will always throw an exception
            catch (NavigationException)
            {
                // "download" is a temporary file name where response content is saved
                string responsePath = $"{downloadPath}{SC}download";

                // Wait 90 seconds for the response to download
                for (int i = 0; i < 30; i++)
                {
                    await Task.Delay(3000);
                    if (System.IO.File.Exists(responsePath)) break;
                    if (i == 30) return new PuppeteerResponse(null, false);
                }

                _ = page.CloseAsync();
                _requestQueue.Remove((int)requestId);

                var content = await System.IO.File.ReadAllTextAsync(responsePath);
                try { Directory.Delete(downloadPath, recursive: true); } catch { };

                if (string.IsNullOrEmpty(content))
                    return new PuppeteerResponse(null, false);

                return new PuppeteerResponse(content, true);
            }

            return new PuppeteerResponse(null, false); // not really needed
        }

        /// <summary>
        /// Needed so Puppeteer could execute fetch() command in the browser console
        /// </summary>
        /// <returns></returns>

        /// <returns>
        /// Reloaded page.
        /// </returns>
        internal async Task TryToLeaveQueue(bool log = true)
        {
            IsReloading = true;
            if (SearchPage is null) return;

            await Task.Delay(15000);
            if (log) Log("\n15sec has passed, reloading... ");

            var response = await SearchPage.ReloadAsync();
            string content = await response.TextAsync();

            if (content.Contains("Waiting Room"))
            {
                if (log) Log(":(\nWait...");
                await TryToLeaveQueue(log);
            }
            IsReloading = false;
        }

        internal async Task<FetchResponse> FetchRequestAsync(string url, string method, dynamic? data = null, string contentType = "application/json")
        {
            if (Browser is null || SearchPage is null)
            {
                Failure("You need to launch a browser first!");
                return new FetchResponse(null);
            }

            string jsFunc = "async () => {" +
            $"  var response = await fetch('{url}', {{ " +
            $"      method: '{method}', " +
            "       headers: " +
            "       { " +
            "           'accept': 'application/json, text/plain, */*', " +
            "           'accept-encoding': 'gzip, deflate, br'," +
            $"          'authorization': 'Token {_caiToken}', " +
            $"          'content-type': '{contentType}', " +
            $"          'origin': '{url}' " +
            "       }" + (data is null ? "" :
            $"    , body: JSON.stringify({JsonConvert.SerializeObject(data)}) ") +
            "   });" +
            "   var responseStatus = response.status;" +
            "   var responseContent = await response.text();" +
            "   return JSON.stringify({ status: responseStatus, content: responseContent });" +
            "}";

            try
            {
                var response = await SearchPage.EvaluateFunctionAsync(jsFunc);
                var fetchResponse = new FetchResponse(response);
                if (fetchResponse.InQueue)
                {
                    await TryToLeaveQueue(log: false);
                    fetchResponse = await FetchRequestAsync(url, method, data, contentType);
                }

                return fetchResponse;
            }
            catch (Exception e)
            {
                Failure(e: e);
                return new FetchResponse(null);
            }
        }

        /// <returns>
        /// Chrome executable path.
        /// </returns>
        internal static async Task<string> TryToDownloadBrowser(string path, string type)
        {
            var product = type == "chrome" ? Product.Chrome : Product.Firefox;
            using var browserFetcher = new BrowserFetcher(new BrowserFetcherOptions() { Path = path, Product = product });
            var revision = await browserFetcher.GetRevisionInfoAsync();

            if (!revision.Local)
            {
                Log($"\nDownloading browser...\nPath: ");
                Log($"{path}\n", ConsoleColor.Yellow);

                if (!await browserFetcher.CanDownloadAsync(revision.Revision))
                    Failure("Error. Not available.");

                int top = Console.GetCursorPosition().Top;

                int progress = 0;
                browserFetcher.DownloadProgressChanged += (sender, args) =>
                {
                    var pp = args.ProgressPercentage;
                    if (pp <= progress) return;

                    progress = pp;

                    Console.SetCursorPosition(0, top);
                    Log($"Progress: [{new string('=', pp / 2)}{new string(' ', 50 - (pp / 2))}] ");
                    if (pp < 100) Log($"{pp}% "); else Log("100% ", ConsoleColor.Green);

                    Log($"({Math.Round(args.BytesReceived / 1024000.0f, 2)}/{Math.Round(args.TotalBytesToReceive / 1024000.0f, 2)} mb)", ConsoleColor.Yellow);
                    Log("                   ");
                };

                await browserFetcher.DownloadAsync();                
            }

            return revision.ExecutablePath;
        }

        private async Task TryToOpenCaiPage()
        {
            if (SearchPage is null) return;

            var response = await SearchPage.GoToAsync($"https://{(_caiPlusMode ? "plus" : "beta")}.character.ai/search?"); // most lightweight page
            string content = await response.TextAsync();

            if (content.Contains("Waiting Room"))
            {
                Log("\nYou are now in line. Wait... ");
                await TryToLeaveQueue();
            }
        }

        private async void ContinueRequest(RequestEventArgs args, dynamic? data, HttpMethod method, string contentType)
        {
            var r = args.Request;
            var payload = CreateRequestPayload(method, data, contentType, _caiToken);

            await r.ContinueAsync(payload);
        }

        private static Payload CreateRequestPayload(HttpMethod method, dynamic? data, string contentType, string caiToken)
        {
            var headers = new Dictionary<string, string> {
                { "authorization", $"Token {caiToken}" },
                { "accept", "application/json, text/plain, */*" },
                { "accept-encoding", "gzip, deflate, br" },
                { "content-type", contentType },
                { "user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/109.0.0.0 Safari/537.36" }
            };
            string? serializedData;
            if (data is string || data is null)
                serializedData = data;
            else
                serializedData = JsonConvert.SerializeObject(data);

            return new Payload() { Method = method, Headers = headers, PostData = serializedData };
        }

        private async Task<int?> WaitForTurnAsync()
        {
            int requestId;

            while (true)
            {
                requestId = new Random().Next(32767);
                if (!_requestQueue.Contains(requestId)) break;
            }
            _requestQueue.Add(requestId);

            for (int i = 0; i < 60; i++)
            {
                if (_requestQueue.First() == requestId) break;
                if (i == 60) return null;

                await Task.Delay(3000);
            }

            return requestId;
        }
    }
}
