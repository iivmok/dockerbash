using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace dockerbash
{
    static class Program
    {
        public static bool failedStart;

        public static void ErrorOut(string message)
        {
            MessageBox.Show("Error: " + message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Program.failedStart = true;
        }

        [STAThread]
        static int Main()
        {
            Application.EnableVisualStyles();

            BashForm form = null;
            var psi = new ProcessStartInfo()
            {
                UseShellExecute = false,
                FileName = "docker",
                Arguments = "ps --format {{.ID}};{{.Names}};{{.Image}}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            Process docker = null;
            try
            {
                docker = Process.Start(psi);
            }
            catch (Win32Exception systemError)
            {
                if (systemError.NativeErrorCode == 2)
                {
                    ErrorOut("Docker not found.");
                    return 1;
                }
            }

            docker.WaitForExit();
            var output = docker.StandardOutput.ReadToEnd();
            var errors = docker.StandardError.ReadToEnd();
            if (errors.Length != 0)
            {
                ErrorOut(errors);
                return 1;
            }
            else
            {
                var containers = (
                    from container in output.Trim().Split('\n')
                    let x = container.Split(';')
                    let obj = new Container() { ID = x[0], Name = x[1], Image = x[2] }
                    select obj
                );
                form = new BashForm(containers);
            }

            if (!Program.failedStart)
            {
                int HighDpiMode_SystemAware = 1;
                var SetHighDpiMode = typeof(Application).GetMethod("SetHighDpiMode");
                if (SetHighDpiMode != null)
                {
                    SetHighDpiMode.Invoke(null, new Object[] { HighDpiMode_SystemAware });
                }
                Application.Run(form);
                return 0;
            }
            return 1;

        }
    }

    public class Container
    {
        public string ID, Name, Image;

        public bool HasBash
        {
            get
            {
                var psi = new ProcessStartInfo()
                {
                    UseShellExecute = false,
                    FileName = "docker",
                    Arguments = $"exec -it {this.ID} bash --version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                var docker = Process.Start(psi);
                docker.WaitForExit();

                var output = docker.StandardOutput.ReadToEnd();
                var errors = docker.StandardError.ReadToEnd();

                if (errors.Length == 0 && docker.ExitCode == 0 && output.ToLower().Contains("bash"))
                    return true;

                return false;
            }
        }
    }
    public partial class BashForm : Form
    {
        int currentTop = 0;
        int maxNameLength = 0;

        static readonly Color dark = Color.FromArgb(30, 30, 30);
        static readonly Color light = Color.FromArgb(224, 224, 224);
        public void AddEntry(Container container)
        {

            var bashButton = new Button();
            //looks
            bashButton.Width = 600;
            bashButton.Height = 80;
            bashButton.Location = new Point(0, currentTop);
            currentTop += 79;
            bashButton.TextAlign = ContentAlignment.MiddleLeft;
            bashButton.Padding = new Padding(20, 0, 0, 0);
            bashButton.FlatAppearance.BorderColor = light;
            bashButton.FlatStyle = FlatStyle.Flat;
            bashButton.FlatAppearance.MouseOverBackColor = light;
            bashButton.MouseEnter += (x, _) => ((Button)x).ForeColor = dark;
            bashButton.MouseLeave += (x, _) => ((Button)x).ForeColor = light;

            bashButton.Text = container.Name.PadRight(maxNameLength + 2) + container.Image;

            bashButton.Click += bashButtonClick;

            bashButton.Tag = container;

            this.Controls.Add(bashButton);
        }

        public BashForm(IEnumerable<Container> containers)
        {

            maxNameLength = containers.Select(x => x.Name.Length).Max();
            containers.ToList().ForEach(x => {
                if (x.HasBash)
                    AddEntry(x);
            });

            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.BackColor = dark;
            this.ForeColor = light;
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "dockerbash";
            this.TopMost = true;
            this.Font = new Font("Consolas", 12);

            this.Focus();
            this.Deactivate += Deactivated;

        }

        private void Deactivated(object sender, EventArgs e)
        {
            this.Close();
        }

        private void bashButtonClick(object sender, EventArgs e)
        {
            var container = (Container)((Button)sender).Tag;
            Process.Start("mintty", $"-t \"{container.Name}\" -e winpty docker exec -it {container.ID} bash");
            this.Close();
        }
    }
}
