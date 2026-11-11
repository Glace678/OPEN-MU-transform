namespace ConstantsReplacer
{
    public partial class MainForm : Form
    {
        private CancellationTokenSource? _cancellationTokenSource;

        public MainForm()
        {
            InitializeComponent();
        }

        private void SearchAndReplaceClick(object? sender, EventArgs e)
        {
            var cts = new CancellationTokenSource();
            this._cancellationTokenSource?.Dispose();
            this._cancellationTokenSource = cts;

            var folder = this._folderTextBox.Text;
            var fileNamePattern = this._fileNamePatternTextBox.Text;
            var searchPattern = this._searchPatternTextBox.Text;
            var constantsDefinitions = this._constantsDefinitionsTextBox.Text;

            this._progressBar.Visible = true;
            var progress = new Progress<(int Total, int Current)>(value =>
            {
                if (!this.IsDisposed)
                {
                    this._progressBar.Maximum = Math.Max(value.Total, value.Current);
                    this._progressBar.Value = Math.Clamp(value.Current, this._progressBar.Minimum, this._progressBar.Maximum);
                }
            });

            this._cancelButton.Enabled = true;
            this._searchButton.Enabled = false;
            _ = Task.Run(async () =>
            {
                Exception? failure = null;
                try
                {
                    var replacer = new Replacer();
                    await replacer.SearchAndReplaceAsync(
                        folder,
                        fileNamePattern,
                        searchPattern,
                        constantsDefinitions,
                        cts.Token,
                        progress);
                }
                catch (OperationCanceledException)
                {
                    // Cancelled by user.
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
                finally
                {
                    if (Interlocked.CompareExchange(ref this._cancellationTokenSource, null, cts) == cts)
                    {
                        cts.Dispose();
                    }
                    else
                    {
                        cts.Dispose();
                    }

                    if (!this.IsDisposed && this.IsHandleCreated)
                    {
                        this.Invoke(() =>
                        {
                            this._cancelButton.Enabled = false;
                            this._searchButton.Enabled = true;
                            this._progressBar.Visible = false;
                            if (failure is not null)
                            {
                                MessageBox.Show(this, failure.Message, "ConstantsReplacer Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        });
                    }
                }
            });
        }

        private void OnCancelClick(object? sender, EventArgs e)
        {
            this._cancellationTokenSource?.Cancel();
        }
    }

}
