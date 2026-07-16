using RestSharp;
using RestSharp.Authenticators;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DocGenPlatform.Tools
{
    ///<summary>
    ///宇视摘要认证自定义实现
    ///</summary>
    public class DigestAuthHelper(string username, string password) : IAuthenticator
    {
        private RestClient _restClient = new();
        private readonly string _username = username;
        private readonly string _password = password;

        private string? _realm;
        private string? _nonce;
        private string? _qop;
        private string? _opaque;

        private int _nc = 0;
        private string _cnonce = string.Empty;

        public async ValueTask Authenticate(IRestClient client, RestRequest request, CancellationToken cancellationToken = default)
        {
            _cnonce = Guid.NewGuid().ToString("N");
            if (client.Options.BaseUrl == null) throw new Exception("baseUrl不能为空！");

            if (_restClient == null)
            {
                var newOptions = new RestClientOptions(client.Options.BaseUrl!)
                {
                    Proxy = client.Options.Proxy,//继承代理设置
                    Timeout = TimeSpan.FromSeconds(15),//15 秒超时
                    ThrowOnAnyError = client.Options.ThrowOnAnyError,//继承错误处理设置(控制非2xx是否自动抛异常)
                    FollowRedirects = false//禁止重定向，避免干扰 401 响应
                };
                _restClient = new RestClient(newOptions);
            }

            var tempReq = new RestRequest(request.Resource, request.Method);
            var tempResp = await _restClient.ExecuteAsync(tempReq);
            if (tempResp.StatusCode != HttpStatusCode.Unauthorized)
                return;

            ParseDigestHeader(tempResp);
            var uri = request.Resource;
            var ha1 = MD5Hash($"{_username}:{_realm}:{_password}");
            var ha2 = MD5Hash($"{request.Method.ToString().ToUpper()}:{uri}");
            int ncValue = Interlocked.Increment(ref _nc);
            var ncString = ncValue.ToString("x8");
            var response = MD5Hash($"{ha1}:{_nonce}:{ncString}:{_cnonce}:{_qop}:{ha2}");

            string headerValue = $"Digest username=\"{_username}\", realm=\"{_realm}\", nonce=\"{_nonce}\", uri=\"{uri}\", qop={_qop}, nc={ncString}, cnonce=\"{_cnonce}\", response=\"{response}\", opaque=\"{_opaque}\"";
            request.AddOrUpdateHeader("Authorization", headerValue);
        }

        private void ParseDigestHeader(RestResponse response)
        {
            var authHeader = (response.Headers?
                .FirstOrDefault(h => h.Name.Equals("WWW-Authenticate", StringComparison.OrdinalIgnoreCase))
                ?.Value?.ToString()) ?? throw new Exception("Digest 认证失败：缺少 WWW-Authenticate");

            _realm = GetDigestValue(authHeader, "realm");
            _nonce = GetDigestValue(authHeader, "nonce");
            _qop = GetDigestValue(authHeader, "qop");
            _opaque = GetDigestValue(authHeader, "opaque");

            if (string.IsNullOrEmpty(_realm) || string.IsNullOrEmpty(_nonce))
                throw new Exception("Digest 解析失败：缺少 realm 或 nonce");
        }

        private static string GetDigestValue(string header, string key)
        {
            var regex = new Regex($"{key}=\"(.*?)\"");
            var match = regex.Match(header);
            var value = match.Success ? match.Groups[1].Value : "";
            return value;
        }

        private static string MD5Hash(string input)
        {
            var raw = MD5.HashData(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder();
            foreach (var b in raw)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }
    }

}
