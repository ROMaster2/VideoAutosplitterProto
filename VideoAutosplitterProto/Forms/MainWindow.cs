using Accord.Video;
using Accord.Video.DirectShow;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VideoAutosplitterProto.Models;

namespace VideoAutosplitterProto.Forms
{
    public partial class MainWindow : Form
    {
        public MainWindow()
        {
            InitializeComponent();
            fillBoxCaptureDevice();
            // !DEBUGGING!
            //Scanner.GameProfile = GameProfile.FromXml(@"C:\Users\Administrator\Pictures\VID\pkmnsnap_features\pkmnsnap.xml");
            //txtGameProfile.Text = @"C:\Users\Administrator\Pictures\VID\pkmnsnap_features\pkmnsnap.xml";
            TryStart();
        }

        private void MainWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            Scanner.Stop();
            Application.Exit();
        }

        private void btnGameProfile_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog() { Filter = "XML Files|*.xml", Title = "Load a Game Profile" } )
            {
                if (ofd.ShowDialog() == DialogResult.OK && ofd.CheckFileExists == true)
                {
                    retry:
                    var gp = GameProfile.FromXml(ofd.FileName);

                    if (gp == null)
                    {
                        DialogResult dr = MessageBox.Show(
                            "Failed to load Game Profile.",
                            "Error",
                            MessageBoxButtons.RetryCancel,
                            MessageBoxIcon.Error
                            );

                        if (dr == DialogResult.Retry)
                        {
                            goto retry;
                        }
                    }
                    else
                    {
                        Scanner.GameProfile = gp;
                        txtGameProfile.Text = ofd.FileName;
                        TryStart();
                    }
                }
            }
        }

        private void btnCaptureDevice_Click(object sender, EventArgs e)
        {
            retry:
            var success = fillBoxCaptureDevice();
            if (!success)
            {
                DialogResult dr = MessageBox.Show(
                    "No video capture devices detected.",
                    "Error",
                    MessageBoxButtons.RetryCancel,
                    MessageBoxIcon.Error
                    );

                if (dr == DialogResult.Retry)
                {
                    goto retry;
                }
            }
        }

        private bool fillBoxCaptureDevice()
        {
            var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

            if (videoDevices.Count > 0)
            {
                boxCaptureDevice.Enabled = true;
                string selectedItem = null;
                int selectedIndex = 0;
                if (boxCaptureDevice.SelectedIndex > -1)
                {
                    selectedItem = (string)boxCaptureDevice.SelectedItem;
                }

                boxCaptureDevice.Items.Clear();
                for (var i = 0; i < videoDevices.Count; i++)
                {
                    boxCaptureDevice.Items.Add(videoDevices[i].Name);
                    if (videoDevices[i].Name == selectedItem)
                    {
                        selectedIndex = i;
                    }
                }
                boxCaptureDevice.SelectedIndex = selectedIndex;
                return true;
            }
            else
            {
                boxCaptureDevice.Items.Clear();
                boxCaptureDevice.Enabled = false;
                return false;
            }
        }

        private void boxCaptureDevice_SelectedIndexChanged(object sender, EventArgs e)
        {
            Scanner.Stop();
            retry:
            var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            var matches = videoDevices.Where(v => v.Name == boxCaptureDevice.Text);
            if (matches.Count() > 0)
            {
                var match = matches.First();
                Scanner.SetVideoSource(match.MonikerString);
                lblCaptureDevice.Text = "Capture Device - " + Scanner.VideoGeometry.ToString();
            }
            else
            {
                lblCaptureDevice.Text = "Capture Device";
                DialogResult dr = MessageBox.Show(
                    "Selected video capture device cannont be found. Has it been unplugged?",
                    "Error",
                    MessageBoxButtons.RetryCancel,
                    MessageBoxIcon.Error
                    );

                if (dr == DialogResult.Retry)
                {
                    goto retry;
                }
                else
                {
                    fillBoxCaptureDevice();
                }
            }
            TryStart();
        }
        
        private void TryStart()
        {
            if (Scanner.GameProfile != null && Scanner.IsVideoSourceValid())
            {
                Scanner.Start();
                //btnTryAutoFit.Enabled = true;
            }
            else
            {
                //btnTryAutoFit.Enabled = false;
            }
        }

        private void btnSetCaptureRegion_Click(object sender, EventArgs e)
        {
            Aligner w = new Aligner();
            w.ShowDialog();
        }

    }
}
