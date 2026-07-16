using DocGenPlatform.Tools.Model;
using Newtonsoft.Json;
using RestSharp;
using System.Text.Json;

namespace DocGenPlatform.Tools
{
    public class FunctionTool
    {
        #region 上传图片到公司图片存储服务器
        public static string SendImgByCompany(string path)
        {
            var url = "http://111.10.204.248:18012/upload";
            Dictionary<string, string> valuePairs = GetDic("file", path);
            RestResponse response = RestSharpHelper.SendRequest(url, Method.Post, 10, null, null, valuePairs);
            if (response.IsSuccessful)
            {
                var itme = JsonConvert.DeserializeObject<ImgRoot>(response.Content ?? "");
                return itme.data.url;
            }

            throw new Exception($"上传文件失败，err:{response.ErrorMessage}, StatusCode：{response.StatusCode}");
        }
        #endregion

        #region 字典构造
        public static Dictionary<string, string> GetDic(string key, string value)
        {
            return new Dictionary<string, string> { { key, value } };
        }
        #endregion

        #region 获取文件的类型
        public static string GetContentTypeByExtension(FileInfo fileInfo)
        {
            string ext = fileInfo.Extension.ToLower();

            //核心映射表（补充你常用的类型）
            var mimeMap = new Dictionary<string, string>
            {
                [".wav"] = "audio/wav",
                [".jpg"] = "image/jpeg",
                [".jpeg"] = "image/jpeg",
                [".png"] = "image/png",
                [".mp3"] = "audio/mpeg",
                [".json"] = "application/json",
                [".txt"] = "text/plain"
            };

            //找不到则返回通用二进制类型
            return mimeMap.TryGetValue(ext, out var contentType)
                ? contentType
                : "application/octet-stream";
        }
        #endregion

        #region 判断字符串是否为有效 JSON
        ///<summary>
        ///判断字符串是否为有效 JSON
        ///</summary>
        public static bool IsValidJson(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return false;
            //去除字符串前后空格（避免空格导致解析失败）
            str = str.Trim();
            //JSON 必须以 { } 或 [ ] 开头结尾（最基础的格式特征）
            if ((str.StartsWith('{') && str.EndsWith('}')) || (str.StartsWith('[') && str.EndsWith(']')))
            {
                try
                {
                    //尝试解析，成功则为有效 JSON
                    using JsonDocument doc = JsonDocument.Parse(str);
                    return true;
                }
                catch (System.Text.Json.JsonException)
                {
                    return false;
                }
            }
            return false;
        }
        #endregion
    }
}
