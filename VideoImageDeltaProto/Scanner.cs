using Accord.Video;
using Accord.Video.DirectShow;
using ImageMagick;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using VideoImageDeltaProto.Forms;
using VideoImageDeltaProto.Models;
using System.IO;
using System.Linq;

using Size = System.Drawing.Size;

namespace VideoImageDeltaProto
{
    static class Scanner
    {
        public static GameProfile GameProfile = null;
        private static VideoCaptureDevice VideoSource = new VideoCaptureDevice();

        public static Frame CurrentFrame = Frame.Blank;
        public static List<Scan> ScanBag = new List<Scan>();
        public static DateTime MostRecentFrameTime = DateTime.Now;
        public static int CurrentIndex = 0;

        public static bool ExtraPrecision = false;

        private static Geometry _VideoGeometry = Geometry.Blank;
        public static Geometry VideoGeometry
        {
            get
            {
                // Hack method because async sucks
                if (_VideoGeometry.IsBlank && !IsVideoSourceRunning() && IsVideoSourceValid())
                {
                    VideoSource.Start();
                    while (_VideoGeometry.IsBlank) Thread.Sleep(1);
                }
                return _VideoGeometry;
            }
        }

        private static Geometry _CropGeometry = Geometry.Blank;
        public static Geometry CropGeometry
        {
            get
            {
                if (_CropGeometry.IsBlank)
                {
                    _CropGeometry = VideoGeometry;
                }
                return _CropGeometry;
            }
            set
            {
                _CropGeometry = value;
                UpdateCropGeometry();
            }
        }

        // Bad name.
        private static Geometry _TrueCropGeometry = Geometry.Blank;
        public static Geometry TrueCropGeometry
        {
            get
            {
                if (_TrueCropGeometry.IsBlank)
                {
                    if (GameProfile != null)
                    {
                        double x = 32768d;
                        double y = 32768d;
                        double width = -32768d;
                        double height = -32768d;
                        foreach (var wz in GameProfile.Screens[0].WatchZones)
                        {
                            var geo = wz.Geometry;
                            geo.RemoveAnchor(wz.Parent.Geometry);
                            x = Math.Min(x, geo.X);
                            y = Math.Min(y, geo.Y);
                            width = Math.Max(width, geo.X + geo.Width);
                            height = Math.Max(height, geo.Y + geo.Height);
                        }
                        width -= x;
                        height -= y;
                        var sGeo = new Geometry(x, y, width, height);
                        sGeo.ResizeTo(CropGeometry, GameProfile.Screens[0].Geometry);
                        sGeo.Update(CropGeometry.X, CropGeometry.Y);
                        _TrueCropGeometry = sGeo;
                    }
                    else
                    {
                        _TrueCropGeometry = CropGeometry;
                    }
                }
                return _TrueCropGeometry;
            }
        }

        private static bool? _NeedExtent;
        public static bool NeedExtent
        {
            get
            {
                if (_NeedExtent == null)
                {
                    _NeedExtent = !_VideoGeometry.Contains(TrueCropGeometry);
                }
                return (bool)_NeedExtent;
            }
        }

        public static void Stop()
        {
            ScanBag.Clear();
            MostRecentFrameTime = DateTime.Now;
            CurrentIndex = 0;
            VideoSource.Stop();
        }

        public static void Start()
        {
            if (GameProfile != null)
            {
                ScanBag.Clear();
                MostRecentFrameTime = DateTime.Now;
                CurrentIndex = 0;
                UpdateCropGeometry();
                VideoSource.Start();
            }
        }

        public static bool IsVideoSourceValid()
        {
            return !string.IsNullOrEmpty(VideoSource.Source);
        }

        public static bool IsVideoSourceRunning()
        {
            return VideoSource.IsRunning;
        }

        public static void SetVideoSource(string monikerString)
        {
            _VideoGeometry = Geometry.Blank;
            VideoSource.Source = monikerString;
            SubscribeToFrameHandler(new NewFrameEventHandler(HandleNewFrame));
            ResetCropGeometry();
        }

        public static void SubscribeToFrameHandler(NewFrameEventHandler method)
        {
            VideoSource.NewFrame += method;
        }

