// XNBExporterPro - MainForm.cs
// Enhanced Windows Forms GUI with drag & drop, batch processing, preview, and more

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace XNBExporterPro
{
    public class MainForm : Form
    {
        // Controls
        private MenuStrip menuStrip;
        private ToolStrip toolStrip;
        private StatusStrip statusStrip;
        private SplitContainer mainSplitContainer;
        private ListView fileListView;
        private PictureBox previewBox;
        private Panel previewPanel;
        private Label previewInfoLabel;
        private ProgressBar progressBar;
        private ToolStripStatusLabel statusLabel;
        private ToolStripStatusLabel fileCountLabel;
        private ComboBox formatComboBox;
        private Label formatLabel;
        private CheckBox overwriteCheckBox;
        private CheckBox recursiveCheckBox;

        // Data
        private List<string> loadedFiles = new List<string>();
        private CancellationTokenSource cts;
        private ImageFormat selectedFormat = ImageFormat.PNG;

        public MainForm()
        {
            InitializeComponent();
            SetupDragDrop();
        }

        private void InitializeComponent()
        {
            this.Text = "XNB Exporter Pro v2.0 — XNB to PNG/BMP/TGA Converter";
            this.Size = new Size(1000, 700);
            this.MinimumSize = new Size(800, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Icon = CreateAppIcon();
            this.Font = new Font("Segoe UI", 9f);
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;

            // === Menu Strip ===
            menuStrip = new MenuStrip();
            menuStrip.BackColor = Color.FromArgb(45, 45, 48);
            menuStrip.ForeColor = Color.White;
            menuStrip.Renderer = new DarkMenuRenderer();

            var fileMenu = new ToolStripMenuItem("File");
            fileMenu.DropDownItems.Add("Open Files...", null, (s, e) => OpenFiles());
            fileMenu.DropDownItems.Add("Open Folder...", null, (s, e) => OpenFolder());
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("Clear List", null, (s, e) => ClearFiles());
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("Exit", null, (s, e) => Close());

            var convertMenu = new ToolStripMenuItem("Convert");
            convertMenu.DropDownItems.Add("Convert All", null, (s, e) => ConvertAll());
            convertMenu.DropDownItems.Add("Convert Selected", null, (s, e) => ConvertSelected());
            convertMenu.DropDownItems.Add(new ToolStripSeparator());
            convertMenu.DropDownItems.Add("Convert to Custom Folder...", null, (s, e) => ConvertToFolder());
            convertMenu.DropDownItems.Add(new ToolStripSeparator());
            convertMenu.DropDownItems.Add("Cancel", null, (s, e) => CancelConversion());

            var helpMenu = new ToolStripMenuItem("Help");
            helpMenu.DropDownItems.Add("About", null, (s, e) => ShowAbout());

            menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, convertMenu, helpMenu });
            this.MainMenuStrip = menuStrip;
            this.Controls.Add(menuStrip);

            // === Tool Strip ===
            toolStrip = new ToolStrip();
            toolStrip.BackColor = Color.FromArgb(37, 37, 38);
            toolStrip.ForeColor = Color.White;
            toolStrip.GripStyle = ToolStripGripStyle.Hidden;
            toolStrip.Renderer = new DarkMenuRenderer();

            var openFilesBtn = new ToolStripButton("📂 Open Files");
            openFilesBtn.Click += (s, e) => OpenFiles();
            openFilesBtn.ForeColor = Color.White;

            var openFolderBtn = new ToolStripButton("📁 Open Folder");
            openFolderBtn.Click += (s, e) => OpenFolder();
            openFolderBtn.ForeColor = Color.White;

            var convertAllBtn = new ToolStripButton("▶ Convert All");
            convertAllBtn.Click += (s, e) => ConvertAll();
            convertAllBtn.ForeColor = Color.LightGreen;

            var convertSelBtn = new ToolStripButton("▷ Convert Selected");
            convertSelBtn.Click += (s, e) => ConvertSelected();
            convertSelBtn.ForeColor = Color.LightGreen;

            var cancelBtn = new ToolStripButton("⏹ Cancel");
            cancelBtn.Click += (s, e) => CancelConversion();
            cancelBtn.ForeColor = Color.Salmon;

            var clearBtn = new ToolStripButton("🗑 Clear");
            clearBtn.Click += (s, e) => ClearFiles();
            clearBtn.ForeColor = Color.White;

            toolStrip.Items.AddRange(new ToolStripItem[]
            {
                openFilesBtn, openFolderBtn,
                new ToolStripSeparator(),
                convertAllBtn, convertSelBtn, cancelBtn,
                new ToolStripSeparator(),
                clearBtn
            });
            this.Controls.Add(toolStrip);

            // === Options Panel ===
            var optionsPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(37, 37, 38),
                Padding = new Padding(10, 5, 10, 5)
            };

            formatLabel = new Label
            {
                Text = "Output Format:",
                AutoSize = true,
                Location = new Point(15, 10),
                ForeColor = Color.White
            };
            optionsPanel.Controls.Add(formatLabel);

            formatComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(120, 7),
                Width = 100,
                BackColor = Color.FromArgb(51, 51, 55),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            formatComboBox.Items.AddRange(new object[] { "PNG", "BMP", "TGA" });
            formatComboBox.SelectedIndex = 0;
            formatComboBox.SelectedIndexChanged += (s, e) =>
            {
                switch (formatComboBox.SelectedIndex)
                {
                    case 0: selectedFormat = ImageFormat.PNG; break;
                    case 1: selectedFormat = ImageFormat.BMP; break;
                    case 2: selectedFormat = ImageFormat.TGA; break;
                }
            };
            optionsPanel.Controls.Add(formatComboBox);

            overwriteCheckBox = new CheckBox
            {
                Text = "Overwrite existing",
                Checked = true,
                AutoSize = true,
                Location = new Point(240, 9),
                ForeColor = Color.White
            };
            optionsPanel.Controls.Add(overwriteCheckBox);

            recursiveCheckBox = new CheckBox
            {
                Text = "Recursive folders",
                Checked = true,
                AutoSize = true,
                Location = new Point(395, 9),
                ForeColor = Color.White
            };
            optionsPanel.Controls.Add(recursiveCheckBox);

            this.Controls.Add(optionsPanel);

            // === Main Split Container ===
            mainSplitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 500,
                BackColor = Color.FromArgb(30, 30, 30),
                SplitterWidth = 4
            };

            // === File List View ===
            fileListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                MultiSelect = true
            };
            fileListView.Columns.Add("File Name", 250);
            fileListView.Columns.Add("Path", 250);
            fileListView.Columns.Add("Size", 80, HorizontalAlignment.Right);
            fileListView.Columns.Add("Status", 150);
            fileListView.SelectedIndexChanged += FileListView_SelectedIndexChanged;
            fileListView.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Delete) RemoveSelected();
                if (e.KeyCode == Keys.A && e.Control) SelectAll();
            };

            mainSplitContainer.Panel1.Controls.Add(fileListView);

            // === Preview Panel ===
            previewPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(25, 25, 25)
            };

            previewInfoLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 60,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.LightGray,
                BackColor = Color.FromArgb(35, 35, 38),
                Text = "Select an XNB file to preview\n\nDrag & Drop XNB files or folders here",
                Font = new Font("Segoe UI", 10f)
            };

            previewBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(25, 25, 25)
            };
            // Checkerboard for transparency
            previewBox.Paint += PreviewBox_Paint;

            previewPanel.Controls.Add(previewBox);
            previewPanel.Controls.Add(previewInfoLabel);
            mainSplitContainer.Panel2.Controls.Add(previewPanel);

            this.Controls.Add(mainSplitContainer);

            // === Status Strip ===
            statusStrip = new StatusStrip
            {
                BackColor = Color.FromArgb(0, 122, 204),
                SizingGrip = true
            };

            statusLabel = new ToolStripStatusLabel("Ready. Drag & drop XNB files to begin.")
            {
                ForeColor = Color.White,
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            progressBar = new ProgressBar
            {
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Visible = false
            };
            var progressHost = new ToolStripControlHost(progressBar)
            {
                Width = 200,
                Visible = false
            };

            fileCountLabel = new ToolStripStatusLabel("0 files")
            {
                ForeColor = Color.White,
                BorderSides = ToolStripStatusLabelBorderSides.Left,
                BorderStyle = Border3DStyle.Etched
            };

            statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel, progressHost, fileCountLabel });
            this.Controls.Add(statusStrip);

            // Set proper control order
            statusStrip.BringToFront();
            menuStrip.SendToBack();
        }

        private void SetupDragDrop()
        {
            this.AllowDrop = true;
            this.DragEnter += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    e.Effect = DragDropEffects.Copy;
            };
            this.DragDrop += (s, e) =>
            {
                string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                AddPaths(paths);
            };
        }

        #region File Management

        private void OpenFiles()
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "XNB Files (*.xnb)|*.xnb|All Files (*.*)|*.*";
                ofd.Multiselect = true;
                ofd.Title = "Select XNB files to convert";
                if (ofd.ShowDialog() == DialogResult.OK)
                    AddPaths(ofd.FileNames);
            }
        }

        private void OpenFolder()
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select folder containing XNB files";
                if (fbd.ShowDialog() == DialogResult.OK)
                    AddPaths(new[] { fbd.SelectedPath });
            }
        }

        private void AddPaths(string[] paths)
        {
            int added = 0;
            foreach (var path in paths)
            {
                if (Directory.Exists(path))
                {
                    var searchOpt = recursiveCheckBox.Checked
                        ? SearchOption.AllDirectories
                        : SearchOption.TopDirectoryOnly;
                    var files = Directory.GetFiles(path, "*.xnb", searchOpt);
                    foreach (var file in files)
                    {
                        if (AddFile(file)) added++;
                    }
                }
                else if (File.Exists(path) && path.EndsWith(".xnb", StringComparison.OrdinalIgnoreCase))
                {
                    if (AddFile(path)) added++;
                }
            }

            UpdateFileCount();
            SetStatus($"Added {added} file(s). Total: {loadedFiles.Count}");
        }

        private bool AddFile(string filePath)
        {
            string fullPath = Path.GetFullPath(filePath);
            if (loadedFiles.Contains(fullPath))
                return false;

            loadedFiles.Add(fullPath);
            var fi = new FileInfo(fullPath);
            var item = new ListViewItem(fi.Name);
            item.SubItems.Add(fi.DirectoryName);
            item.SubItems.Add(FormatSize(fi.Length));
            item.SubItems.Add("Ready");
            item.Tag = fullPath;
            item.ForeColor = Color.White;
            fileListView.Items.Add(item);
            return true;
        }

        private void ClearFiles()
        {
            loadedFiles.Clear();
            fileListView.Items.Clear();
            previewBox.Image?.Dispose();
            previewBox.Image = null;
            previewInfoLabel.Text = "Select an XNB file to preview\n\nDrag & Drop XNB files or folders here";
            UpdateFileCount();
            SetStatus("List cleared.");
        }

        private void RemoveSelected()
        {
            foreach (ListViewItem item in fileListView.SelectedItems)
            {
                loadedFiles.Remove((string)item.Tag);
                item.Remove();
            }
            UpdateFileCount();
        }

        private void SelectAll()
        {
            foreach (ListViewItem item in fileListView.Items)
                item.Selected = true;
        }

        private void UpdateFileCount()
        {
            fileCountLabel.Text = $"{loadedFiles.Count} file(s)";
        }

        private string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1048576) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / 1048576.0:F1} MB";
        }

        #endregion

        #region Preview

        private void FileListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (fileListView.SelectedItems.Count == 0) return;

            string filePath = (string)fileListView.SelectedItems[0].Tag;
            ShowPreview(filePath);
        }

        private void ShowPreview(string filePath)
        {
            try
            {
                var texture = XnbReader.ReadTexture(filePath);

                // Convert RGBA to Bitmap for preview
                var bmp = new Bitmap(texture.Width, texture.Height, PixelFormat.Format32bppArgb);
                var bmpData = bmp.LockBits(
                    new Rectangle(0, 0, texture.Width, texture.Height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format32bppArgb);

                // RGBA -> BGRA for GDI+
                byte[] bgraData = new byte[texture.PixelData.Length];
                for (int i = 0; i < texture.Width * texture.Height; i++)
                {
                    bgraData[i * 4 + 0] = texture.PixelData[i * 4 + 2]; // B
                    bgraData[i * 4 + 1] = texture.PixelData[i * 4 + 1]; // G
                    bgraData[i * 4 + 2] = texture.PixelData[i * 4 + 0]; // R
                    bgraData[i * 4 + 3] = texture.PixelData[i * 4 + 3]; // A
                }

                Marshal.Copy(bgraData, 0, bmpData.Scan0, bgraData.Length);
                bmp.UnlockBits(bmpData);

                previewBox.Image?.Dispose();
                previewBox.Image = bmp;

                string platformName = "Unknown";
                switch ((char)texture.PlatformId)
                {
                    case 'w': platformName = "Windows"; break;
                    case 'x': platformName = "Xbox"; break;
                    case 'm': platformName = "Phone"; break;
                    case 'a': platformName = "Android"; break;
                    case 'i': platformName = "iOS"; break;
                }

                previewInfoLabel.Text = $"{Path.GetFileName(filePath)}  |  " +
                    $"{texture.Width} × {texture.Height}  |  " +
                    $"Format: {texture.Format}  |  " +
                    $"Platform: {platformName}  |  " +
                    $"Mips: {texture.MipCount}  |  " +
                    $"Compressed: {((texture.Flags & (XnbFlags.LzxCompressed | XnbFlags.Lz4Compressed)) != 0 ? "Yes" : "No")}";
            }
            catch (Exception ex)
            {
                previewBox.Image?.Dispose();
                previewBox.Image = null;
                previewInfoLabel.Text = $"Error: {ex.Message}";
            }
        }

        private void PreviewBox_Paint(object sender, PaintEventArgs e)
        {
            // Draw checkerboard background for transparency
            if (previewBox.Image != null)
            {
                int checkSize = 10;
                var g = e.Graphics;
                for (int y = 0; y < previewBox.Height; y += checkSize)
                {
                    for (int x = 0; x < previewBox.Width; x += checkSize)
                    {
                        bool dark = ((x / checkSize) + (y / checkSize)) % 2 == 0;
                        using (var brush = new SolidBrush(dark ? Color.FromArgb(50, 50, 50) : Color.FromArgb(70, 70, 70)))
                        {
                            g.FillRectangle(brush, x, y, checkSize, checkSize);
                        }
                    }
                }
            }
        }

        #endregion

        #region Conversion

        private async void ConvertAll()
        {
            if (loadedFiles.Count == 0)
            {
                MessageBox.Show("No files loaded. Drag & drop XNB files or use File > Open.",
                    "No Files", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            await ConvertFiles(Enumerable.Range(0, fileListView.Items.Count).ToList());
        }

        private async void ConvertSelected()
        {
            var indices = new List<int>();
            foreach (int idx in fileListView.SelectedIndices)
                indices.Add(idx);

            if (indices.Count == 0)
            {
                MessageBox.Show("No files selected.",
                    "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            await ConvertFiles(indices);
        }

        private async void ConvertToFolder()
        {
            if (loadedFiles.Count == 0)
            {
                MessageBox.Show("No files loaded.", "No Files", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select output folder";
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    await ConvertFiles(
                        Enumerable.Range(0, fileListView.Items.Count).ToList(),
                        fbd.SelectedPath);
                }
            }
        }

        private async Task ConvertFiles(List<int> indices, string outputFolder = null)
        {
            cts = new CancellationTokenSource();
            var token = cts.Token;

            int total = indices.Count;
            int done = 0;
            int success = 0;
            int failed = 0;

            progressBar.Visible = true;
            progressBar.Value = 0;
            progressBar.Maximum = total;
            progressBar.Parent.Visible = true;

            SetStatus($"Converting {total} file(s)...");

            string ext = ImageWriter.GetExtension(selectedFormat);

            await Task.Run(() =>
            {
                foreach (int idx in indices)
                {
                    if (token.IsCancellationRequested) break;

                    string filePath = "";
                    try
                    {
                        this.Invoke(new Action(() =>
                        {
                            filePath = (string)fileListView.Items[idx].Tag;
                            fileListView.Items[idx].SubItems[3].Text = "Converting...";
                            fileListView.Items[idx].ForeColor = Color.Yellow;
                        }));

                        var texture = XnbReader.ReadTexture(filePath);

                        string outDir = outputFolder ?? Path.GetDirectoryName(filePath);
                        string outFile = Path.Combine(outDir,
                            Path.GetFileNameWithoutExtension(filePath) + ext);

                        if (!overwriteCheckBox.Checked && File.Exists(outFile))
                        {
                            this.Invoke(new Action(() =>
                            {
                                fileListView.Items[idx].SubItems[3].Text = "Skipped (exists)";
                                fileListView.Items[idx].ForeColor = Color.Gray;
                            }));
                            done++;
                            continue;
                        }

                        if (outDir != null && !Directory.Exists(outDir))
                            Directory.CreateDirectory(outDir);

                        ImageWriter.Save(outFile, texture.Width, texture.Height,
                            texture.PixelData, selectedFormat);

                        success++;
                        this.Invoke(new Action(() =>
                        {
                            fileListView.Items[idx].SubItems[3].Text =
                                $"✓ Done ({texture.Width}×{texture.Height})";
                            fileListView.Items[idx].ForeColor = Color.LightGreen;
                        }));
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        string errorMsg = ex.Message;
                        this.Invoke(new Action(() =>
                        {
                            fileListView.Items[idx].SubItems[3].Text = $"✗ Error: {errorMsg}";
                            fileListView.Items[idx].ForeColor = Color.Salmon;
                        }));
                    }

                    done++;
                    this.Invoke(new Action(() =>
                    {
                        progressBar.Value = done;
                        SetStatus($"Converting... {done}/{total} ({success} OK, {failed} failed)");
                    }));
                }
            });

            progressBar.Visible = false;
            progressBar.Parent.Visible = false;

            if (token.IsCancellationRequested)
                SetStatus($"Cancelled. {success} converted, {failed} failed.");
            else
                SetStatus($"Done! {success} converted, {failed} failed out of {total}.");

            if (success > 0 && failed == 0)
            {
                statusStrip.BackColor = Color.FromArgb(0, 160, 80);
            }
            else if (failed > 0)
            {
                statusStrip.BackColor = Color.FromArgb(200, 80, 40);
            }

            // Reset status bar color after a delay
            var timer = new System.Windows.Forms.Timer { Interval = 3000 };
            timer.Tick += (s, e) =>
            {
                statusStrip.BackColor = Color.FromArgb(0, 122, 204);
                timer.Stop();
                timer.Dispose();
            };
            timer.Start();
        }

        private void CancelConversion()
        {
            cts?.Cancel();
            SetStatus("Cancelling...");
        }

        #endregion

        #region Helpers

        private void SetStatus(string text)
        {
            statusLabel.Text = text;
        }

        private void ShowAbout()
        {
            MessageBox.Show(
                "XNB Exporter Pro v2.0\n\n" +
                "Enhanced XNB to PNG/BMP/TGA converter\n\n" +
                "Features:\n" +
                "• Batch conversion of XNB files\n" +
                "• Drag & Drop files and folders\n" +
                "• Preview with transparency\n" +
                "• Supports Color, DXT1, DXT3, DXT5, RGB565, BGRA5551, BGRA4444, Alpha8\n" +
                "• LZX and LZ4 decompression\n" +
                "• Output to PNG, BMP, or TGA\n" +
                "• No XNA/MonoGame/DirectX required\n\n" +
                "Based on XNBExporter by mediaexplorer74\n" +
                "Enhanced version — standalone, no dependencies",
                "About XNB Exporter Pro",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private Icon CreateAppIcon()
        {
            try
            {
                var bmp = new Bitmap(32, 32);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.FromArgb(0, 122, 204));

                    using (var brush = new SolidBrush(Color.White))
                    using (var font = new Font("Segoe UI", 12, FontStyle.Bold))
                    {
                        var sf = new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center
                        };
                        g.DrawString("X", font, brush, new RectangleF(0, 0, 32, 32), sf);
                    }
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }

    #region Dark Theme Renderer

    public class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Color.White;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected)
            {
                using (var brush = new SolidBrush(Color.FromArgb(62, 62, 64)))
                {
                    e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
                }
            }
            else
            {
                base.OnRenderMenuItemBackground(e);
            }
        }
    }

    public class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuStripGradientBegin => Color.FromArgb(45, 45, 48);
        public override Color MenuStripGradientEnd => Color.FromArgb(45, 45, 48);
        public override Color MenuItemBorder => Color.FromArgb(62, 62, 64);
        public override Color MenuBorder => Color.FromArgb(51, 51, 55);
        public override Color MenuItemSelected => Color.FromArgb(62, 62, 64);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(62, 62, 64);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(62, 62, 64);
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(27, 27, 28);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(27, 27, 28);
        public override Color ToolStripDropDownBackground => Color.FromArgb(27, 27, 28);
        public override Color ImageMarginGradientBegin => Color.FromArgb(27, 27, 28);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(27, 27, 28);
        public override Color ImageMarginGradientEnd => Color.FromArgb(27, 27, 28);
        public override Color SeparatorDark => Color.FromArgb(51, 51, 55);
        public override Color SeparatorLight => Color.FromArgb(51, 51, 55);
        public override Color StatusStripGradientBegin => Color.FromArgb(0, 122, 204);
        public override Color StatusStripGradientEnd => Color.FromArgb(0, 122, 204);
    }

    #endregion
}
