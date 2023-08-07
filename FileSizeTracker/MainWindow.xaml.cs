using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FileSizeTracker
{
    public partial class MainWindow : Window
    {
        private FileSearcher fileSearcher;
        private bool isSearching;
        private List<ResultItem> resultItems;

        public MainWindow()
        {
            InitializeComponent();
            PopulateDriveList();
            isSearching = false;
            resultItems = new List<ResultItem>();
        }

        private void PopulateDriveList()
        {
            DriveList.Items.Clear();
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    DriveList.Items.Add(drive);
                }
                else
                {
                    foreach (string volume in Directory.GetDirectories(drive.Name))
                    {
                        DriveList.Items.Add(new VolumeInfo(volume));
                    }
                }
            }
        }

        private void DriveList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SearchButton.IsEnabled = DriveList.SelectedItem != null;
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (isSearching)
            {
                fileSearcher.Pause();
                SearchButton.Content = "Resume Search";
                isSearching = false;
            }
            else
            {
                if (fileSearcher == null)
                {
                    fileSearcher = new FileSearcher(DriveList.SelectedItem.ToString());
                    fileSearcher.ProgressChanged += FileSearcher_ProgressChanged;
                    fileSearcher.ResultFound += FileSearcher_ResultFound;
                    fileSearcher.SearchCompleted += FileSearcher_SearchCompleted;
                }
                fileSearcher.Start();
                SearchButton.Content = "Pause Search";
                isSearching = true;
            }
        }

        private void FileSearcher_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            SearchProgress.Value = e.ProgressPercentage;
        }

        private void FileSearcher_ResultFound(object sender, ResultEventArgs e)
        {
            var resultItem = new ResultItem(e.Path, e.FileCount, e.FileSizeSum);
            resultItems.Add(resultItem);
            ResultList.Items.Add(resultItem);

            var watcher = new FileSystemWatcher(e.Path);
            watcher.IncludeSubdirectories = true;
            watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.DirectoryName;
            watcher.Changed += Watcher_Changed;
            watcher.Created += Watcher_Changed;
            watcher.Deleted += Watcher_Deleted;
            watcher.Renamed += Watcher_Renamed;

            watcher.EnableRaisingEvents = true;

            resultItem.Watcher = watcher;

        }


        private void FileSearcher_SearchCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                MessageBox.Show("The search was cancelled.", "File Search App", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (e.Error != null)
            {
                MessageBox.Show("An error occurred during the search.", "File Search App", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                MessageBox.Show("The search completed successfully.", "File Search App", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            SearchButton.Content = "Start Search";
            isSearching = false;
        }

        private void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            var resultItem = resultItems.FirstOrDefault(item => item.Watcher == sender);
            resultItem?.Update();
        }

        private void Watcher_Deleted(object sender, FileSystemEventArgs e)
        {
            var resultItem = resultItems.FirstOrDefault(item => item.Watcher == sender);
            
            if (resultItem == null) return;
            
            if (e.FullPath == resultItem.Path)
            {
                ResultList.Items.Remove(resultItem);
                resultItems.Remove(resultItem);
                resultItem.Watcher.Dispose();
            }
            else
            {
                resultItem.Update();
            }
        }

        private void Watcher_Renamed(object sender, RenamedEventArgs e)
        {
            var resultItem = resultItems.FirstOrDefault(item => item.Watcher == sender);
            
            if (resultItem == null) return;
            
            if (e.OldFullPath == resultItem.Path)
            {
                resultItem.Path = e.FullPath;
            }
            else
            {
                resultItem.Update();
            }
        }
    }

    public class VolumeInfo
    {
        public string Name { get; set; }

        public DriveType DriveType { get; set; }

        public string DriveFormat { get; set; }

        public long TotalSize { get; set; }

        public long TotalFreeSpace { get; set; }

        public VolumeInfo(string path)
        {
            Name = path;

            var drive = new DriveInfo(Path.GetPathRoot(path));

            DriveType = drive.DriveType;

            try
            {
                DriveFormat = drive.DriveFormat;
            }
            catch
            {
                DriveFormat = "Unknown";
            }

            try
            {
                TotalSize = drive.TotalSize;
            }
            catch
            {
                TotalSize = 0;
            }

            try
            {
                TotalFreeSpace = drive.TotalFreeSpace;
            }
            catch
            {
                TotalFreeSpace = 0;
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }


    public class ResultItem : INotifyPropertyChanged
    {
        private string path;
        public string Path
        {
            get { return path; }
            set
            {
                if (path == value) return;
                path = value;
                OnPropertyChanged(nameof(Path));
            }
        }

        private int fileCount;
        public int FileCount
        {
            get => fileCount;
            set
            {
                if (fileCount == value) return;
                fileCount = value;
                OnPropertyChanged(nameof(FileCount));
            }
        }

        private long fileSizeSum;
        public long FileSizeSum
        {
            get { return fileSizeSum; }
            set
            {
                if (fileSizeSum == value) return;
                fileSizeSum = value;
                OnPropertyChanged(nameof(FileSizeSum));
            }
        }

        public FileSystemWatcher Watcher { get; set; }

        public ResultItem(string path, int fileCount, long fileSizeSum)
        {
            Path = path;
            FileCount = fileCount;
            FileSizeSum = fileSizeSum;
        }

        public void Update()
        {
            try
            {
                string[] files = Directory.GetFiles(Path, "*", SearchOption.AllDirectories);

                FileCount = files.Length;

                long sizeSum = 0;

                foreach (string file in files)
                {
                    try
                    {
                        sizeSum += new FileInfo(file).Length;
                    }
                    catch
                    {
                    }
                }

                FileSizeSum = sizeSum;
            }
            catch
            {
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}