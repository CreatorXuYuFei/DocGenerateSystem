using Newtonsoft.Json;
using RestSharp;
using System.Diagnostics;
using JsonException = Newtonsoft.Json.JsonException;

namespace DocGenPlatform.Tools
{
    ///<summary>
    ///HTTP请求类（基于RestSharp）
    ///</summary>
    public class RestSharpHelper
    {
        /* 小白使用必读
         * RestSharp 不再自行实现 HTTP 传输层，而是直接封装并复用.NET 内置的 HttpClient，默认使用 SocketsHttpHandler（.NET Core 2.1+ 及.NET 5+ 的默认 HTTP 处理器）作为底层消息处理器，负责 TCP 连接管理、请求发送 / 响应接收等核心操作
         * RestSharp 的最新版本（v112+）底层实现主要基于 .NET 原生的 HttpClient（System.Net.Http 命名空间） 及相关组件，同时针对不同.NET 版本有适配优化
         * RestSharp 的 RestRequest/RestResponse 最终会转换为.NET 原生的 HttpRequestMessage/HttpResponseMessage，并通过 HttpClient 发送，确保与.NET 生态的兼容性
         * RestSharp认证体系依赖 IAuthenticator认证逻辑（如 Basic Auth、OAuth2）通过实现 RestSharp.Authenticators.IAuthenticator 接口，在请求发送前动态修改 HttpRequestMessage 的头信息，底层仍由 HttpClient 处理认证后的请求
         * 
         * 如果需要记录跟踪http请求日志可以结合HttpMonitor类使用
         */

        #region 核心：单例客户端户端（全局唯一，保证连接复用）
        private static readonly Lazy<RestClient> _lazyClient = new(() =>
        {
            var handler = new SocketsHttpHandler
            {
                MaxConnectionsPerServer = 500,
                AllowAutoRedirect = true,
                PooledConnectionLifetime = TimeSpan.FromMinutes(15) //解决DNS不更新
            };

            var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(120) }; //设置全局最大超时时间120秒两分钟
            return new RestClient(httpClient); //RestSharp 复用这个 HttpClient
        });

        private static RestClient Client => _lazyClient.Value;
        #endregion

        #region 同步接口请求
        ///<summary>
        ///同步接口请求
        ///</summary>
        ///<param name="httpUrl">接口地址</param>
        ///<param name="method">请求方式</param>
        ///<param name="timeOut">超时时间</param>
        ///<param name="body">请求体</param>
        ///<param name="headers">请求头附加</param>
        ///<param name="Parameter">如果表单上传文件，仅支持图片jpg</param>
        ///<returns>低频请求使用，返回响应信息结构体</returns>
        public static RestResponse SendRequest(string httpUrl, Method method, int timeOut, Object? body = null, Dictionary<string, string>? headers = null, Dictionary<string, string>? Parameter = null)
        {
            var request = new RestRequest(httpUrl);
            request = GetRequest(method, timeOut, request, body, headers, Parameter);
            var response = Client.Execute(request);
            return response;
        }
        #endregion

        #region 异步接口请求
        ///<summary>
        ///异步接口请求
        ///</summary>
        ///<param name="httpUrl">接口地址</param>
        ///<param name="method">请求方式</param>
        ///<param name="timeOut">超时时间</param>
        ///<param name="body">请求体</param>
        ///<param name="headers">请求头附加</param>
        ///<param name="Parameter">如果表单上传文件，仅支持图片jpg</param>
        ///<returns>高频接口调用使用，返回响应信息结构体</returns>
        
        public static async Task<RestResponse> SendTaskRequest(string httpUrl, Method method, int timeOut, Object? body = null, Dictionary<string, string>? headers = null, Dictionary<string, string>? Parameter = null)
        {
            var request = new RestRequest(httpUrl);
            request = GetRequest(method, timeOut, request, body, headers, Parameter);
            //高频请求场景下，用 .ConfigureAwait(false) 减少线程切换
            var response = await Client.ExecuteAsync(request).ConfigureAwait(false);
            return response;
        }
        #endregion

        #region 通用泛型解析接口请求返回信息
        ///<summary>
        ///同步请求并解析为指定类型
        ///</summary>
        ///<typeparam name="T">返回数据类型</typeparam>
        ///<param name="httpUrl">接口地址</param>
        ///<param name="method">请求方式</param>
        ///<param name="timeOut">超时时间</param>
        ///<param name="body">请求体</param>
        ///<param name="headers">请求头</param>
        ///<param name="Parameter">表单参数（含文件）</param>
        ///<returns>解析后的实体对象</returns>
        public static T SendRequest<T>(string httpUrl, Method method, int timeOut, Object? body = null, Dictionary<string, string>? headers = null, Dictionary<string, string>? Parameter = null)
        {
            try
            {
                //执行请求
                var response = SendRequest(httpUrl, method, timeOut, body, headers, Parameter);

                //解析结果并返回
                return ParseResponse<T>(response);
            }
            catch
            {
                throw;
            }
        }

