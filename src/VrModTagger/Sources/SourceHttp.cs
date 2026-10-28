using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace VrModTagger.Sources
{
    /// <summary>
    /// Shared HttpClient for all sources, with a descriptive User-Agent and simple retry.
    /// </summary>
    public static class SourceHttp
    {
        public const string UserAgent = "VrModTagger/0.1 (Playnite plugin)";

        private static readonly Lazy<HttpClient> client = new Lazy<HttpClient>(() =>
        {
            // .NET Framework 4.6.2 doesn't enable TLS 1.2 by default.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            return http;
        });

        public static HttpClient Client => client.Value;

        public static async Task<string> GetStringAsync(
            string url,
            CancellationToken cancellationToken,
            IDictionary<string, string> headers = null,
            int attempts = 3)
        {
            for (var attempt = 1; ; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    if (headers != null)
                    {
                        foreach (var header in headers)
                        {
                            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                    }

                    try
                    {
                        using (var response = await Client.SendAsync(request, cancellationToken).ConfigureAwait(false))
                        {
                            if (response.IsSuccessStatusCode)
                            {
                                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            }

                            var retryable = (int)response.StatusCode >= 500 || (int)response.StatusCode == 429;
                            if (!retryable || attempt >= attempts)
                            {
                                throw new HttpRequestException($"GET {url} returned {(int)response.StatusCode} {response.ReasonPhrase}.");
                            }
                        }
                    }
                    catch (HttpRequestException) when (attempt < attempts)
                    {
                    }
                    catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < attempts)
                    {
                        // Request timed out.
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Runs <paramref name="action"/> for every item with at most <paramref name="maxParallel"/> running at once.
        /// </summary>
        public static async Task<List<TResult>> ForEachThrottledAsync<TItem, TResult>(
            IEnumerable<TItem> items,
            int maxParallel,
            Func<TItem, Task<TResult>> action)
        {
            using (var gate = new SemaphoreSlim(maxParallel))
            {
                var tasks = items.Select(async item =>
                {
                    await gate.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        return await action(item).ConfigureAwait(false);
                    }
                    finally
                    {
                        gate.Release();
                    }
                }).ToList();
                return (await Task.WhenAll(tasks).ConfigureAwait(false)).ToList();
            }
        }
    }
}
