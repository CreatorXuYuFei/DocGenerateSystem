using DocGenPlatform.Tools.Model;
using System.Text.Json;

namespace DocGenPlatform.Tools
{
    public static class TemplateBindConfigHelper
    {
        private static readonly string _configPath = Path.Combine(AppContext.BaseDirectory, "Template", "template-bind.json");
        private static readonly JsonSerializerOptions _jsonOpt = new() { WriteIndented = true };

        /// <summary>
        /// 读取全部模板绑定关系
        /// </summary>
        public static List<TemplateBindItem> ReadAllBind()
        {
            if (!File.Exists(_configPath))
                return new List<TemplateBindItem>();

            string json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<List<TemplateBindItem>>(json, _jsonOpt) ?? [];
        }

        /// <summary>
        /// 保存/更新绑定关系（新增/修改）
        /// </summary>
        public static void SaveBind(List<TemplateBindItem> bindList)
        {
            string dir = Path.GetDirectoryName(_configPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string json = JsonSerializer.Serialize(bindList, _jsonOpt);
            File.WriteAllText(_configPath, json);
        }

        /// <summary>
        /// 根据模板ID更新/新增绑定
        /// </summary>
        public static void UpsertBind(TemplateBindItem item)
        {
            var list = ReadAllBind();
            var exist = list.FirstOrDefault(x => x.TemplateId == item.TemplateId);
            if (exist != null)
            {
                exist.Category = item.Category;
                exist.TemplateName = item.TemplateName;
            }
            else
            {
                list.Add(item);
            }
            SaveBind(list);
        }

        /// <summary>
        /// 删除指定模板绑定
        /// </summary>
        public static void RemoveBind(string templateId)
        {
            var list = ReadAllBind();
            list.RemoveAll(x => x.TemplateId == templateId);
            SaveBind(list);
        }
    }
}
