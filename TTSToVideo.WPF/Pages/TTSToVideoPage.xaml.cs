using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TTSToVideo.Business.Models;
using TTSToVideo.Helpers;
using TTSToVideo.WPF.Models;
using TTSToVideo.WPF.Pages;
using TTSToVideo.WPF.ViewsModels;
using Path = System.IO.Path;

namespace TTSToVideo.WPF
{
    /// <summary>
    /// Interaction logic for MainPage.xaml
    /// </summary>
    public partial class TTSToVideoPage : Page
    {
        public TTSToVideoPage(TTSToVideoViewModel ttsToVideo, IServiceProvider serviceProvider)
        {
            InitializeComponent();
            this.DataContext = ttsToVideo;
            this.ttsToVideo = ttsToVideo;
            ServiceProvider = serviceProvider;
            this.Loaded += MainPage_Loaded;
        }

        public TTSToVideoViewModel ttsToVideo { get; }
        public IServiceProvider ServiceProvider { get; }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!this.ttsToVideo.IsInitialized)
            {
                await this.ttsToVideo.Init();
            }
        }

        private void OpenFontStyleWindow(object sender, RoutedEventArgs e)
        {
            var window = (ServiceProvider.GetService(typeof(FontStyleWindowsView)) as FontStyleWindowsView);
            //Get the current item of the datagrid
            var item = (sender as Button).DataContext as StatementModel;
            (window.DataContext as FontStyleViewModel).Statement = item;
            window.ShowDialog();
        }

        private void IncreaseFontSize_Click(object sender, RoutedEventArgs e)
        {
            if (this.PromptTextBox.FontSize < 30) // Set a maximum font size limit  
            {
                this.PromptTextBox.FontSize += 2;
            }
        }

        private int _lastMatchIndex = -1;
        private int _lastMatchLength = 0;

        private string GetPromptText()
        {
            var vm = DataContext as ViewsModels.TTSToVideoViewModel;
            return vm?.Model?.Prompt ?? string.Empty;
        }

        private void SelectMatch(int index, int length)
        {
            if (index < 0 || length <= 0) return;
            PromptTextBox.Focus();
            PromptTextBox.SelectionStart = index;
            PromptTextBox.SelectionLength = length;
            _lastMatchIndex = index;
            _lastMatchLength = length;
        }

        private void FindButton_Click(object sender, RoutedEventArgs e)
        {
            string query = SearchTextBox.Text;
            if (string.IsNullOrWhiteSpace(query)) return;

            string text = GetPromptText();
            int idx = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                SelectMatch(idx, query.Length);
            }
            else
            {
                _lastMatchIndex = -1;
                _lastMatchLength = 0;
                System.Media.SystemSounds.Beep.Play();
            }
        }

        private void FindNextButton_Click(object sender, RoutedEventArgs e)
        {
            string query = SearchTextBox.Text;
            if (string.IsNullOrWhiteSpace(query)) return;

            string text = GetPromptText();
            int startPos = (_lastMatchIndex >= 0) ? _lastMatchIndex + _lastMatchLength : 0;
            if (startPos >= text.Length) startPos = 0;

            int idx = text.IndexOf(query, startPos, StringComparison.OrdinalIgnoreCase);
            if (idx < 0 && startPos > 0)
            {
                // wrap
                idx = text.IndexOf(query, 0, StringComparison.OrdinalIgnoreCase);
            }

            if (idx >= 0)
            {
                SelectMatch(idx, query.Length);
            }
            else
            {
                System.Media.SystemSounds.Beep.Play();
            }
        }

        private static readonly string[] _videoExts = [".mp4", ".mov", ".mkv", ".webm", ".avi", ".m4v"];

        private void ReplaceMediaButton_Click(object sender, RoutedEventArgs e)
        {
            // Get the bound statement (DataContext of the button)
            if (!(sender is FrameworkElement fe && fe.DataContext is TTSToVideo.WPF.Models.StatementModel stmt))
                return;

            var ofd = new OpenFileDialog
            {
                Title = "Select Image or Video",
                Filter = "Media Files|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.gif;*.mp4;*.mov;*.mkv;*.webm;*.avi;*.m4v|All Files|*.*",
                Multiselect = false
            };
            if (ofd.ShowDialog() != true) return;

            string selected = ofd.FileName;
            string ext = Path.GetExtension(selected).ToLowerInvariant();
            bool isVideo = _videoExts.Contains(ext);

            try
            {

                var path = Path.Combine(ttsToVideo.ProjectSelected.FullPath, $"{ttsToVideo.SelectedPlatform.Value}", ttsToVideo.SelectedLanguage);
                if (isVideo)
                {
                    // Target path (reuse existing video path if present; else derive from first image path)

                    var baseImg = path;

                    var targetPath = PathHelper.GenerateImagePath(baseImg, stmt.Text, $".jpg{ext}"); 

                    // Backup if exists
                    if (File.Exists(targetPath))
                    {
                        string backup = $"{Path.GetFileNameWithoutExtension(targetPath)}_{DateTime.Now:yyyyMMdd_HHmmss}.bak{Path.GetExtension(targetPath)}";
                        string backupFull = Path.Combine(Path.GetDirectoryName(targetPath)!, backup);
                        File.Copy(targetPath, backupFull, true);
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                    File.Copy(selected, targetPath, true);
                    stmt.ImageAnimatedPath = targetPath;
                }
                else
                {
                    // Replace primary image (first image)


                    var imgRef = path;

                    var targetPath = PathHelper.GenerateImagePath(imgRef, stmt.Text, ext);

                    if (imgRef == null || string.IsNullOrWhiteSpace(imgRef))
                    {
                        MessageBox.Show("Statement has no image slot to replace.", "Replace Media", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (File.Exists(targetPath))
                    {
                        string backup = $"{Path.GetFileNameWithoutExtension(targetPath)}_{DateTime.Now:yyyyMMdd_HHmmss}.bak{Path.GetExtension(targetPath)}";
                        string backupFull = Path.Combine(Path.GetDirectoryName(targetPath)!, backup);
                        File.Copy(targetPath, backupFull, true);
                    }

                    File.Copy(selected, targetPath, true);
                }

                // Force UI refresh if using INotifyPropertyChanged in VM (raise manually if needed)
                // Example (uncomment if Statement implements it and exposes a Raise method):
                // stmt.Raise(nameof(stmt.Images));
                // stmt.Raise(nameof(stmt.ImageAnimatedPath));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error replacing media: {ex.Message}", "Replace Media", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

      
    }
}
