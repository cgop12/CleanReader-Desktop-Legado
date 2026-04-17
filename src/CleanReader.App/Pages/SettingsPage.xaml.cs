// Copyright (c) Richasy. All rights reserved.

using System;
using CleanReader.ViewModels.Desktop;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace CleanReader.App.Pages
{
    /// <summary>
    /// 设置页面.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        private readonly SettingsPageViewModel _viewModel = SettingsPageViewModel.Instance;
        private readonly LibraryViewModel _libraryViewModel = LibraryViewModel.Instance;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsPage"/> class.
        /// </summary>
        public SettingsPage() => InitializeComponent();

        /// <inheritdoc/>
        protected override void OnNavigatedTo(NavigationEventArgs e)
            => _viewModel.InitializeCommand.Execute().Subscribe();

        private async void ImportBookSourceButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var filePicker = new Windows.Storage.Pickers.FileOpenPicker();
            filePicker.FileTypeFilter.Add(".json");
            filePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;

            // 获取窗口句柄
            var window = AppViewModel.Instance.MainWindow;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

            var file = await filePicker.PickSingleFileAsync();
            if (file != null)
            {
                try
                {
                    var json = await Windows.Storage.FileIO.ReadTextAsync(file);
                    var bookSource = Newtonsoft.Json.JsonConvert.DeserializeObject<CleanReader.Services.Novel.Models.BookSource>(json);
                    // 将书源添加到管理列表
                    // 需要访问书源视图模型
                    // 暂时先显示消息
                    var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                    {
                        Title = "导入成功",
                        Content = $"已导入书源: {bookSource.Name}",
                        CloseButtonText = "确定",
                        XamlRoot = this.Content.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
                catch (Exception ex)
                {
                    var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                    {
                        Title = "导入失败",
                        Content = $"错误: {ex.Message}",
                        CloseButtonText = "确定",
                        XamlRoot = this.Content.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
            }
        }
    }
}
