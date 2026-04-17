// Copyright (c) Richasy. All rights reserved.

using System.Text;
using System.Threading.Tasks.Dataflow;
using System.Web;
using CleanReader.Services.Novel;
using CleanReader.Services.Novel.Models;
using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;
using Newtonsoft.Json;

namespace CleanReader.Services.Novel
{
    /// <summary>
    /// 在线小说解析服务.
    /// </summary>
    public sealed partial class NovelService
    {
        private const string TextNode = "text";
        private const string OriginUrl = "ORIGIN_URL";
        private readonly LegadoRuleParser _legadoParser = new LegadoRuleParser();
        private Dictionary<string, BookSource> _sources;

        /// <summary>
        /// 初始化目前的可用书源.
        /// </summary>
        /// <param name="sourceFolderPath">书源文件夹地址.</param>
        /// <returns><see cref="Task"/>.</returns>
        public async Task InitializeBookSourcesAsync(string sourceFolderPath)
        {
            _sources = new Dictionary<string, BookSource>();
            _httpClient = GetHttpClient();
            var files = Directory.GetFiles(sourceFolderPath);
            foreach (var fileName in files)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(fileName);
                    var source = JsonConvert.DeserializeObject<BookSource>(content);
                    _sources.Add(source.Id, source);
                    Console.WriteLine($"已添加源：{source.Id}");
                }
                catch (Exception)
                {
                    continue;
                }
            }
        }

        /// <summary>
        /// 获取全部的书源.
        /// </summary>
        /// <returns><see cref="BookSource"/> 列表.</returns>
        public List<BookSource> GetBookSources()
        {
            return _sources == null ?
                new List<BookSource>() :
                _sources.Select(p => p.Value).ToList();
        }

        /// <summary>
        /// 根据书名查询书籍.
        /// </summary>
        /// <param name="sourceId">书源Id.</param>
        /// <param name="bookName">书名.</param>
        /// <returns><see cref="Book"/> 列表.</returns>
        /// <exception cref="InvalidDataException">书源定义中没有搜索模块.</exception>
        /// <exception cref="ArgumentOutOfRangeException">指定的源不存在.</exception>
        public async Task<List<Book>> SearchBookAsync(string sourceId, string bookName)
        {
            if (_sources.ContainsKey(sourceId))
            {
                var source = _sources[sourceId];
                var searchConfig = source.Search;
                if (searchConfig == null || string.IsNullOrEmpty(searchConfig.SearchUrl))
                {
                    throw new InvalidDataException($"{sourceId} 中没有搜索模块");
                }

                // 使用 Legado 解析引擎进行搜索
                var books = await _legadoParser.SearchAsync(source, bookName, CancellationToken.None);

                // 如果需要详情，并行获取
                if (searchConfig.NeedDetail)
                {
                    var tasks = new List<Task>();
                    foreach (var book in books)
                    {
                        tasks.Add(InitializeBookDetailAsync(sourceId, book));
                    }
                    await Task.WhenAll(tasks);
                }

                return books;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(sourceId), "没有找到指定的源.");
            }
        }

        /// <summary>
        /// 从不同源搜索书名.
        /// </summary>
        /// <param name="bookName">书名.</param>
        /// <returns>以书源分组的搜索结果.</returns>
        public async Task<Dictionary<string, List<Book>>> SearchBookAsync(string bookName)
        {
            var tasks = new List<Task>();
            var result = new Dictionary<string, List<Book>>();
            foreach (var source in _sources)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var books = await SearchBookAsync(source.Key, bookName);
                        result.Add(source.Key, books);
                    }
                    catch (Exception)
                    {
                    }
                }));
            }

            await Task.WhenAll(tasks);
            return result;
        }

        /// <summary>
        /// 获取书籍目录.
        /// </summary>
        /// <param name="sourceId">书源Id.</param>
        /// <param name="bookUrl">书籍地址.</param>
        /// <param name="cancellationTokenSource">中止令牌.</param>
        /// <returns>章节列表.</returns>
        /// <exception cref="ArgumentOutOfRangeException">书源不存在.</exception>
        /// <exception cref="InvalidDataException">书源定义中没有章节目录模块.</exception>
        public async Task<List<Chapter>> GetBookChaptersAsync(string sourceId, string bookUrl, CancellationTokenSource cancellationTokenSource)
        {
            if (!_sources.ContainsKey(sourceId))
            {
                throw new ArgumentOutOfRangeException(nameof(sourceId), "没有找到指定的源.");
            }

            var source = _sources[sourceId];
            var book = new Book
            {
                SourceId = sourceId,
                Url = bookUrl
            };
            return await _legadoParser.GetChapterListAsync(source, book, cancellationTokenSource?.Token ?? CancellationToken.None);
        }

        /// <summary>
        /// 获取章节内容.
        /// </summary>
        /// <param name="sourceId">书源Id.</param>
        /// <param name="chapter">章节实例.</param>
        /// <param name="cancellationTokenSource">中止令牌.</param>
        /// <returns>章节内容.</returns>
        /// <exception cref="ArgumentOutOfRangeException">书源不存在.</exception>
        /// <exception cref="InvalidDataException">指定源中没有章节内容模块.</exception>
        public async Task<ChapterContent> GetChapterContentAsync(string sourceId, Chapter chapter, CancellationTokenSource cancellationTokenSource)
        {
            if (!_sources.ContainsKey(sourceId))
            {
                throw new ArgumentOutOfRangeException(nameof(sourceId), "没有找到指定的源.");
            }

            var source = _sources[sourceId];
            return await _legadoParser.GetChapterContentAsync(source, chapter, cancellationTokenSource?.Token ?? CancellationToken.None);
        }

        /// <summary>
        /// 初始化书籍详情.
        /// </summary>
        /// <param name="sourceId">书源Id.</param>
        /// <param name="book">书籍实例.</param>
        /// <returns><see cref="Task"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">书源不存在.</exception>
        /// <exception cref="InvalidOperationException">指定源中不支持获取章节详情.</exception>
        public async Task InitializeBookDetailAsync(string sourceId, Book book)
        {
            if (_sources.ContainsKey(sourceId))
            {
                var source = _sources[sourceId];
                if (source.BookDetail != null && source.IsBookDetailEnabled)
                {
                    try
                    {
                        await _legadoParser.GetBookInfoAsync(source, book, CancellationToken.None);
                    }
                    catch (Exception)
                    {
                        // 忽略解析错误
                    }
                }
                else
                {
                    throw new InvalidOperationException("不支持获取书籍详情");
                }
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(sourceId), "没有找到指定的源.");
            }
        }

        /// <summary>
        /// 从分类中获取书籍列表.
        /// </summary>
        /// <param name="sourceId">源Id.</param>
        /// <param name="categoryName">分类名.</param>
        /// <param name="page">页码.</param>
        /// <param name="tokenSource">终止令牌.</param>
        /// <returns>书籍列表.</returns>
        /// <exception cref="ArgumentOutOfRangeException">源或配置不存在.</exception>
        /// <exception cref="InvalidOperationException">不支持该操作.</exception>
        public async Task<List<Book>> GetBooksWithCategoryAsync(string sourceId, string categoryName, int page = 1, CancellationTokenSource tokenSource = null)
        {
            if (!_sources.ContainsKey(sourceId))
            {
                throw new ArgumentOutOfRangeException(nameof(sourceId), "没有找到指定的源.");
            }

            var source = _sources[sourceId];
            if (source.IsExploreEnabled && source.Explore != null)
            {
                var category = source.Explore.Categories.Where(p => p.Name.Equals(categoryName)).FirstOrDefault();
                if (category == null)
                {
                    throw new ArgumentOutOfRangeException($"分类 {categoryName} 不在配置中");
                }

                var url = category.Url.Replace("{{page}}", page.ToString());
                if (!url.StartsWith("http"))
                {
                    url = source.WebUrl + url;
                }

                var encoding = string.IsNullOrEmpty(source.Charset) ? Encoding.UTF8 : Encoding.GetEncoding(source.Charset);
                var doc = await GetHtmlDocumentAsync(url, encoding, source.Explore.Request, tokenSource);
                var bookList = new List<Book>();
                var nodes = doc.DocumentNode.QuerySelectorAll(source.Explore.Range);
                var exploreConfig = source.Explore;

                var options = new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 12, BoundedCapacity = DataflowBlockOptions.Unbounded };
                if (tokenSource != null)
                {
                    options.CancellationToken = tokenSource.Token;
                }

                var action = new ActionBlock<Task>(async t =>
                {
                    await t;
                });

                foreach (var node in nodes)
                {
                    var book = new Book
                    {
                        SourceId = sourceId,
                    };
                    var statusStr = string.Empty;
                    try
                    {
                        InitializeBook(book, exploreConfig, node, out statusStr);
                        if (string.IsNullOrEmpty(book.BookName))
                        {
                            continue;
                        }

                        FormatBook(book, exploreConfig, ref statusStr);
                        ReplaceBook(book, exploreConfig, ref statusStr);
                        RepairBook(book, exploreConfig, ref statusStr);
                        EncodingBook(book, statusStr);

                        if (exploreConfig.NeedDetail)
                        {
                            action.Post(InitializeBookDetailAsync(sourceId, book));
                        }
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    Console.WriteLine($"已解析书籍：{book.BookName}");
                    bookList.Add(book);
                }

                action.Complete();
                if (action.InputCount > 0)
                {
                    await action.Completion;
                }

                return bookList;
            }
            else
            {
                throw new InvalidOperationException($"{sourceId} 中不支持分类索引");
            }
        }
    }
}
