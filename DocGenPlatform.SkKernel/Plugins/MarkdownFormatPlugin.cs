using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace DocGenPlatform.SkKernel.Plugins;

/// <summary>Markdown 格式化校验插件</summary>
public partial class MarkdownFormatPlugin
{
    [KernelFunction("fix_markdown")]
    [Description("清洗大模型输出，提取纯净 Markdown 内容，移除代码块标记、思考过程、多余解释")]
    public string FixMarkdown([Description("模型原始输出文本")] string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;

        // 移除 markdown 代码块包裹
        var content = MarkdownCodeBlockRegex().Replace(rawText, "$1");
        // 移除前置思考/解释文本，定位第一个标题开始
        var match = FirstTitleRegex().Match(content);
        if (match.Success) content = content[match.Index..];
        // 移除尾部多余解释
        content = EndNoteRegex().Replace(content, "").Trim();
        // 规范化换行
        content = MultipleNewlineRegex().Replace(content, "\n\n");

        return content.Trim();
    }

    [GeneratedRegex(@"```markdown\s*(.*?)\s*```", RegexOptions.Singleline)]
    private static partial Regex MarkdownCodeBlockRegex();

    [GeneratedRegex(@"^# ", RegexOptions.Multiline)]
    private static partial Regex FirstTitleRegex();

    [GeneratedRegex(@"(以上|如有|仅供|注：).*$", RegexOptions.Singleline)]
    private static partial Regex EndNoteRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultipleNewlineRegex();
}