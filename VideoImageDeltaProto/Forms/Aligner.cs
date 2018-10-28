using ImageMagick;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using VideoImageDeltaProto;
using VideoImageDeltaProto.Models;

using Screen = VideoImageDeltaProto.Models.Screen;

namespace VideoImageDeltaProto.Forms
{
    public partial class Aligner : Form
    {
        // Tried to add event handling to Scanner, but it refused all attempts.
        // Asynchronous programming has to be the LEAST intuitive thing I've ever encountered.

        private const double DEFAULT_MOVE_DISTANCE = 1;
        private const double ALT_DISTANCE_MODIFIER = 10;
        private const double SHIFT_DISTANCE_MODIFIER = 10;
        private const double CONTROL_DISTANCE_MODIFIER = 0.1;

        private const Gravity STANDARD_GRAVITY = Gravity.Northwest;
        private const FilterType DEFAULT_SCALE_FILTER = FilterType.Lanczos;
        private static readonly MagickColor EXTENT_COLOR = MagickColor.FromRgba(255, 0, 255, 127);

        private static readonly Geometry MIN_VALUES = new Geometry(-Scanner.VideoGeometry.Width, -Scanner.VideoGeometry.Height, 4, 4);
        private static readonly Geometry MAX_VALUES = new Geometry(
            Scanner.VideoGeometry.Width,
            Scanner.VideoGeometry.Height,
            Scanner.VideoGeometry.Width * 2,
            Scanner.VideoGeometry.Height * 2);

        public Aligner()
        {
            InitializeComponent();
            SetAllNumValues(Scanner.CropGeometry);
            FillDdlWatchZone();
        }

        private void Aligner_ResizeEnd(object sender, EventArgs e)
        {
            if (ThumbnailBox.Image != null)
            {
                RefreshThumbnail();
            }
        }

