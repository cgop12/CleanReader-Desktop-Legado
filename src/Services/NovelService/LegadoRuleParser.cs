// Copyright (c) Richasy. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.XPath;
using CleanReader.Services.Novel.Enums;
using ModelsAttribute = CleanReader.Services.Novel.Models.Attribute;
using CleanReader.Services.Novel.Models;
using HtmlAgilityPack;
using Fizzler.Systems.HtmlAgilityPack;
using Jint;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CleanReader.Services.Novel
{
    /// <summary>
    /// Legado 书源规则解析器.
    /// </summary>
    public class LegadoRuleParser : IBookSourceEngine
    {
        private readonly Engine _jsEngine;
        private readonly JsBridge _jsBridge;
        private static readonly string DefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/97.0.4692.99 Safari/537.36 Edg/97.0.1072.69";
        private readonly HttpClient _httpClient;

        /// <summary>
        /// 初始化 <see cref="LegadoRuleParser"/> 类的新实例.
        /// </summary>
        public LegadoRuleParser()
        {
            _jsEngine = new Engine(options =>
            {
                options.AllowClr(typeof(Encoding).Assembly);
                options.AllowClr(typeof(Convert).Assembly);
                options.AllowClr(typeof(Regex).Assembly);
            });

            _jsBridge = new JsBridge();
            _jsEngine.SetValue("java", _jsBridge);

            var handler = new HttpClientHandler { UseCookies = true };
            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.8,en-US;q=0.5,en;q=0.3");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", DefaultUserAgent);
        }

        /// <summary>
        /// 向 JavaScript 引擎注入上下文变量。
        /// </summary>
        /// <param name="context">当前上下文对象（HTML 节点或 JSON 数据）。</param>
        /// <param name="source">书源配置。</param>
        /// <param name="book">书籍实例。</param>
        /// <param name="baseUrl">基础 URL。</param>
        private void InjectContextVariables(object context, BookSource source, Book book, string baseUrl)
        {
            // 注入 result 变量（上下文对象转换为字符串）
            if (context is HtmlNode htmlNode)
            {
                _jsEngine.SetValue("result", htmlNode.OuterHtml);
            }
            else if (context is JToken token)
            {
                _jsEngine.SetValue("result", token.ToString(Newtonsoft.Json.Formatting.None));
            }
            else
            {
                _jsEngine.SetValue("result", context?.ToString() ?? string.Empty);
            }

            // 注入 baseUrl 变量
            _jsEngine.SetValue("baseUrl", baseUrl ?? string.Empty);

            // 注入 source 变量（BookSource 对象序列化为 JSON 再解析）
            if (source != null)
            {
                var sourceJson = Newtonsoft.Json.JsonConvert.SerializeObject(source);
                var sourceObj = _jsEngine.Evaluate($"JSON.parse('{EscapeJsonString(sourceJson)}')");
                _jsEngine.SetValue("source", sourceObj);
            }

            // 注入 book 变量
            if (book != null)
            {
                var bookJson = Newtonsoft.Json.JsonConvert.SerializeObject(book);
                var bookObj = _jsEngine.Evaluate($"JSON.parse('{EscapeJsonString(bookJson)}')");
                _jsEngine.SetValue("book", bookObj);
            }

            // 确保 java 对象已注入
            _jsEngine.SetValue("java", _jsBridge);
        }

        /// <summary>
        /// 转义 JSON 字符串中的特殊字符，以便在 JavaScript 字符串字面量中使用。
        /// </summary>
        private string EscapeJsonString(string json)
        {
            if (string.IsNullOrEmpty(json))
                return string.Empty;

            return json.Replace("\\", "\\\\")
                       .Replace("'", "\\'")
                       .Replace("\"", "\\\"")
                       .Replace("\r", "\\r")
                       .Replace("\n", "\\n")
                       .Replace("\t", "\\t");
        }

        /// <inheritdoc/>
        public async Task<List<Book>> SearchAsync(BookSource source, string keyword, CancellationToken cancellationToken = default)
        {
            if (source.Search == null)
                throw new InvalidOperationException("书源未配置搜索模块");

            var config = source.Search;
            HtmlDocument doc;
            var encoding = string.IsNullOrEmpty(source.Charset) ? Encoding.UTF8 : Encoding.GetEncoding(source.Charset);

            if (config.Request != null)
            {
                doc = await GetHtmlDocumentAsync(config.SearchUrl, keyword, config.Request, encoding, cancellationToken);
            }
            else
            {
                var searchKeyword = config.EncodingKeyword ? HttpUtility.UrlEncode(keyword, encoding) : keyword;
                var searchUrl = config.SearchUrl + searchKeyword;
                doc = await GetHtmlDocumentAsync(searchUrl, encoding, cancellationToken);
            }

            var rangeNodes = doc.DocumentNode.QuerySelectorAll(config.Range);
            var books = new List<Book>();

            foreach (var node in rangeNodes)
            {
                var book = new Book { SourceId = source.Id };
                ParseAttributes(book, config, node);
                if (string.IsNullOrEmpty(book.BookName))
                    continue;

                FormatBook(book, config);
                ReplaceBook(book, config);
                RepairBook(book, config);
                EncodeBook(book);

                books.Add(book);
            }

            return books;
        }

        /// <inheritdoc/>
        public async Task<Book> GetBookInfoAsync(BookSource source, Book book, CancellationToken cancellationToken = default)
        {
            if (source.BookDetail == null)
                throw new InvalidOperationException("书源未配置书籍详情模块");

            var config = source.BookDetail;
            string url = DecodeBase64(book.Url);
            var encoding = string.IsNullOrEmpty(source.Charset) ? Encoding.UTF8 : Encoding.GetEncoding(source.Charset);
            var doc = await GetHtmlDocumentAsync(url, encoding, cancellationToken);
            var rangeNode = doc.DocumentNode.QuerySelector(config.Range);

            if (rangeNode != null)
            {
                ParseAttributes(book, config, rangeNode);
                FormatBook(book, config);
                ReplaceBook(book, config);
                RepairBook(book, config);
                EncodeBook(book, true);
            }

            return book;
        }

        /// <inheritdoc/>
        public async Task<List<Chapter>> GetChapterListAsync(BookSource source, Book book, CancellationToken cancellationToken = default)
        {
            if (source.Chapter == null)
                throw new InvalidOperationException("书源未配置章节目录模块");

            var config = source.Chapter;
            string url = DecodeBase64(book.Url);
            var encoding = string.IsNullOrEmpty(source.Charset) ? Encoding.UTF8 : Encoding.GetEncoding(source.Charset);
            var doc = await GetHtmlDocumentAsync(url, encoding, cancellationToken);
            var rangeNodes = doc.DocumentNode.QuerySelectorAll(config.Range);

            if (config.IsChildFiltered && !string.IsNullOrEmpty(config.ChildSelector))
            {
                rangeNodes = rangeNodes.SelectMany(p => p.QuerySelectorAll(config.ChildSelector));
            }

            var chapters = new List<Chapter>();
            int index = 0;

            foreach (var node in rangeNodes)
            {
                var chapter = new Chapter { SourceId = source.Id };
                ParseChapter(chapter, config, node);
                if (string.IsNullOrEmpty(chapter.Id))
                    continue;

                RepairChapter(chapter, config, url);
                ReplaceChapter(chapter, config);
                FormatChapter(chapter, config);

                if (!string.IsNullOrEmpty(chapter.Title) && !string.IsNullOrEmpty(chapter.Id))
                {
                    index++;
                    chapter.Index = index;
                    chapters.Add(chapter);
                }
            }

            return chapters;
        }

        /// <inheritdoc/>
        public async Task<ChapterContent> GetChapterContentAsync(BookSource source, Chapter chapter, CancellationToken cancellationToken = default)
        {
            if (source.ChapterContent == null)
                throw new InvalidOperationException("书源未配置章节内容模块");

            var config = source.ChapterContent;
            string url = DecodeBase64(chapter.Id);
            var encoding = string.IsNullOrEmpty(source.Charset) ? Encoding.UTF8 : Encoding.GetEncoding(source.Charset);
            var doc = await GetHtmlDocumentAsync(url, encoding, cancellationToken);
            var rangeNodes = doc.DocumentNode.QuerySelectorAll(config.Range);

            var content = new ChapterContent
            {
                Id = chapter.Id,
                Title = chapter.Title,
                ChapterIndex = chapter.Index
            };

            if (rangeNodes.Any())
            {
                var text = string.Join("\n", rangeNodes.Select(n => n.InnerText));
                content.Content = NormalizeContent(text);
            }

            return content;
        }

        #region 规则解析核心

        /// <summary>
        /// 执行规则字符串，根据模式分发.
        /// </summary>
        /// <param name="rule">规则字符串.</param>
        /// <param name="context">上下文对象（HtmlNode 或 JToken）.</param>
        /// <param name="source">书源配置（可选）.</param>
        /// <param name="book">书籍实例（可选）.</param>
        /// <param name="baseUrl">基础 URL（可选）.</param>
        /// <returns>解析结果.</returns>
        public string ExecuteRule(string rule, object context, BookSource source = null, Book book = null, string baseUrl = null)
        {
            if (string.IsNullOrWhiteSpace(rule))
                return string.Empty;

            // 模式 1: <js>...</js> 代码块
            var jsBlockMatch = Regex.Match(rule, @"^<js>(.*?)</js>$", RegexOptions.Singleline);
            if (jsBlockMatch.Success)
            {
                string jsCode = jsBlockMatch.Groups[1].Value;
                return ExecuteJavaScript(jsCode, context, source, book, baseUrl);
            }

            // 模式 2: @js: 前缀
            if (rule.StartsWith("@js:"))
            {
                string jsCode = rule.Substring(4);
                return ExecuteJavaScript(jsCode, context, source, book, baseUrl);
            }

            // 模式 3: $. 开头的 JSONPath
            if (rule.StartsWith("$."))
            {
                if (context is JToken token)
                {
                    var result = token.SelectTokens(rule);
                    return string.Join(" ", result.Select(r => r.ToString()));
                }
                else
                {
                    throw new ArgumentException("JSONPath 规则需要 JToken 上下文");
                }
            }

            // 模式 4: 标准 XPath
            if (rule.StartsWith("/") || rule.StartsWith("./") || rule.StartsWith("//"))
            {
                if (context is HtmlNode htmlNode)
                {
                    var nav = htmlNode.CreateNavigator();
                    var expr = nav.Compile(rule);
                    var iterator = nav.Select(expr);
                    var results = new List<string>();
                    while (iterator.MoveNext())
                    {
                        results.Add(iterator.Current.Value);
                    }
                    return string.Join(" ", results);
                }
                else if (context is XmlNode xmlNode)
                {
                    var doc = new XmlDocument();
                    doc.LoadXml(xmlNode.OuterXml);
                    var results = doc.SelectNodes(rule);
                    return string.Join(" ", results.Cast<XmlNode>().Select(n => n.InnerText));
                }
                else
                {
                    throw new ArgumentException("XPath 规则需要 XmlNode 或 HtmlNode 上下文");
                }
            }

            // 默认: CSS 选择器 (现有逻辑)
            if (context is HtmlNode htmlContext)
            {
                var selected = htmlContext.QuerySelector(rule);
                return selected?.InnerText ?? string.Empty;
            }

            return string.Empty;
        }

        private string ExecuteJavaScript(string jsCode, object context, BookSource source = null, Book book = null, string baseUrl = null)
        {
            // 注入上下文变量
            if (context is HtmlNode htmlNode)
            {
                _jsEngine.SetValue("html", htmlNode.OuterHtml);
                _jsEngine.SetValue("node", htmlNode);
            }
            else if (context is JToken token)
            {
                _jsEngine.SetValue("json", token.ToString(Newtonsoft.Json.Formatting.None));
                _jsEngine.SetValue("data", token);
            }

            // 注入 result、source、book、baseUrl 变量
            InjectContextVariables(context, source, book, baseUrl);

            try
            {
                var result = _jsEngine.Evaluate(jsCode);
                return result?.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                // 记录日志
                return $"JS 执行错误: {ex.Message}";
            }
        }

        #endregion

        #region 辅助方法

        private async Task<HtmlDocument> GetHtmlDocumentAsync(string url, Encoding encoding, CancellationToken cancellationToken = default)
        {
            var content = await _httpClient.GetStreamAsync(url, cancellationToken);
            var doc = new HtmlDocument();
            doc.Load(content, encoding);
            return doc;
        }

        private async Task<HtmlDocument> GetHtmlDocumentAsync(string url, string keyword, RequestConfig config, Encoding encoding, CancellationToken cancellationToken = default)
        {
            if (config?.Headers?.Any() ?? false)
            {
                foreach (var header in config.Headers)
                {
                    _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }

            var method = config.Method.Equals("post", StringComparison.OrdinalIgnoreCase) ? HttpMethod.Post : HttpMethod.Get;
            var request = new HttpRequestMessage(method, url);

            if (!string.IsNullOrEmpty(config.Body))
            {
                if (config.DataType == "form")
                {
                    var sp = config.Body.Split(',');
                    var dict = new Dictionary<string, string>();
                    foreach (var item in sp)
                    {
                        var text = item.Replace("{{keyword}}", keyword);
                        var kv = text.Split('=');
                        if (kv.Length > 1)
                        {
                            dict.Add(kv[0].Trim(), text.Replace(kv[0], string.Empty).Trim().TrimStart('='));
                        }
                    }
                    request.Content = new FormUrlEncodedContent(dict);
                }
                else if (config.DataType == "raw")
                {
                    request.Content = new StringContent(config.Body, Encoding.UTF8, "text/xml");
                }
                else if (config.DataType == "json")
                {
                    request.Content = new StringContent(config.Body, Encoding.UTF8, "application/json");
                }
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(content);
            return doc;
        }

        private void ParseAttributes(Book book, BookInformationConfigBase config, HtmlNode node)
        {
            book.BookName = ParseAttribute(config.BookName, node);
            book.Url = ParseAttribute(config.BookUrl, node);
            book.CoverUrl = ParseAttribute(config.BookCover, node);
            book.Author = ParseAttribute(config.BookAuthor, node);
            book.Description = ParseAttribute(config.BookDescription, node);
            book.Category = ParseAttribute(config.Category, node);
            book.Tag = ParseAttribute(config.Tag, node);
            book.UpdateTime = ParseAttribute(config.UpdateTime, node);

            // 状态和最新章节
            var statusStr = ParseAttribute(config.BookStatus, node);
            book.LatestChapterTitle = ParseAttribute(config.LastChapterTitle, node);
            book.LatestChapterId = ParseAttribute(config.LastChapterUrl, node);

            // 状态映射
            if (!string.IsNullOrEmpty(statusStr))
            {
                book.Status = statusStr switch
                {
                    "连载" => BookStatus.Writing,
                    "完结" => BookStatus.Finish,
                    _ => BookStatus.Invalid,
                };
            }
        }

        private void ParseChapter(Chapter chapter, ChapterConfig config, HtmlNode node)
        {
            chapter.Title = ParseAttribute(config.Title, node);
            chapter.Id = ParseAttribute(config.Url, node);
        }

        private string ParseAttribute(ModelsAttribute attr, HtmlNode node, BookSource source = null, Book book = null, string baseUrl = null)
        {
            if (attr == null || string.IsNullOrEmpty(attr.Rule))
                return string.Empty;

            // 使用 ExecuteRule 处理多种模式
            string result = ExecuteRule(attr.Rule, node, source, book, baseUrl);

            // 应用过滤
            if (!string.IsNullOrEmpty(attr.Filter))
            {
                result = Regex.Replace(result, attr.Filter, string.Empty).Trim();
            }

            return result;
        }

        private void FormatBook(Book book, BookInformationConfigBase config)
        {
            book.BookName = FormatString(book.BookName, config.BookName?.Filter);
            book.Url = FormatString(book.Url, config.BookUrl?.Filter);
            book.CoverUrl = FormatString(book.CoverUrl, config.BookCover?.Filter);
            book.Author = FormatString(book.Author, config.BookAuthor?.Filter);
            book.Description = FormatString(book.Description, config.BookDescription?.Filter);
            book.LatestChapterTitle = FormatString(book.LatestChapterTitle, config.LastChapterTitle?.Filter);
            book.LatestChapterId = FormatString(book.LatestChapterId, config.LastChapterUrl?.Filter);
            book.Category = FormatString(book.Category, config.Category?.Filter);
            book.Tag = FormatString(book.Tag, config.Tag?.Filter);
            book.UpdateTime = FormatString(book.UpdateTime, config.UpdateTime?.Filter);
        }

        private void FormatChapter(Chapter chapter, ChapterConfig config)
        {
            chapter.Title = FormatString(chapter.Title, config.Title?.Filter);
            chapter.Id = FormatString(chapter.Id, config.Url?.Filter);
        }

        private void ReplaceBook(Book book, BookInformationConfigBase config)
        {
            if (config.Replace?.Any() ?? false)
            {
                foreach (var replace in config.Replace)
                {
                    switch (replace.Field)
                    {
                        case FieldType.Title:
                            book.BookName = ReplaceString(book.BookName, replace.Old, replace.New);
                            break;
                        case FieldType.Url:
                            book.Url = ReplaceString(book.Url, replace.Old, replace.New);
                            break;
                        case FieldType.BookCover:
                            book.CoverUrl = ReplaceString(book.CoverUrl, replace.Old, replace.New);
                            break;
                        case FieldType.BookAuthor:
                            book.Author = ReplaceString(book.Author, replace.Old, replace.New);
                            break;
                        case FieldType.BookDescription:
                            book.Description = ReplaceString(book.Description, replace.Old, replace.New);
                            break;
                        case FieldType.LastChapterTitle:
                            book.LatestChapterTitle = ReplaceString(book.LatestChapterTitle, replace.Old, replace.New);
                            break;
                        case FieldType.LastChapterUrl:
                            book.LatestChapterId = ReplaceString(book.LatestChapterId, replace.Old, replace.New);
                            break;
                        case FieldType.Category:
                            book.Category = ReplaceString(book.Category, replace.Old, replace.New);
                            break;
                        case FieldType.Tag:
                            book.Tag = ReplaceString(book.Tag, replace.Old, replace.New);
                            break;
                        case FieldType.UpdateTime:
                            book.UpdateTime = ReplaceString(book.UpdateTime, replace.Old, replace.New);
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private void ReplaceChapter(Chapter chapter, ChapterConfig config)
        {
            if (config.Replace?.Any() ?? false)
            {
                foreach (var replace in config.Replace)
                {
                    switch (replace.Field)
                    {
                        case FieldType.Title:
                            chapter.Title = ReplaceString(chapter.Title, replace.Old, replace.New);
                            break;
                        case FieldType.Url:
                            chapter.Id = ReplaceString(chapter.Id, replace.Old, replace.New);
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private void RepairBook(Book book, BookInformationConfigBase config)
        {
            if (config.Repair?.Any() ?? false)
            {
                foreach (var repair in config.Repair)
                {
                    var isLeft = repair.Position == "l";
                    switch (repair.Field)
                    {
                        case FieldType.Title:
                            book.BookName = RepairString(book.BookName, isLeft, repair.Value);
                            break;
                        case FieldType.Url:
                            book.Url = RepairString(book.Url, isLeft, repair.Value);
                            break;
                        case FieldType.BookCover:
                            book.CoverUrl = RepairString(book.CoverUrl, isLeft, repair.Value);
                            break;
                        case FieldType.BookAuthor:
                            book.Author = RepairString(book.Author, isLeft, repair.Value);
                            break;
                        case FieldType.BookDescription:
                            book.Description = RepairString(book.Description, isLeft, repair.Value);
                            break;
                        case FieldType.LastChapterTitle:
                            book.LatestChapterTitle = RepairString(book.LatestChapterTitle, isLeft, repair.Value);
                            break;
                        case FieldType.LastChapterUrl:
                            book.LatestChapterId = RepairString(book.LatestChapterId, isLeft, repair.Value);
                            break;
                        case FieldType.Category:
                            book.Category = RepairString(book.Category, isLeft, repair.Value);
                            break;
                        case FieldType.Tag:
                            book.Tag = RepairString(book.Tag, isLeft, repair.Value);
                            break;
                        case FieldType.UpdateTime:
                            book.UpdateTime = RepairString(book.UpdateTime, isLeft, repair.Value);
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private void RepairChapter(Chapter chapter, ChapterConfig config, string baseUrl)
        {
            if (config.Repair?.Any() ?? false)
            {
                foreach (var repair in config.Repair)
                {
                    var isLeft = repair.Position == "l";
                    switch (repair.Field)
                    {
                        case FieldType.Title:
                            chapter.Title = RepairString(chapter.Title, isLeft, repair.Value);
                            break;
                        case FieldType.Url:
                            chapter.Id = RepairString(chapter.Id, isLeft, repair.Value);
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private void EncodeBook(Book book, bool needCheck = false)
        {
            book.Url = EncodeBase64(book.Url, needCheck);
            book.BookId = EncodeBase64($"{book.BookName}|{book.Author}", needCheck);
            book.LatestChapterId = EncodeBase64(book.LatestChapterId, needCheck);
        }

        private string FormatString(string text, string regex)
        {
            if (!string.IsNullOrEmpty(regex) && !string.IsNullOrEmpty(text))
            {
                try
                {
                    text = HttpUtility.HtmlDecode(text);
                    return Regex.Replace(text, regex, string.Empty).Trim();
                }
                catch
                {
                    // 忽略错误
                }
            }
            return text;
        }

        private string ReplaceString(string input, string pattern, string replacement)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            if (replacement.Contains("{{") && replacement.Contains("}}"))
            {
                var match = Regex.Match(input, pattern);
                if (match.Success && match.Groups.Count > 1)
                {
                    for (var i = 1; i < match.Groups.Count; i++)
                    {
                        var v = match.Groups[i].Value;
                        replacement = replacement.Replace($"{{{{{i}}}}}", v);
                    }
                    return replacement;
                }
            }
            return Regex.Replace(input, pattern, replacement);
        }

        private string RepairString(string input, bool isLeft, string addon)
            => isLeft ? addon + input : input + addon;

        private static string EncodeBase64(string text, bool needCheck = false)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            var bytes = Encoding.UTF8.GetBytes(text);
            return Convert.ToBase64String(bytes);
        }

        private static string DecodeBase64(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            var bytes = Convert.FromBase64String(text);
            return Encoding.UTF8.GetString(bytes);
        }

        private static string NormalizeContent(string html)
        {
            return Regex.Replace(html.Replace("</p>", "\n").Replace("<br>", "\n"), "<[^>]+>", string.Empty);
        }

        #endregion

        #region JsBridge 实现

        /// <summary>
        /// JavaScript 桥接对象，模拟 Legado 的 java 对象.
        /// </summary>
        public class JsBridge
        {
            /// <summary>
            /// Base64 编码（UTF-8）.
            /// </summary>
            public string base64Encode(string text)
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                return Convert.ToBase64String(bytes);
            }

            /// <summary>
            /// Base64 解码（UTF-8）.
            /// </summary>
            public string base64Decode(string text)
            {
                var bytes = Convert.FromBase64String(text);
                return Encoding.UTF8.GetString(bytes);
            }

            /// <summary>
            /// 格式化 UTC 时间戳.
            /// </summary>
            /// <param name="timestamp">毫秒时间戳.</param>
            /// <param name="format">格式字符串（如 "yyyy-MM-dd"）.</param>
            /// <param name="timezoneOffset">时区偏移（小时）.</param>
            /// <returns>格式化后的时间字符串.</returns>
            public string timeFormatUTC(long timestamp, string format, int timezoneOffset = 8)
            {
                var dt = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
                dt = dt.ToOffset(TimeSpan.FromHours(timezoneOffset));
                return dt.ToString(format);
            }

            /// <summary>
            /// 输出日志.
            /// </summary>
            public void log(object message)
            {
                System.Diagnostics.Debug.WriteLine($"[JS] {message}");
            }

            // 其他高频方法占位
            public string md5(string text) => throw new NotImplementedException();
            public string sha256(string text) => throw new NotImplementedException();
            public string urlEncode(string text) => throw new NotImplementedException();
            public string urlDecode(string text) => throw new NotImplementedException();
            public string substring(string text, int start, int end) => text.Substring(start, end - start);
            public int indexOf(string text, string search) => text.IndexOf(search);
            public string replace(string text, string pattern, string replacement) => Regex.Replace(text, pattern, replacement);
            public string trim(string text) => text.Trim();
        }

        #endregion
    }
}