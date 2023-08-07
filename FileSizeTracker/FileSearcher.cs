using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FileSizeTracker;

public class FileSearcher
{
    private BackgroundWorker worker;
    private CancellationTokenSource cts;

    public string DriveOrVolume { get; set; }

    public event ProgressChangedEventHandler ProgressChanged;

    public event EventHandler<ResultEventArgs> ResultFound;

    public event RunWorkerCompletedEventHandler SearchCompleted;

    public FileSearcher(string driveOrVolume)
    {
        DriveOrVolume = driveOrVolume;

        worker = new BackgroundWorker();
        worker.WorkerReportsProgress = true;
        worker.WorkerSupportsCancellation = true;
        worker.DoWork += Worker_DoWork;
        worker.ProgressChanged += Worker_ProgressChanged;
        worker.RunWorkerCompleted += Worker_RunWorkerCompleted;

        cts = new CancellationTokenSource();
    }

    public void Start()
    {
        if (!worker.IsBusy)
        {
            worker.RunWorkerAsync();
        }
        else if (cts.IsCancellationRequested)
        {
            cts = new CancellationTokenSource();
        }
    }

    public void Pause()
    {
        cts.Cancel();
    }

    public void Cancel()
    {
        cts.Cancel();
        worker.CancelAsync();
    }

private void Worker_DoWork(object sender, DoWorkEventArgs e)
{
    BackgroundWorker worker = sender as BackgroundWorker;
    
    var directories = Directory.GetDirectories(DriveOrVolume, "*", new EnumerationOptions
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = true
    });
    
    int directoryCount = directories.Count();
    int processedDirectories = 0;
    object lockObject = new object();

    Parallel.ForEach(directories, (directory) =>
    {
        if (worker.CancellationPending)
        {
            e.Cancel = true;
            return;
        }

        while (cts.Token.IsCancellationRequested)
        {
            Thread.Sleep(100);
        }

        try
        {
            string[] files = Directory.GetFiles(directory);

            bool hasLargeFile = files.Any(file =>
            {
                try
                {
                    return new FileInfo(file).Length > 10 * 1024 * 1024; // If the file size is larger than 10 MB
                }
                catch
                {
                    // If an exception occurs, ignore it and continue with the next file 
                    return false;
                }
            });

            if (hasLargeFile)
            {
                string[] allFiles = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);

                int fileCount = allFiles.Length;

                long fileSizeSum = allFiles.Sum(file =>
                {
                    try
                    {
                        return new FileInfo(file).Length;
                    }
                    catch
                    {
                        // If an exception occurs, ignore it and continue with the next file 
                        return 0;
                    }
                });

                lock (lockObject)
                {
                    worker.ReportProgress(processedDirectories * 100 / directoryCount,
                        new ResultEventArgs(directory, fileCount, fileSizeSum));
                }
            }
        }
        catch
        {
            // If an exception occurs, ignore it and continue with the next directory 
        }

        lock (lockObject)
        {
            processedDirectories++;
            worker.ReportProgress(processedDirectories * 100 / directoryCount);
        }
    });
}
    
    private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
    {
        ProgressChanged?.Invoke(this, e);

        if (e.UserState != null && e.UserState is ResultEventArgs)
        {
            ResultFound?.Invoke(this, e.UserState as ResultEventArgs);
        }
    }

    private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
        SearchCompleted?.Invoke(this, e);
    }
}

public class ResultEventArgs : EventArgs
{
    public string Path { get; set; }

    public int FileCount { get; set; }

    public long FileSizeSum { get; set; }

    public ResultEventArgs(string path, int fileCount, long fileSizeSum)
    {
        Path = path;
        FileCount = fileCount;
        FileSizeSum = fileSizeSum;
    }
}