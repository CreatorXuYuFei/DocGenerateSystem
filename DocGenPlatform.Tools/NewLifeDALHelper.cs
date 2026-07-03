using CoreLogger;
using DocGenPlatform.Tools.Model;
using XCode.DataAccessLayer;

namespace DocGenPlatform.Tools
{
    ///<summary>
    ///XCode数据库连接池（基于XCode.DAL和ConcurrentExpireCache实现）
    ///封装XCode框架的DAL连接管理，支持连接复用和线程安全
    ///</summary>
    public class NewLifeDALHelper
    {
        //数据库链接缓存
        private static readonly ConcurrentExpireCache<string, DAL> _connectionCache;
        //释放指定连接
        public static void Release(string dbConnName) => _connectionCache.CleanupByKey(dbConnName);
        //清空无效链接
        public static void ClearExpiredCache() => _connectionCache.CleanupExpired();
        //程序关闭时清空缓存
        public static void ClearCache() => _connectionCache.Clear();
        //使用静态构造函数，
        static NewLifeDALHelper()
        {
            //可选择程序停止前清理数据库链接
            static bool isDALExpired(DAL model) => model.Session.Disposed;
            //初始化数据库通用缓存
            _connectionCache = new ConcurrentExpireCache<string, DAL>(isDALExpired);
            //加载本地配置数据库
            LoadDB();
        }

        #region 创建数据库连接并返回链接池
        public static DAL Create(string DBConnName, string DBConnStr = "", string DbType = "")
        {
            try
            {
                //读取已有链接
                var dal = _connectionCache.Get(DBConnName, null, null, -1, false);
                if (dal.Count > 0)//存在链接则返回
                {
                    var db = dal.First();
                    if (IsDalValid(db))
                        return db;
                    else
                    {
                        //先移除再创建
                        _connectionCache.CleanupByKey(DBConnName);
                        //递归创建
                        return Create(DBConnName, DBConnStr, DbType);
                    }
                }
                else
                {
                    //加锁防止多线程重复添加连接串
                    lock (DBConnName)
                    {
                        if (DBConnStr == "")
                            GetConnectionByName(DBConnName);
                        else
                        {
                            //添加前先判断配置是否已存在
                            if (DAL.ConnStrs!=null && !DAL.ConnStrs.ContainsKey(DBConnName))
                                //添加链接信息
                                DAL.AddConnStr(DBConnName, DBConnStr, null, DbType);//DbType：MySql.Data.MySqlClient、System.Data.SqlClient、System.Data.SQLite、Oracle.DataAccess.Client、
                        }
                        //创建链接
                        DAL dAL = DAL.Create(DBConnName);
                        //写入通用字典
                        _connectionCache.Add(DBConnName, dAL);
                        return dAL;
                    }
                }
            }
            catch (Exception ex)
            {
                SysLogHelper.Error($"创建数据库链接{DBConnName}失败！链接信息：{DBConnStr}", ex, "数据库链接日志");
                throw new Exception(ex.Message);
            }
        }
        #endregion

        #region 初始化加载AppSetting数据库连接信息
        private static void LoadDB()
        {
            //读取本地数据库配置
            var config = GetConnectionStrings();
            if (config != null)
            {
                foreach (var item in config)
                {   //添加前先判断配置是否已存在
                    if (!DAL.ConnStrs.ContainsKey(item.Name))
                        //添加数据库链接信息
                        DAL.AddConnStr(item.Name, item.ConnectionString, null, item.ProviderName);
                }
            }
        }
        #endregion

        #region 根据连接名获取特定数据库连接配置
        ///<summary>
        ///根据连接名获取特定数据库连接配置
        ///</summary>
        ///<param name="connectionName">连接名（如dbconn）</param>
        ///<returns>对应的数据库连接配置</returns>
        private static void GetConnectionByName(string connectionName)
        {
            //读取appsettings配置
            var config = GetConnectionStrings();
            if (config != null)
            {
                //获取指定数据库配置
                var items = config.Where(o => o.Name == connectionName).ToList();
                if (items.Count > 0)
                {
                    //添加前先判断配置是否已存在
                    if (DAL.ConnStrs != null && !DAL.ConnStrs.ContainsKey(connectionName))
                        //添加数据库链接信息
                        DAL.AddConnStr(connectionName, items.First().ConnectionString, null, items.First().ProviderName);
                    //存在则忽略
                }
                else
                    throw new Exception($"数据库：{connectionName}未配置和写入数据库链接信息，创建数据库访问实例失败！");
            }
        }
        #endregion

        #region 获取connectionStrings节点下的所有子节点信息
        private static List<DbConnectionConfig>? GetConnectionStrings()
        {
            try
            {
                return ConfigHelper.GetConfigList<DbConnectionConfig>("ConnectionStrings");
            }
            catch (Exception ex)
            {
                SysLogHelper.Error($"加载本地connectionStrings配置失败！", ex, "数据库链接日志");
                return null;
            }
        }
        #endregion

        #region 创建数据库实例时检查链接是否有效
        private static bool IsDalValid(DAL dal)
        {
            try { dal.Execute("SELECT 1"); return true; } //执行简单SQL检测
            catch { return false; }
        }
        #endregion
    }
}
