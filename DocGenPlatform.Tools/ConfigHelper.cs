using Enterprise.Configuration.Abstractions;
using Enterprise.Configuration.Core;
using Enterprise.Configuration.Exceptions;
using Enterprise.Configuration.Extensions;

namespace DocGenPlatform.Tools
{
    public class ConfigHelper
    {
        // 全局唯一配置根（启动时赋值一次）
        private static IConfigRoot? _configRoot;

        /// <summary>启动时初始化（仅赋值，不做任何绑定）</summary>
        public static void Init(IConfigRoot configRoot)
        {
            _configRoot = configRoot ?? throw new ArgumentNullException(nameof(configRoot));
        }

        #region 通用读取方法（被动触发解析）
        /// <summary>读取单个实体</summary>
        public static T GetConfig<T>(string sectionPath) where T : class, new()
        {
            if (_configRoot == null) LoadAppSetting();
            return _configRoot?.GetSection(sectionPath ?? "").BindModel<T>() ?? new T();
        }

        /// <summary>读取集合 List<T></summary>
        public static List<T> GetConfigList<T>(string sectionPath) where T : class, new()
        {
            if (_configRoot == null) LoadAppSetting();
            return _configRoot?.GetSection(sectionPath ?? "").BindModel<List<T>>() ?? [];
        }

        /// <summary>直接读取原始字符串配置</summary>
        public static string? GetAppSettingValue(string key)
        {
            if (_configRoot == null) LoadAppSetting();
            return _configRoot?[key];
        }

        /// <summary>手动强制重载全量配置（按需调用）</summary>
        public static void ReloadAll()
        {
            if (_configRoot == null) LoadAppSetting();
            _configRoot?.ReloadAll();
        }
        #endregion

        #region 本地配置信息缓存
        public static void LoadAppSetting(string FilePath = "")
        {
            //读取程序根目录，默认在根目录下查找
            string baseFile = "appsettings.json";
            FilePath = FilePath == "" ? baseFile : FilePath;
            var _configuration = new ConfigBuilder()
                .AddJsonFile(FilePath, optional: false, reloadOnChange: true)   //基础Json
                .Build();
            // 初始化静态工具（启动阶段仅赋值，无绑定）
            Init(_configuration);
        }
        #endregion
    }
}
