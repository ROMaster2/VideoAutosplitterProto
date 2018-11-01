using ImageMagick;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoAutosplitterProto.Models
{
    public static class CompiledFeatures
    {
        public static CWatchZone[] CWatchZones { get; internal set; }
        public static bool HasDupeCheck { get; internal set; }
        public static bool UseExtremePrecision { get; internal set; }

        public static void Compile(GameProfile gameProfile, bool useExtremePrecision)
        {
            if (CWatchZones != null)
            {
                foreach (var cWatchZone in CWatchZones)
                {
                    cWatchZone.Dispose();
                }
                Array.Clear(CWatchZones, 0, CWatchZones.Length);
            }
            HasDupeCheck = false;
            UseExtremePrecision = useExtremePrecision;

            var cWatchZones = new CWatchZone[gameProfile.WatchZones.Count];
            var indexCount = 0;

            for (int i1 = 0; i1 < gameProfile.WatchZones.Count; i1++)
            {
                WatchZone watchZone = gameProfile.WatchZones[i1];
                Screen screen = watchZone.Screen;

                var CWatches = new CWatcher[watchZone.Watches.Count];

                var gameGeo = screen.GameGeometry.HasSize ? screen.GameGeometry : screen.Geometry;
                var wzCropGeo = watchZone.WithoutScale(gameGeo);
                wzCropGeo.RemoveAnchor(gameGeo);
                wzCropGeo.ResizeTo(screen.CropGeometry, gameGeo);
                wzCropGeo.Update(screen.CropGeometry.X, screen.CropGeometry.Y);

                for (int i2 = 0; i2 < watchZone.Watches.Count; i2++)
                {
                    Watcher watcher = watchZone.Watches[i2];

                    if (watcher.WatcherType == WatcherType.Standard)
                    {
                        var CWatchImages = new CWatchImage[watcher.WatchImages.Count];

                        for (int i3 = 0; i3 < watcher.WatchImages.Count; i3++)
                        {
                            WatchImage watchImage = watcher.WatchImages[i3];
                            var mi = new MagickImage(watchImage.Image)
                            {
                                ColorSpace = watcher.ColorSpace
                            };
                            GetComposedImage(ref mi, watcher.Channel);
                            if (!UseExtremePrecision)
                            {
                                StandardResize(ref mi, wzCropGeo.ToMagick(false));
                            }
                            else
                            {
                                PreciseResize(ref mi, watchZone.Geometry, gameGeo);
                            }
                            if (watcher.Equalize)
                            {
                                mi.Equalize();
                            }

                            CWatchImages[i3] = new CWatchImage(watchImage.Name, indexCount, mi);
                            indexCount++;
                        }

                        CWatches[i2] = new CWatcher(CWatchImages, watcher);
                    }
                    else if (watcher.WatcherType == WatcherType.BestMatch)
                    {
                        var mic = new MagickImageCollection();
                        for (int i3 = 0; i3 < watcher.WatchImages.Count; i3++)
                        {
                            WatchImage watchImage = watcher.WatchImages[i3];
                            var mi = new MagickImage(watchImage.FilePath)
                            {
                                ColorSpace = watcher.ColorSpace
                            };
                            GetComposedImage(ref mi, watcher.Channel);
                            if (!UseExtremePrecision)
                            {
                                StandardResize(ref mi, wzCropGeo.ToMagick(false));
                            }
                            else
                            {
                                PreciseResize(ref mi, watchZone.Geometry, gameGeo);
                            }
                            if (watcher.Equalize)
                            {
                                mi.Equalize();
                            }

                            //CWatchImages[i3] = new CWatchImage(watchImage.Name, indexCount, mi);
                            indexCount++;
                        }


                    }
                    else if (watcher.WatcherType == WatcherType.DuplicateFrame)
                    {
                        HasDupeCheck = true;
                    }
                }

                cWatchZones[i1] = new CWatchZone(watchZone.Name, wzCropGeo, CWatches);
            }

            CWatchZones = cWatchZones;
        }

        public static void StandardResize(ref MagickImage mi, MagickGeometry geo)
        {
            geo.IgnoreAspectRatio = true;
            mi.Scale(geo);
            mi.RePage();
        }

        // Todo: Test this more
        public static void PreciseResize(ref MagickImage mi, Geometry refGeo, Geometry gameGeo)
        {
            var underlay = new MagickImage(MagickColors.Transparent, (int)gameGeo.Width, (int)gameGeo.Height);
            var point = refGeo.LocationWithoutAnchor(gameGeo);
            underlay.Composite(mi, new PointD(point.X, point.Y), CompositeOperator.Copy);
            underlay.RePage();
            var mGeo = Scanner.CropGeometry.ToMagick(false);
            mGeo.IgnoreAspectRatio = true;
            underlay.Resize(mGeo);
            underlay.RePage();
            underlay.Trim();
            underlay.RePage();
            mi = underlay;
        }

        // May need to update to support multiple channels.
        // Todo: Put this elsewhere. It's copied from Scanner.cs...
        public static void GetComposedImage(ref MagickImage mi, int channelIndex)
        {
            if (channelIndex > -1)
            {
                var mic = mi.Separate();
                mi.Dispose();
                int i = 0;
                foreach (var x in mic)
                {
                    if (i == channelIndex)
                    {
                        mi = (MagickImage)x;
                    }
                    else
                    {
                        x.Dispose();
                    }
                    i++;
                }
            }
        }

    }

    public struct CWatchZone
    {
        public CWatchZone(string name, Geometry geometry, CWatcher[] cWatches)
        {
            Name = name;
            TrueGeometry = geometry;
            MagickGeometry = geometry.ToMagick();
            CWatches = cWatches;
        }
        public string Name;
        public Geometry TrueGeometry;
        public MagickGeometry MagickGeometry;
        public CWatcher[] CWatches;
        public void Dispose()
        {
            foreach (var x in CWatches)
            {
                x.Dispose();
            }
            Array.Clear(CWatches, 0, CWatches.Length);
        }
    }

    public struct CWatcher
    {
        public CWatcher(
            CWatchImage[] cWatchImages,
            string name,
            WatcherType watcherType,
            ColorSpace colorSpace,
            int channel,
            bool equalize,
            ErrorMetric errorMetric
            )
        {
            Name = name;
            WatcherType = watcherType;
            ColorSpace = colorSpace;
            Channel = channel;
            Equalize = equalize;
            ErrorMetric = errorMetric;
            CWatchImages = cWatchImages;

            IsStandardCheck = WatcherType.Equals(WatcherType.Standard);
            IsDuplicateFrameCheck = WatcherType.Equals(WatcherType.DuplicateFrame);
            IsBestMatchCheck = WatcherType.Equals(WatcherType.BestMatch);
        }
        public CWatcher(CWatchImage[] cWatchImages, Watcher watcher)
        {
            Name = watcher.Name;
            WatcherType = watcher.WatcherType;
            ColorSpace = watcher.ColorSpace;
            Channel = watcher.Channel;
            Equalize = watcher.Equalize;
            ErrorMetric = watcher.ErrorMetric;
            CWatchImages = cWatchImages;

            IsStandardCheck = WatcherType.Equals(WatcherType.Standard);
            IsDuplicateFrameCheck = WatcherType.Equals(WatcherType.DuplicateFrame);
            IsBestMatchCheck = WatcherType.Equals(WatcherType.BestMatch);
        }

        public string Name;
        public WatcherType WatcherType;
        public ColorSpace ColorSpace;
        public int Channel;
        public bool Equalize;
        public ErrorMetric ErrorMetric;
        public CWatchImage[] CWatchImages;

        public bool IsStandardCheck;
        public bool IsDuplicateFrameCheck;
        public bool IsBestMatchCheck;

        public void Dispose()
        {
            foreach (var x in CWatchImages)
            {
                x.Dispose();
            }
            Array.Clear(CWatchImages, 0, CWatchImages.Length);
        }
    }

    public struct CWatchImage
    {
        public CWatchImage(string name, int index)
        {
            Name = name;
            Index = index;
            MagickImage = null;
            MagickImageCollection = null;
            HasAlpha = false;
        }
        public CWatchImage(string name, int index, IMagickImage magickImage)
        {
            Name = name;
            Index = index;
            MagickImage = magickImage;
            MagickImageCollection = null;
            HasAlpha = MagickImage.HasAlpha;
        }
        public CWatchImage(string name, int index, IMagickImageCollection magickImageCollection)
        {
            Name = name;
            Index = index;
            MagickImage = null;
            MagickImageCollection = magickImageCollection;
            HasAlpha = false;
        }
        public string Name;
        public int Index;
        public IMagickImage MagickImage;
        public bool HasAlpha;
        public IMagickImageCollection MagickImageCollection;
        public void Dispose()
        {
            if (MagickImage != null) MagickImage.Dispose();
            if (MagickImageCollection != null) MagickImageCollection.Dispose();
        }
    }
}
