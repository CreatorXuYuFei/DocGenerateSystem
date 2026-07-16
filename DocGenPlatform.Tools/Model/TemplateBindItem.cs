using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocGenPlatform.Tools.Model
{
    /// <summary>
    /// 模板ID与分类绑定配置项
    /// </summary>
    public class TemplateBindItem
    {
        /// <summary>
        /// 模板唯一ID
        /// </summary>
        public string TemplateId { get; set; } = string.Empty;

        /// <summary>
        /// 模板分类标识（用于向量库匹配）
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// 模板名称（备注）
        /// </summary>
        public string TemplateName { get; set; } = string.Empty;
    }

    /// <summary>
    /// 模板绑定配置根节点
    /// </summary>
    public class TemplateBindRoot
    {
        public List<TemplateBindItem> TemplateBindConfig { get; set; } = new();
    }

}
