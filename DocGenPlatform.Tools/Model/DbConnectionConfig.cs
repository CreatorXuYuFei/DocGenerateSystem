using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocGenPlatform.Tools.Model
{
    ///<summary>
    ///数据库连接配置模型
    ///对应appsettings.json中connectionStrings节点下的每个子节点
    ///</summary>
    public class DbConnectionConfig
    {
        ///<summary>
        ///连接名（如dbconn、algorithm）
        ///</summary>
        public string Name { get; set; }

        ///<summary>
        ///连接字符串
        ///</summary>
        public string ConnectionString { get; set; }

        ///<summary>
        ///数据库提供者名称
        ///</summary>
        public string ProviderName { get; set; }
    }
}
