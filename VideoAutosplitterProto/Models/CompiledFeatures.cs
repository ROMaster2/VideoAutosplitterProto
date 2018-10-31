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
        public static void Compile(GameProfile gameProfile)
        {
            if (CWatchZones != null)
            {
                foreach (var cWatchZone in CWatchZones)
                {
                    cWatchZone.Dispose();
                }
                CWatchZones.Clear();
            }

            var cWatchZones = new CWatchZone[gameProfile.WatchZones.Count];
            var indexCount = 0;

            for (int i1 = 0; i1 < gameProfile.WatchZones.Count; i1++)
            {
                WatchZone watchZone = gameProfile.WatchZones[i1];
                Screen screen = watchZone.Screen;

                var CWatches = new CWatcher[watchZone.Watches.Count];

                // If it's a game with user definable resolution, use what they've set.
                // Otherwise, assume the base resolution.
                var gameGeo = screen.GameGeometry.HasSize ? screen.GameGeometry : screen.Geometry;
                // Redefine parent geometry and adjust size and offset accordingly
                var wzCropGeo = watchZone.WithoutScale(gameGeo);
                // Relative position known, anchor no longer needed.
                wzCropGeo.RemoveAnchor(gameGeo);
                // Change size to fit the geometry on the video capture
                wzCropGeo.ResizeTo(screen.CropGeometry, gameGeo);
                // Add the video capture's offset
                wzCropGeo.Update(screen.CropGeometry.X, screen.CropGeometry.Y);

                for (int i2 = 0; i2 < watchZone.Watches.Count; i2++)
                {
                    Watcher watcher = watchZone.Watches[i2];
                    var CWatchImages = new CWatchImage[watcher.WatchImages.Count];

                    for (int i3 = 0; i3 < watcher.WatchImages.Count; i3++)
                    {
                        WatchImage watchImage = watcher.WatchImages[i3];
                        if (watcher.WatcherType == WatcherType.Standard)
                        {
                            var mi = new MagickImage((Bitmap)watchImage.Image.Clone())
                            {
                                ColorSpace = watcher.ColorSpace
                            };
                            if (!Scanner.ExtremePrecision)
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
                        } else if (watcher.WatcherType == WatcherType.DuplicateFrame)
                        {
                            CWatchImages[i3] = new CWatchImage(watchImage.Name, indexCount);
                        } else if (watcher.WatcherType == WatcherType.BestMatch)
                        {

                        }
                        indexCount++;
                    }

                    CWatches[i2] = new CWatcher(CWatchImages, watcher);
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

        public static ICollection<CWatchZone> CWatchZones { get; internal set; }
    }

    public struct CWatchZone
    {
        public CWatchZone(string name, Geometry geometry, ICollection<CWatcher> cWatchers)
        {
            Name = name;
            TrueGeometry = geometry;
            MagickGeometry = geometry.ToMagick();
            CWatchers = cWatchers;
        }
        public string Name;
        public Geometry TrueGeometry;
        public MagickGeometry MagickGeometry;
        public ICollection<CWatcher> CWatchers;
        public void Dispose()
        {
            foreach (var x in CWatchers)
            {
                x.Dispose();
            }
            CWatchers.Clear();
        }
    }

    public struct CWatcher
    {
        public CWatcher(
            ICollection<CWatchImage> cWatchImages,
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
        }
        public CWatcher(ICollection<CWatchImage> cWatchImages, Watcher watcher)
        {
            Name = watcher.Name;
            WatcherType = watcher.WatcherType;
            ColorSpace = watcher.ColorSpace;
            Channel = watcher.Channel;
            Equalize = watcher.Equalize;
            ErrorMetric = watcher.ErrorMetric;
            CWatchImages = cWatchImages;
        }
        public string Name;
        public WatcherType WatcherType;
        public ColorSpace ColorSpace;
        public int Channel;
        public bool Equalize;
        public ErrorMetric ErrorMetric;
        public ICollection<CWatchImage> CWatchImages;
        public void Dispose()
        {
            foreach (var x in CWatchImages)
            {
                x.Dispose();
            }
            CWatchImages.Clear();
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
        }
        public CWatchImage(string name, int index, IMagickImage magickImage)
        {
            Name = name;
            Index = index;
            MagickImage = magickImage;
            MagickImageCollection = null;
        }
        public CWatchImage(string name, int index, IMagickImageCollection magickImageCollection)
        {
            Name = name;
            Index = index;
            MagickImage = null;
            MagickImageCollection = magickImageCollection;
        }
        public string Name;
        public int Index;
        public IMagickImage MagickImage;
        public IMagickImageCollection MagickImageCollection;
        public void Dispose()
        {
            if (MagickImage != null) MagickImage.Dispose();
            if (MagickImageCollection != null) MagickImageCollection.Dispose();
        }
    }
}