        public static void UnsubscribeToFrameHandler(NewFrameEventHandler method)
        {
            VideoSource.NewFrame -= method;
        }

        public static Geometry ResetCropGeometry()
        {
            _CropGeometry = Geometry.Blank;
            UpdateCropGeometry();
            return CropGeometry;
        }

        public static void UpdateCropGeometry()
        {
            _NeedExtent = null;
            _TrueCropGeometry = Geometry.Blank;
            if (GameProfile != null)
            {
                int i = 0;
                foreach (var wz in GameProfile.Screens[0].WatchZones)
                {
                    var g = wz.Geometry;
                    g.RemoveAnchor(wz.Parent.Geometry);
                    g.ResizeTo(CropGeometry, GameProfile.Screens[0].Geometry);
                    wz.ThumbnailGeometry = g;

                    foreach (var wi in wz.WatchImages)
                    {
                        wi.SetMagickImage(ExtraPrecision);
                        wi.Index = i;
                        i++;
                    }
                }
            }
        }

        public static void HandleNewFrame(object sender, NewFrameEventArgs e)
        {
            var now = DateTime.Now;
            var newScan = new Scan(new Frame(now, (Bitmap)e.Frame.Clone()), CurrentFrame.Clone());
            CurrentFrame = new Frame(now, (Bitmap)e.Frame.Clone());
            CurrentIndex++;

            if (_VideoGeometry.Size != e.Frame.Size.ToWindows()) _VideoGeometry = new Geometry(e.Frame.Size.ToWindows());

            if (GameProfile != null)
            {
                Run2(newScan);
            }

        }

