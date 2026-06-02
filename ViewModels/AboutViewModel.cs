using System.Windows.Input;

namespace GitUploadTool.ViewModels;

public class AboutViewModel : BindableBase
{
    public string AppName => "GitUploadTool";
    public string Version => "1.0.0";
    public string Description => "A desktop tool to upload local projects to GitHub easily.";
    public string GitHubUrl => "https://github.com/yourusername/GitUploadTool";

    public ICommand OpenUrlCommand { get; }

    public AboutViewModel()
    {
        OpenUrlCommand = new RelayCommand<string>(url =>
        {
            if (!string.IsNullOrEmpty(url))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
        });
    }
}
