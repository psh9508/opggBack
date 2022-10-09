using NolowaBackendDotNet.Extensions;
using opggBack.Models.Http;
using System.Text.Json;

namespace opggBack.Core
{
    public interface IHttpHeader
    {
        bool HasHeader(string name);
        bool RemoveHeader(string name);
        bool TryGetHeaderValue(string name, out string value);
        void AddHeader(string name, string value, bool isOverried = false);
    }

    public interface IHttpProvider<TResult, TRequest> : IHttpHeader where TResult : HttpCommunicationModelBase, new()
    {
        Task<(bool IsSuccess, TResult Body)> PostAsync(string uri, TRequest body, string contentType = "application/json");
        Task<(bool IsSuccess, TResult Body)> GetAsync(string uri);
    }

    public class HttpProvider<TResult, TRequest> : IHttpProvider<TResult, TRequest> where TResult : HttpCommunicationModelBase, new()
    {
        protected static readonly HttpClient _httpClient = new HttpClient();


        public HttpProvider()
        {
            if (_httpClient.BaseAddress == null)
            {
                _httpClient.BaseAddress = new Uri("https://localhost:5001/");
                _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            }
        }

        public bool HasHeader(string name)
        {
            return _httpClient.DefaultRequestHeaders.Contains(name);
        }

        public bool RemoveHeader(string name)
        {
            if (HasHeader(name))
            {
                _httpClient.DefaultRequestHeaders.Remove(name);
                return true;
            }

            return false;
        }

        public bool TryGetHeaderValue(string name, out string value)
        {
            value = string.Empty;

            if (HasHeader(name))
            {
                value = _httpClient.DefaultRequestHeaders.GetValues(name).Single();
                return true;
            }

            return false;
        }

        public void AddHeader(string name, string value, bool isOverried = false)
        {
            if (isOverried)
            {
                if (_httpClient.DefaultRequestHeaders.Contains(name))
                    _httpClient.DefaultRequestHeaders.Remove(name);

                _httpClient.DefaultRequestHeaders.Add(name, value);
            }
            else
            {
                if (_httpClient.DefaultRequestHeaders.Contains(name))
                    return;

                _httpClient.DefaultRequestHeaders.Add(name, value);
            }
        }

        public async Task<(bool IsSuccess, TResult Body)> PostAsync(string uri, TRequest body)
        {
            return await DoPostBodyAsync(async () =>
            {
                var debugBody = JsonSerializer.Serialize(body);

                return await _httpClient.PostAsJsonAsync(uri, body);
            });
        }

        private async Task<(bool IsSuccess, TResult Body)> PostWithJsonEncoding(string uri, TRequest body) 
        {
            return await DoPostBodyAsync(async () =>
            {
                var debug = JsonSerializer.Serialize(body);

                return await _httpClient.PostAsJsonAsync(uri, body);
            });
        }

        private async Task<(bool IsSuccess, TResult Body)> PostWithURLEncoding(string uri, TRequest body)
        {
            return await DoPostBodyAsync(async () =>
            {
                var values = body.ToDictionary();

                using (var content = new FormUrlEncodedContent(values))
                {
                    content.Headers.Clear();
                    content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");

                    return await _httpClient.PostAsync(uri, content);
                }
            });
        }

        private async Task<(bool IsSuccess, TResult Body)> DoPostBodyAsync(Func<Task<HttpResponseMessage>> postAsync)
        {
            try
            {
                var response = await postAsync();

                var debug = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode == false)
                    return (false, default(TResult));

                return (true, await response.Content.ReadFromJsonAsync<TResult>());
            }
            catch(Exception ex) when (ex is NotSupportedException  // When content type is not valid
                                      || ex is JsonException       // Invalid JSON
                                     )         
            {
                //return (false, default(TResult));
                return (false, new TResult()
                {
                    Exception = ex,
                });
            }
        }

        public async Task<(bool IsSuccess, TResult Body)> GetAsync(string uri)
        {
            try
            {
                var result = await _httpClient.GetAsync(uri);

                var debug = await result.Content.ReadAsStringAsync();

                return (true, await result.Content.ReadFromJsonAsync<TResult>());
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<ValueTuple<bool, TResult>> PostAsync(string uri, TRequest body, string contentType = "application/json")
        {
            if (contentType == "application/x-www-form-urlencoded")
            {
                return await PostWithURLEncoding(uri, body);
            }
            else
            {
                return await PostWithJsonEncoding(uri, body);
            }
        }
    }
}