        // Todo: Make into array returning task
        public static void Run2(Scan scan)
        {
            using (var fileImageBase = new MagickImage((Bitmap)scan.CurrentFrame.Bitmap))
            {
                if (NeedExtent)
                {
                    fileImageBase.Extent(TrueCropGeometry.ToMagick(), Gravity.Northwest, MagickColor.FromRgba(0, 0, 0, 0));
                }
                else
                {
                    fileImageBase.Crop(TrueCropGeometry.ToMagick(), Gravity.Northwest);
                }
                fileImageBase.RePage();
                if (CurrentIndex % 300 == 0) fileImageBase.Write(@"E:\test6.png");
                foreach (var wz in GameProfile.Screens[0].WatchZones)
                {
                    var tmp = wz.ThumbnailGeometry;
                    tmp.Update(-TrueCropGeometry.X, -TrueCropGeometry.Y);
                    var mg = tmp.ToMagick();
                    using (var fileImageCropped = (MagickImage)fileImageBase.Clone())
                    {
                        fileImageCropped.Crop(mg, wz.ThumbnailGeometry.Anchor.ToGravity());
                        if (CurrentIndex % 300 == 0) fileImageCropped.Write(@"E:\test5.png");
                        foreach (var w in wz.Watches)
                        {
                            fileImageCropped.ColorSpace = w.ColorSpace; // Can safely change since it doesn't affect pixel data.
                            // May need to update to support multiple channels.
                            using (var fileImageComposed = Untitled(fileImageCropped, w.Channel))
                            {
                                if (CurrentIndex % 300 == 0) fileImageComposed.Write(@"E:\test4.png");
                                if (!w.DupeFrameCheck)
                                {
                                    foreach (var wi in w.WatchImages)
                                    {
                                        using (var deltaImage = wi.MagickImage.Clone())
                                        using (var fileImageCompare = (MagickImage)fileImageComposed.Clone())
                                        {
                                            //if (deltaImage.HasAlpha)
                                            //{
                                            //    fileImageCompare.Composite(deltaImage, CompositeOperator.CopyAlpha);
                                            //}
                                            deltaImage.ColorSpace = w.ColorSpace;
                                            var a = (float)deltaImage.Compare(fileImageCompare,
                                                ErrorMetric.PeakSignalToNoiseRatio);
                                            Interlocked.Exchange(ref Program.floatArray[wi.Index], a);

                                            if (CurrentIndex % 300 == 0)
                                            {
                                                fileImageCompare.Write(@"E:\test2.png");
                                                deltaImage.Write(@"E:\test3.png");
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    // Put PreviousFrame clones in proper spots.
                                    // it's just kind of unoptimial for non-dupe checkers
                                    using (var deltaImagePre = new MagickImage((Bitmap)scan.PreviousFrame.Bitmap.Clone()))
                                    using (var fileImageCompare = (MagickImage)fileImageComposed.Clone())
                                    {
                                        if (NeedExtent)
                                        {
                                            deltaImagePre.Extent(TrueCropGeometry.ToMagick(), Gravity.Northwest, MagickColor.FromRgba(0, 0, 0, 0));
                                        }
                                        else
                                        {
                                            deltaImagePre.Crop(TrueCropGeometry.ToMagick(), Gravity.Northwest);
                                        }
                                        deltaImagePre.RePage();
                                        deltaImagePre.Crop(mg, wz.ThumbnailGeometry.Anchor.ToGravity());
                                        deltaImagePre.ColorSpace = w.ColorSpace;
                                        using (var deltaImage = (w.Channel > -1) ? (MagickImage)deltaImagePre.Clone() :
                                            (MagickImage)deltaImagePre.Clone().Separate().ToArray()[w.Channel])
                                        {
                                            if (CurrentIndex % 300 == 0)
                                            {
                                                deltaImagePre.Write(@"E:\test9.png");
                                            }

                                            var a = (float)deltaImagePre.Compare(fileImageCompare,
                                                ErrorMetric.PeakSignalToNoiseRatio);
                                            Interlocked.Exchange(ref Program.floatArray[18], a);


                                            if (CurrentIndex % 300 == 0)
                                            {
                                                fileImageCompare.Write(@"E:\test2.png");
                                                deltaImagePre.Write(@"E:\test3.png");
                                                fileImageComposed.Write(@"E:\test4.png");
                                                fileImageCropped.Write(@"E:\test5.png");
                                                fileImageBase.Write(@"E:\test6.png");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            scan.Clean();
        }

        // Todo: Make into array returning task
        public static void Run(Scan scan)
        {
            using (var fileImageBase = new MagickImage((Bitmap)scan.CurrentFrame.Bitmap.Clone()))
            {
                if (NeedExtent)
                {
                    fileImageBase.Extent(TrueCropGeometry.ToMagick(), Gravity.Northwest, MagickColor.FromRgba(0, 0, 0, 0));
                }
                else
                {
                    fileImageBase.Crop(TrueCropGeometry.ToMagick(), Gravity.Northwest);
                }
                fileImageBase.RePage();
                if (CurrentIndex % 300 == 0) fileImageBase.Write(@"E:\test6.png");
                foreach (var wz in GameProfile.Screens[0].WatchZones)
                {
                    var tmp = wz.ThumbnailGeometry;
                    tmp.Update(-TrueCropGeometry.X, -TrueCropGeometry.Y);
                    var mg = tmp.ToMagick();
                    using (var fileImageCropped = (MagickImage)fileImageBase.Clone())
                    {
                        fileImageCropped.Crop(mg, wz.ThumbnailGeometry.Anchor.ToGravity());
                        if (CurrentIndex % 300 == 0) fileImageCropped.Write(@"E:\test5.png");
                        foreach (var w in wz.Watches)
                        {
                            fileImageCropped.ColorSpace = w.ColorSpace; // Can safely change since it doesn't affect pixel data.
                            // May need to update to support multiple channels.
                            using (var fileImageComposed = Untitled(fileImageCropped, w.Channel))
                            {
                                if (CurrentIndex % 300 == 0) fileImageComposed.Write(@"E:\test4.png");
                                if (!w.DupeFrameCheck)
                                {
                                    foreach (var wi in w.WatchImages)
                                    {
                                        using (var deltaImage = wi.MagickImage.Clone())
                                        using (var fileImageCompare = (MagickImage)fileImageComposed.Clone())
                                        {
                                            //if (deltaImage.HasAlpha)
                                            //{
                                            //    fileImageCompare.Composite(deltaImage, CompositeOperator.CopyAlpha);
                                            //}
                                            deltaImage.ColorSpace = w.ColorSpace;
                                            var a = (float)deltaImage.Compare(fileImageCompare,
                                                ErrorMetric.PeakSignalToNoiseRatio);
                                            Interlocked.Exchange(ref Program.floatArray[wi.Index], a);

                                            if (CurrentIndex % 300 == 0)
                                            {
                                                fileImageCompare.Write(@"E:\test2.png");
                                                deltaImage.Write(@"E:\test3.png");
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    // Put PreviousFrame clones in proper spots.
                                    // it's just kind of unoptimial for non-dupe checkers
                                    using (var deltaImagePre = new MagickImage((Bitmap)scan.PreviousFrame.Bitmap.Clone()))
                                    using (var fileImageCompare = (MagickImage)fileImageComposed.Clone())
                                    {
                                        if (NeedExtent)
                                        {
                                            deltaImagePre.Extent(TrueCropGeometry.ToMagick(), Gravity.Northwest, MagickColor.FromRgba(0, 0, 0, 0));
                                        }
                                        else
                                        {
                                            deltaImagePre.Crop(TrueCropGeometry.ToMagick(), Gravity.Northwest);
                                        }
                                        deltaImagePre.RePage();
                                        deltaImagePre.Crop(mg, wz.ThumbnailGeometry.Anchor.ToGravity());
                                        deltaImagePre.ColorSpace = w.ColorSpace;
                                        using (var deltaImage = (w.Channel > -1) ? (MagickImage)deltaImagePre.Clone() :
                                            (MagickImage)deltaImagePre.Clone().Separate().ToArray()[w.Channel])
                                        {
                                            if (CurrentIndex % 300 == 0)
                                            {
                                                deltaImagePre.Write(@"E:\test9.png");
                                            }

                                            var a = (float)deltaImagePre.Compare(fileImageCompare,
                                                ErrorMetric.PeakSignalToNoiseRatio);
                                            Interlocked.Exchange(ref Program.floatArray[18], a);


                                            if (CurrentIndex % 300 == 0)
                                            {
                                                fileImageCompare.Write(@"E:\test2.png");
                                                deltaImagePre.Write(@"E:\test3.png");
                                                fileImageComposed.Write(@"E:\test4.png");
                                                fileImageCropped.Write(@"E:\test5.png");
                                                fileImageBase.Write(@"E:\test6.png");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            scan.Clean();
        }

        private static MagickImage Untitled(MagickImage input, sbyte channel)
        {
            MagickImage mi = (MagickImage)input.Clone();
            if (channel > -1)
            {
                mi = (MagickImage)mi.Separate().ToArray()[channel];
            }
            return mi;
        }

        public static void RunOld()
        {
            while (true)
            {
                if (GameProfile != null && VideoSource.IsRunning) // Can't really become unset, so only check once.
                {
                    UpdateCropGeometry();
                    while (VideoSource.IsRunning)
                    {
                        var scanCount = ScanBag.Count;
                        Parallel.For(CurrentIndex, scanCount, (i) =>
                        //for (int i = currentIndex; i < scanCount; i++)
                        {
                            var scan = ScanBag[i];
                            try
                            {
                                using (var fileImageBase = new MagickImage((Bitmap)scan.CurrentFrame.Bitmap.Clone()))
                                {
                                    //fileImageBase.Extent(CropGeometry.ToMagick(), Gravity.Northwest, MagickColor.FromRgba(0, 0, 0, 0));
                                    fileImageBase.Crop(CropGeometry.ToMagick(), Gravity.Northwest);
                                    fileImageBase.RePage();
                                    foreach (var wz in GameProfile.Screens[0].WatchZones)
                                    {
                                        var mg = wz.ThumbnailGeometry.ToMagick();

                                        using (var fileImageCropped = (MagickImage)fileImageBase.Clone())
                                        {
                                            fileImageCropped.Extent(mg, wz.ThumbnailGeometry.Anchor.ToGravity(), MagickColor.FromRgba(0, 0, 0, 0));
                                            foreach (var w in wz.Watches)
                                            {
                                                var fileImageComposed = (MagickImage)fileImageCropped.Clone();
                                                fileImageComposed.ColorSpace = w.ColorSpace;
                                                if (w.Channel > -1)
                                                {
                                                    fileImageComposed = (MagickImage)fileImageComposed.Separate().ToArray()[w.Channel].Clone();
                                                }

                                                if (!w.DupeFrameCheck)
                                                {
                                                    foreach (var wi in w.WatchImages)
                                                    {
                                                        using (var deltaImage = wi.MagickImage.Clone())
                                                        using (var fileImageCompare = (MagickImage)fileImageComposed.Clone())
                                                        {
                                                            //if (deltaImage.HasAlpha)
                                                            //{
                                                            //    fileImageCompare.Composite(deltaImage, CompositeOperator.CopyAlpha);
                                                            //}
                                                            deltaImage.ColorSpace = w.ColorSpace;
                                                            var a = (float)deltaImage.Compare(fileImageCompare,
                                                                ErrorMetric.NormalizedCrossCorrelation);
                                                            Interlocked.Exchange(ref Program.floatArray[wi.Index], a);

                                                            /*
                                                            if (i % 300 == 0 && wi.index == 1)
                                                            {
                                                                fileImageCompare.Write(@"E:\test2.png");
                                                                deltaImage.Write(@"E:\test3.png");
                                                                fileImageComposed.Write(@"E:\test4.png");
                                                                fileImageCropped.Write(@"E:\test5.png");
                                                                fileImageBase.Write(@"E:\test6.png");
                                                            }*/

                                                            //int id = (int)Math.Round(thumbID * timeIndexMultiplier);
                                                            //wi.DeltaBag.Add(new Bag(id, delta));
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    // Put PreviousFrame clones in proper spots.
                                                    // it's just kind of unoptimial for non-dupe checkers
                                                    var deltaImage = new MagickImage((Bitmap)scan.PreviousFrame.Bitmap.Clone());
                                                    using (var fileImageCompare = (MagickImage)fileImageComposed.Clone())
                                                    {
                                                        deltaImage.Crop(CropGeometry.ToMagick(), Gravity.Northwest);
                                                        deltaImage.RePage();
                                                        deltaImage.Extent(mg, wz.ThumbnailGeometry.Anchor.ToGravity(), MagickColor.FromRgba(0, 0, 0, 0));
                                                        deltaImage.ColorSpace = w.ColorSpace;
                                                        if (w.Channel > -1)
                                                        {
                                                            deltaImage = (MagickImage)deltaImage.Separate().ToList()[w.Channel].Clone();
                                                        }
                                                        if (i % 300 == 0)
                                                        {
                                                            deltaImage.Write(@"E:\test9.png");
                                                        }

                                                        var a = (float)deltaImage.Compare(fileImageCompare,
                                                            ErrorMetric.PeakSignalToNoiseRatio);
                                                        Interlocked.Exchange(ref Program.floatArray[18], a);


                                                        if (i % 300 == 0)
                                                        {
                                                            fileImageCompare.Write(@"E:\test2.png");
                                                            deltaImage.Write(@"E:\test3.png");
                                                            fileImageComposed.Write(@"E:\test4.png");
                                                            fileImageCropped.Write(@"E:\test5.png");
                                                            fileImageBase.Write(@"E:\test6.png");
                                                        }

                                                        //int id = (int)Math.Round(thumbID * timeIndexMultiplier);
                                                        //wi.DeltaBag.Add(new Bag(id, delta));
                                                    }
                                                    deltaImage.Dispose();
                                                }
                                                fileImageComposed.Dispose();
                                            }
                                        }
                                    }

                                    //if (i % 300 == 0)
                                    //{
                                    //    fileImageBase.Write(@"E:\test.png");
                                    //}

                                }
                            }
                            catch (Exception) { }
                            scan.Clean();
                        //}
                        });
                        CurrentIndex = scanCount;
                        // So that it doesn't hog a whole core when waiting for more frames.
                        Thread.Sleep(1);
                    }
                }
                Thread.Sleep(500);
            }
        }

    }
}
