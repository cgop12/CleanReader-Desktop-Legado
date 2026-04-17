// Copyright (c) Richasy. All rights reserved.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CleanReader.Services.Novel.Models;

namespace CleanReader.Services.Novel
{
    /// <summary>
    /// 书源解析引擎接口.
    /// </summary>
    public interface IBookSourceEngine
    {
        /// <summary>
        /// 搜索书籍.
        /// </summary>
        /// <param name="source">书源配置.</param>
        /// <param name="keyword">搜索关键词.</param>
        /// <param name="cancellationToken">取消令牌.</param>
        /// <returns>书籍列表.</returns>
        Task<List<Book>> SearchAsync(BookSource source, string keyword, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取书籍详情.
        /// </summary>
        /// <param name="source">书源配置.</param>
        /// <param name="book">书籍实例（至少包含 BookId 和 Url）.</param>
        /// <param name="cancellationToken">取消令牌.</param>
        /// <returns>更新后的书籍信息.</returns>
        Task<Book> GetBookInfoAsync(BookSource source, Book book, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取章节目录.
        /// </summary>
        /// <param name="source">书源配置.</param>
        /// <param name="book">书籍实例（至少包含 Url）.</param>
        /// <param name="cancellationToken">取消令牌.</param>
        /// <returns>章节列表.</returns>
        Task<List<Chapter>> GetChapterListAsync(BookSource source, Book book, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取章节内容.
        /// </summary>
        /// <param name="source">书源配置.</param>
        /// <param name="chapter">章节实例（至少包含 Id）.</param>
        /// <param name="cancellationToken">取消令牌.</param>
        /// <returns>章节内容.</returns>
        Task<ChapterContent> GetChapterContentAsync(BookSource source, Chapter chapter, CancellationToken cancellationToken = default);
    }
}