        // If the window is mazimized or restored, do thing.
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x0112)
            {
                if (m.WParam == new IntPtr(0xF030) || m.WParam == new IntPtr(0xF120))
                {
                    RefreshThumbnail();
                }
            }
        }

        private Geometry GetScaledGeometry(Geometry refGeo)
        {
            var referenceWidth = refGeo.IsBlank ? Scanner.CropGeometry.Width : refGeo.Width;
            var referenceHeight = refGeo.IsBlank ? Scanner.CropGeometry.Height : refGeo.Height;

            // Tried to not have hardcoded, but Microsoft diagreed.
            var parent = (TableLayoutPanel)ThumbnailBox.Parent;
            var parentWidth = parent.Width - 200;
            var parentHeight = parent.Height;
            var xMargin = ThumbnailBox.Margin.Left + ThumbnailBox.Margin.Right;
            var yMargin = ThumbnailBox.Margin.Top + ThumbnailBox.Margin.Bottom;
            var xRatio = (parentWidth - xMargin) / referenceWidth;
            var yRatio = (parentHeight - yMargin) / referenceHeight;

            var ratio = Math.Min(Math.Min(1, xRatio), yRatio);
            var width = Math.Max(referenceWidth * ratio, 1);
            var height = Math.Max(referenceHeight * ratio, 1);
            return new Geometry(width, height);
        }

        private void RefreshThumbnail()
        {
            Geometry minGeo = Geometry.Min(Scanner.CropGeometry, GetScaledGeometry(Geometry.Blank));
            MagickImage mi = null;
            retry:
            try
            {
                mi = new MagickImage((Bitmap)Scanner.CurrentFrame.Bitmap.Clone());
            }
            catch (Exception) { goto retry; }
            mi.ColorSpace = ColorSpace.RGB;
            if (Scanner.NeedExtent)
            {
                mi.Extent(Scanner.CropGeometry.ToMagick(), STANDARD_GRAVITY, EXTENT_COLOR);
            }
            else
            {
                mi.Crop(Scanner.CropGeometry.ToMagick(), STANDARD_GRAVITY);
            }
            mi.RePage();

            if (DdlWatchZone.SelectedIndex > 0)
            {
                var wi = (WatchImage)DdlWatchZone.SelectedItem;
                var tGeo = wi.Parent.Parent.ThumbnailGeometry.Clone();

                var baseMGeo = new MagickGeometry(100, 100, (int)Math.Round(tGeo.Width), (int)Math.Round(tGeo.Height));

                tGeo.Update(-100, -100, 200, 200);
                mi.Extent(tGeo.ToMagick(), STANDARD_GRAVITY, EXTENT_COLOR);

                using (var baseM = new MagickImage(
                    MagickColor.FromRgba(0, 0, 0, 0),
                    baseMGeo.Width,
                    baseMGeo.Height))
                using (var overlay = new MagickImage(
                        MagickColor.FromRgba(170, 170, 170, 223),
                        baseMGeo.Width + 200,
                        baseMGeo.Height + 200))
                {
                    baseM.ColorSpace = ColorSpace.RGB;
                    overlay.ColorSpace = ColorSpace.RGB;
                    overlay.Composite(baseM, new PointD(baseMGeo.X, baseMGeo.Y), CompositeOperator.Alpha);
                    mi.Composite(overlay, CompositeOperator.Atop);
                }
                mi.RePage();

                minGeo = minGeo.Min(GetScaledGeometry(tGeo));

                if (CkbViewDelta.Checked)
                {
                    wi.SetMagickImage(Scanner.ExtraPrecision);
                    using (var deltaImage = wi.MagickImage.Clone())
                    {
                        deltaImage.Write(@"E:\fuck0.png");
                        deltaImage.ColorSpace = ColorSpace.RGB;
                        mi.Crop(baseMGeo, STANDARD_GRAVITY);
                        mi.Alpha(AlphaOption.Off); // Why is this necessary? It wasn't necessary before.
                        deltaImage.Equalize();
                        mi.Equalize();
                        deltaImage.RePage();
                        mi.RePage();
                        mi.Write(@"E:\fuck1.png");
                        deltaImage.Write(@"E:\fuck2.png");
                        double delta1 = mi.Compare(mi, ErrorMetric.NormalizedCrossCorrelation);
                        double delta2 = deltaImage.Compare(deltaImage, ErrorMetric.PeakSignalToNoiseRatio);
                        LblDeltas.Text =
                            mi.Compare(deltaImage, ErrorMetric.PeakSignalToNoiseRatio).ToString("0.####") + "\r\n" +
                            mi.Compare(deltaImage, ErrorMetric.NormalizedCrossCorrelation).ToString("0.####") + "\r\n" +
                            mi.Compare(deltaImage, ErrorMetric.Absolute).ToString("0.####") + "\r\n" +
                            mi.Compare(deltaImage, ErrorMetric.Fuzz).ToString("0.####") + "\r\n" +
                            mi.Compare(deltaImage, ErrorMetric.MeanAbsolute).ToString("0.####") + "\r\n" +
                            mi.Compare(deltaImage, ErrorMetric.MeanSquared).ToString("0.####") + "\r\n" +
                            mi.Compare(deltaImage, ErrorMetric.StructuralDissimilarity).ToString("0.####") + "\r\n" +
                            mi.Compare(deltaImage, ErrorMetric.StructuralSimilarity).ToString("0.####");
                        mi.Composite(deltaImage, CompositeOperator.Difference);
                        mi.Write(@"E:\fuck3.png");
                    }

                    minGeo = minGeo.Min(GetScaledGeometry(wi.Parent.Parent.ThumbnailGeometry));
                }
            }

            if (mi.Width > minGeo.Size.ToDrawing().Width || mi.Height > minGeo.Size.ToDrawing().Height)
            {
                var mGeo = minGeo.ToMagick();
                mGeo.IgnoreAspectRatio = false;
                mi.FilterType = DEFAULT_SCALE_FILTER;
                mi.Resize(mGeo);
            }

            ThumbnailBox.Size = minGeo.Size.ToDrawing();
            ThumbnailBox.Image = mi.ToBitmap();
        }

        private void BtnRefreshFrame_Click(object sender, EventArgs e) => RefreshThumbnail();

        private void BtnTryAutoAlign_Click(object sender, EventArgs e)
        {
            retry:
            try
            {
                using (var haystack = (Bitmap)Scanner.CurrentFrame.Bitmap.Clone())
                using (var needle = (Bitmap)Scanner.GameProfile.Screens[0].Autofitter.Image.Clone())
                {
                    var geo = Test2.Run(needle, haystack);
                    SetAllNumValues(geo.Min(MAX_VALUES).Max(MIN_VALUES));
                }
            }
            catch (Exception) { goto retry; }
        }

        #region Numeric Field Logic/Events

        private void UpdateCropGeometry(Geometry? geo = null)
        {
            Geometry newGeo = geo ?? Geometry.Blank;
            if (geo == null)
            {
                newGeo.X = (double)NumX.Value;
                newGeo.Y = (double)NumY.Value;
                newGeo.Width = (double)NumWidth.Value;
                newGeo.Height = (double)NumHeight.Value;
            }
            Scanner.CropGeometry = newGeo.Min(MAX_VALUES).Max(MIN_VALUES);
            Scanner.UpdateCropGeometry();
            RefreshThumbnail();
        }

        private void SetAllNumValues(Geometry geo)
        {
            NumX.Value = (decimal)geo.X;
            NumY.Value = (decimal)geo.Y;
            NumWidth.Value = (decimal)geo.Width;
            NumHeight.Value = (decimal)geo.Height;
            UpdateCropGeometry(geo);
        }

        // Validated triggers when the user manually the value, rather than anytime it changes.
        private void NumX_Validated(object sender, EventArgs e) => UpdateCropGeometry();
        private void NumY_Validated(object sender, EventArgs e) => UpdateCropGeometry();
        private void NumWidth_Validated(object sender, EventArgs e) => UpdateCropGeometry();
        private void NumHeight_Validated(object sender, EventArgs e) => UpdateCropGeometry();

        #endregion Numeric Field Logic/Events

        #region DPad Logic

        private int GetDPadID(object sender)
        {
            // Technically hacky but it makes for less methods.
            var name = ((PictureBox)sender).Name;
            return int.Parse(name.Substring(name.Length - 1, 1));
        }

        private void ShowSelectedDPad(PictureBox sender, bool selected, bool reverse)
        {
            int i = GetDPadID(sender);
            // Todo: Un-hardcode...somehow
            string imgName = "DPad" + (reverse ? "N" : null) + (selected ? "S" : null) + i.ToString();
            sender.Image = (Bitmap)Properties.Resources.ResourceManager.GetObject(imgName);
        }

        private double GetDistanceModifier()
        {
            double dist = DEFAULT_MOVE_DISTANCE; // 1 pixel
            if (ModifierKeys.HasFlag(Keys.Alt) && ModifierKeys.HasFlag(Keys.Shift))
                dist *= ALT_DISTANCE_MODIFIER * SHIFT_DISTANCE_MODIFIER; // 100 pixels
            else if (ModifierKeys.HasFlag(Keys.Control) && ModifierKeys.HasFlag(Keys.Shift))
                dist *= CONTROL_DISTANCE_MODIFIER / SHIFT_DISTANCE_MODIFIER; // 0.01 pixels
            else if (ModifierKeys.HasFlag(Keys.Control))
                dist *= CONTROL_DISTANCE_MODIFIER; // 0.1 pixels
            else if (ModifierKeys.HasFlag(Keys.Shift))
                dist *= SHIFT_DISTANCE_MODIFIER; // 10 pixels
            return dist;
        }

        // I can't think of good names for what's used in the offsets/resizers. I can hardly explain them.
        // This could probably go in Geometry as a method anyway.
        private void AdjustCrop(PictureBox sender, double o1, double o2, double r1, double r2)
        {
            int i = GetDPadID(sender);
            var geo = Scanner.CropGeometry.Clone();

            switch (i)
            {
                case 1: geo.Offset(o1, o1); geo.Resize(r1, r1); break;
                case 2: geo.Offset( 0, o1); geo.Resize( 0, r1); break;
                case 3: geo.Offset(o2, o1); geo.Resize(r2, r1); break;
                case 4: geo.Offset(o1,  0); geo.Resize(r1,  0); break;
                case 6: geo.Offset(o2,  0); geo.Resize(r2,  0); break;
                case 7: geo.Offset(o1, o2); geo.Resize(r1, r2); break;
                case 8: geo.Offset( 0, o2); geo.Resize( 0, r2); break;
                case 9: geo.Offset(o2, o2); geo.Resize(r2, r2); break;
                default: break;
            }

            SetAllNumValues(geo.Min(MAX_VALUES).Max(MIN_VALUES));
        }

        #endregion DPad Logic

        #region DPad Events

        private void MoveDPad_MouseUp(object sender, MouseEventArgs e)   => ShowSelectedDPad((PictureBox)sender, false, false);
        private void GrowDPad_MouseUp(object sender, MouseEventArgs e)   => ShowSelectedDPad((PictureBox)sender, false, false);
        private void ShrinkDPad_MouseUp(object sender, MouseEventArgs e) => ShowSelectedDPad((PictureBox)sender, false, true);

        private void MoveDPad_MouseDown(object sender, MouseEventArgs e)
        {
            double dist = GetDistanceModifier();
            ShowSelectedDPad((PictureBox)sender, true, false);
            AdjustCrop((PictureBox)sender, -dist, dist, 0, 0);
        }

        private void GrowDPad_MouseDown(object sender, MouseEventArgs e)
        {
            double dist = GetDistanceModifier();
            ShowSelectedDPad((PictureBox)sender, true, false);
            AdjustCrop((PictureBox)sender, -dist, 0, dist, dist);
        }

        private void ShrinkDPad_MouseDown(object sender, MouseEventArgs e)
        {
            double dist = GetDistanceModifier();
            ShowSelectedDPad((PictureBox)sender, true, true);
            AdjustCrop((PictureBox)sender, dist, 0, -dist, -dist);
        }

        #endregion DPad Events

        private void BtnResetRegion_Click(object sender, EventArgs e)
        {
            var geo = Scanner.ResetCropGeometry();
            SetAllNumValues(geo);
        }

        private void FillDdlWatchZone()
        {
            DdlWatchZone.Items.Add("<None>");
            foreach (var wi in Scanner.GameProfile.WatchImages)
            {
                wi.SetName((Screen)wi.Parent.Parent.Parent, (WatchZone)wi.Parent.Parent, (Watcher)wi.Parent);
                DdlWatchZone.Items.Add(wi);
            }
            DdlWatchZone.SelectedIndex = 0;
        }

        private void DdlWatchZone_SelectedIndexChanged(object sender, EventArgs e) => RefreshThumbnail();

        private void CkbViewDelta_CheckedChanged(object sender, EventArgs e) => RefreshThumbnail();

        private void CkbUseExtraPrecision_CheckedChanged(object sender, EventArgs e)
        {
            Scanner.ExtraPrecision = CkbUseExtraPrecision.Checked;
            RefreshThumbnail();
        }
    }
}