        ///<summary>
        ///异步请求并解析为指定类型
        ///</summary>
        ///<typeparam name="T">返回数据类型</typeparam>
        ///<param name="httpUrl">接口地址</param>
        ///<param name="method">请求方式</param>
        ///<param name="timeOut">超时时间</param>
        ///<param name="body">请求体</param>
        ///<param name="headers">请求头</param>
        ///<param name="Parameter">表单参数（含文件）</param>
        ///<returns>解析后的实体对象</returns>
        public static async Task<T> SendTaskRequest<T>(string httpUrl, Method method, int timeOut, Object? body = null, Dictionary<string, string>? headers = null, Dictionary<string, string>? Parameter = null)
        {
            try
            {
                //执行请求
                var response = await SendTaskRequest(httpUrl, method, timeOut, body, headers, Parameter);
                //解析结果并返回
                return ParseResponse<T>(response);
            }
            catch
            {
                throw;
            }
        }

        ///<summary>
        ///解析响应结果为指定类型
        ///</summary>
        ///<typeparam name="T">目标类型</typeparam>
        ///<param name="response">RestSharp响应对象</param>
        ///<returns>解析后的实体</returns>
        ///<exception cref="HttpRequestException">请求失败时抛出</exception>
        private static T ParseResponse<T>(RestResponse response)
        {
            //验证请求是否成功
            if (!response.IsSuccessful)
            {
                throw new HttpRequestException(
                    $"请求失败: {response.StatusCode} - {response.StatusDescription}",
                    response.ErrorException);
            }

            //处理空响应：若类型是引用类型或可空值类型，返回null；否则抛异常
            if (string.IsNullOrEmpty(response.Content))
            {
                if (typeof(T).IsClass || Nullable.GetUnderlyingType(typeof(T)) != null)
                    return Activator.CreateInstance<T>();//不能为空的类型时触发空异常
                else
                    throw new InvalidOperationException($"响应内容为空，无法转换为非可空值类型 {typeof(T).Name}");
            }

            //反序列化JSON响应
            try
            {
                var data = JsonConvert.DeserializeObject<T>(response.Content);
                if (data != null)
                    return data;
                else
                    return Activator.CreateInstance<T>();//不能为空的类型时触发空异常
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"响应解析失败: 无法将内容转换为类型 {typeof(T).Name}。原始内容: {response.Content}",
                    ex);
            }
        }
        #endregion

        #region 通用请求RestRequest
        ///<summary>
        ///通用请求RestRequest
        ///</summary>
        ///<param name="request"></param>
        ///<param name="body">请求体</param>
        ///<param name="headers">请求头附加</param>
        ///<param name="Parameter">如果表单上传文件，仅支持图片jpg</param>
        ///<returns></returns>
        ///<exception cref="InvalidOperationException"></exception>
        public static RestRequest GetRequest(Method method, int timeOut, RestRequest request, Object? body = null, Dictionary<string, string>? headers = null, Dictionary<string, string>? Parameter = null)
        {
            request.Timeout = TimeSpan.FromSeconds(timeOut);
            request.Method = method;
           
            //添加请求头,通用的
            if (headers != null)
            {
                //指定标准，防止请求失败
                if (body == null) request.AddHeader("Accept", "text/plain");
                foreach (var header in headers)
                {
                    if (header.Key == "Authenticator")
                    {
                        if (header.Value.Contains(':'))
                        {
                            var UseerMsg = header.Value.Split(':');
                            request.Authenticator = new DigestAuthHelper(UseerMsg[0], UseerMsg[1]);
                            continue;
                        }
                    }
                    request.AddHeader(header.Key, header.Value);
                }
            }
            if (body != null)
            {
                //如果是Post的Json Bod则添加指定Header防止请求失败
                request.AddHeader("Content-Type", "application/json");
                string strBody;
                //1.先判断是否为字符串类型
                if (body is string str)
                {
                    //2.验证字符串是否是 JSON
                    if (FunctionTool.IsValidJson(str))
                        strBody = body as string ?? throw new ArgumentException("传入的接口请求参数body为空字符串");
                    else
                        throw new InvalidOperationException("传入的接口请求参数body不是有效json字符串！");
                }
                else
                    strBody = JsonConvert.SerializeObject(body);
                request.AddParameter("application/json", strBody, ParameterType.RequestBody);
            }
            if (Parameter != null)
            {
                //强制multipart/form-data格式（关键）
                request.AlwaysMultipartFormData = true;
                foreach (var parameter in Parameter)
                {
                    if (parameter.Key.Contains("file"))
                    {
                        //前置校验文件路径
                        string filePath = parameter.Value;
                        if (string.IsNullOrWhiteSpace(filePath))
                            throw new ArgumentException($"文件路径不能为空!");
                        if (!File.Exists(filePath))
                            throw new FileNotFoundException("上传文件不存在", filePath);

                        //校验文件大小
                        var fileInfo = new FileInfo(filePath);
                        if (fileInfo.Length > 10 * 1024 * 1024) //10MB限制
                            throw new InvalidOperationException("文件过大，最大支持10MB");

                        //读取文件类型
                        string ContentType = FunctionTool.GetContentTypeByExtension(fileInfo);

                        //通过回调函数提供文件流（RestSharp会在内部管理流的生命周期）
                        request.AddFile(
                            name: parameter.Key,
                            getFile: () => File.OpenRead(filePath), //回调：需要时才打开流
                            fileName: Path.GetFileName(filePath),
                            contentType: ContentType
                        );
                        continue;
                    }
                    //2.添加表单参数（明确参数类型为表单字段）
                    request.AddParameter(parameter.Key, parameter.Value, ParameterType.GetOrPost);
                }
            }
            return request;
        }
        #endregion
    }
}
