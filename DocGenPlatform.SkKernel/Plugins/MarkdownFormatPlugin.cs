using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace DocGenPlatform.SkKernel.Plugins;

/// <summary>Markdown 格式化校验插件</summary>
public partial class MarkdownFormatPlugin
{
    [KernelFunction("fix_markdown")]
    [Description("清洗大模型输出，提取纯净 Markdown 内容，移除代码块标记、思考过程、多余解释")]
    public static string FixMarkdown([Description("模型原始输出文本")] string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return string.Empty;

        string content = rawText;

        // 1. 移除 ```markdown 代码块外层标记，保留内部文档正文
        content = MarkdownCodeBlockRegex().Replace(content, "$1");

        // 2. 截取从全文第一个一级标题 # 开始的内容，无标题则完整保留原文（兜底防丢失）
        var firstTitleMatch = FullTextFirstTitleRegex().Match(content);
        if (firstTitleMatch.Success)
        {
            content = content[firstTitleMatch.Index..];
        }
        else
        {
            // 不存在一级标题，不做截断，完整保留全部文本
            content = content.Trim();
        }

        // 3. 安全移除单行尾部注释，不会跨段落删除正文内容
        content = TrailingNoteRegex().Replace(content, "");

        // 4. 统一处理连续空行，3个及以上换行替换为两段换行
        content = MultipleNewlineRegex().Replace(content, "\n\n");

        // 最终去首尾空白返回
        return content.Trim();
    }

    /// 匹配```markdown 多行代码块，提取内部内容
    [GeneratedRegex(@"```markdown\s*(.*?)\s*```", RegexOptions.Singleline)]
    private static partial Regex MarkdownCodeBlockRegex();

    /// 匹配全文第一个一级标题 # xxx，无多行模式，避免中途截断
    [GeneratedRegex(@"# .+")]
    private static partial Regex FullTextFirstTitleRegex();

    /// 匹配单行末尾备注说明，仅清除本行内容，不跨段清空
    [GeneratedRegex(@"\s*(以上|如有疑问|仅供参考|注：|说明：|补充：).*?$", RegexOptions.Multiline)]
    private static partial Regex TrailingNoteRegex();

    /// 将3个及以上连续换行统一替换为两段换行
    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultipleNewlineRegex();
}