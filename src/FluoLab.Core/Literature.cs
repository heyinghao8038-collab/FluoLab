using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace FluoLab.Core
{
    public sealed class LiteraturePage
    {
        public int Number { get; set; }
        public string OriginalText { get; set; }
        public string ChineseText { get; set; }
        public string Status { get; set; }
        public bool NumericReview { get; set; }
    }
    public sealed class LiteratureDocument
    {
        public string FileName { get; set; }
        public string SourceSha256 { get; set; }
        public string Engine { get; set; }
        public string Model { get; set; }
        public string UpdatedAt { get; set; }
        public List<LiteraturePage> Pages { get; set; }
    }
    public static class LiteratureReport
    {
        public const string ReviewWarning = "机器译文仅辅助阅读。请对照原文核对专业术语、否定关系、数字、单位、表格和公式，不作为实验或医疗决策依据。";
        public static string Html(LiteratureDocument document)
        {
            var output = new StringBuilder("<!doctype html><html lang=\"zh-CN\"><meta charset=\"utf-8\"><title>FluoLab 中英对照阅读</title><style>body{font:16px/1.7 'Microsoft YaHei',sans-serif;max-width:1100px;margin:32px auto;padding:20px;color:#1d1d1f}pre{white-space:pre-wrap;overflow-wrap:anywhere;background:#f5f5f7;padding:16px}section{border-top:1px solid #ddd;margin-top:28px}aside{padding:16px;background:#fff5df}.pair{display:grid;grid-template-columns:1fr 1fr;gap:20px}@media(max-width:800px){.pair{display:block}}</style><body><h1>文献中英对照</h1>");
            output.Append("<p>源文件：").Append(E(document.FileName)).Append("<br>SHA-256：").Append(E(document.SourceSha256)).Append("<br>引擎：").Append(E(document.Engine)).Append("<br>模型：").Append(E(document.Model)).Append("<br>更新时间：").Append(E(document.UpdatedAt)).Append("</p><aside>").Append(ReviewWarning).Append(" 原版式未改写；多栏、表格与扫描件可能不能正确提取。</aside>");
            foreach (var page in document.Pages)
            {
                output.Append("<section id=\"page-").Append(page.Number).Append("\"><h2>第 ").Append(page.Number).Append(" 页 · ").Append(E(page.Status)).Append("</h2>");
                if (page.NumericReview) output.Append("<aside>数字标记与原文不一致，请逐项核对。未自动修正。</aside>");
                output.Append("<div class=\"pair\"><div><h3>原文提取</h3><pre>").Append(E(page.OriginalText)).Append("</pre></div><div><h3>机器中文译文</h3><pre>").Append(E(string.IsNullOrWhiteSpace(page.ChineseText) ? "未翻译或无可提取文本" : page.ChineseText)).Append("</pre></div></div></section>");
            }
            return output.Append("</body></html>").ToString();
        }
        public static string Text(LiteratureDocument document)
        {
            var output = new StringBuilder("FluoLab 中英对照阅读\r\n源文件：" + document.FileName + "\r\nSHA-256：" + document.SourceSha256 + "\r\n引擎：" + document.Engine + "\r\n模型：" + document.Model + "\r\n更新时间：" + document.UpdatedAt + "\r\n" + ReviewWarning + "\r\n");
            foreach (var page in document.Pages)
                output.Append("\r\n===== 第 ").Append(page.Number).Append(" 页 · ").Append(page.Status).Append(" =====\r\n").Append(page.NumericReview ? "数字标记需对照核对，未自动修正。\r\n" : "").Append("[原文]\r\n").Append(page.OriginalText).Append("\r\n[机器译文]\r\n").Append(string.IsNullOrWhiteSpace(page.ChineseText) ? "未翻译或无可提取文本" : page.ChineseText).Append("\r\n");
            return output.ToString();
        }
        private static string E(string text) { return WebUtility.HtmlEncode(text ?? ""); }
    }
}
