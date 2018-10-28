using ImageMagick;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Xml.Serialization;

namespace VideoImageDeltaProto.Models
{
    public class WatchImage : IGeometry
    {
        internal WatchImage(Watcher watcher, string filePath)
        {
            Parent = watcher;
            FilePath = filePath;
        }

        // Todo: Add exception handling when file does not exist anymore.

        internal WatchImage() { }

        public string FilePath { get; set; }
        [XmlIgnore]
        public MagickImage MagickImage { get; internal set; }
        private Image image;
        public Image Image
        {
            get
            {
                if (image == null)
                {
                    image = Image.FromFile(@FilePath);
                }
                return image;
            }
        }

        [XmlIgnore]
        public Screen Screen { get { return WatchZone.Screen; } }
        [XmlIgnore]
        public WatchZone WatchZone { get { return Watcher.WatchZone; } }
        [XmlIgnore]
        public Watcher Watcher { get { return (Watcher)Parent; } }

        public int Index;

        public string FileName
        {
            get
            {
                var i = FilePath.LastIndexOf('\\');
                return FilePath.Substring(i + 1, FilePath.Length - i - 1);
            }
        }

        public void SetName(Screen screen, WatchZone watchZone, Watcher watcher)
        {
            Name = screen.Name + "/" + watchZone.Name + "/" + watcher.Name + " - " + FileName;
        }

        public void SetMagickImage(bool extremePrecision)
        {
            var mi = new MagickImage((Bitmap)Image);
            mi.ColorSpace = Watcher.ColorSpace;
            if (!extremePrecision)
            {
                var mGeo = new MagickGeometry(
                    (int)Math.Round(WatchZone.ThumbnailGeometry.Width),
                    (int)Math.Round(WatchZone.ThumbnailGeometry.Height))
                {
                    IgnoreAspectRatio = true
                };
                mi.Scale(mGeo);
                mi.RePage();
            }
            else
            {
                var underlay = new MagickImage(MagickColor.FromRgba(0, 0, 0, 0), (int)Screen.Geometry.Width, (int)Screen.Geometry.Height);
                underlay.Composite(mi, new PointD(WatchZone.Geometry.Width, WatchZone.Geometry.Height), CompositeOperator.Copy);
                underlay.Write(@"E:\lmao0.png");
                underlay.RePage();
                var mGeo = Screen.ThumbnailGeometry.ToMagick();
                mGeo.IgnoreAspectRatio = true;
                underlay.Resize(mGeo);
                underlay.Write(@"E:\lmao1.png");
                underlay.RePage();
                underlay.Trim();
                underlay.Write(@"E:\lmao2.png");
                mi = underlay;
            }
            MagickImage = mi;
            Clear();
        }

        public void Clear()
        {
            image = null;
        }

        [XmlIgnore]
        public ConcurrentBag<Bag> DeltaBag = new ConcurrentBag<Bag>();

        public string DeltaBagCompact
        {
            get
            {
                var lb = new List<byte>();
                foreach (var b in DeltaBag)
                {
                    lb.AddRange(BitConverter.GetBytes(b.FrameIndex));
                    lb.AddRange(Utilities.FloatToBytes(b.Confidence));
                }
                return Convert.ToBase64String(lb.ToArray());
            }
            set
            {
                var lb = Convert.FromBase64String(value);
                /*if (lb.Count() % 6 != 0)
                {
                    throw new Exception("Incorrect number of bytes. Conversion not possible.");
                }*/
                DeltaBag = new ConcurrentBag<Bag>();
                for (int i = 0; i < lb.Count(); i += 6)
                {
                    var frameIndex = BitConverter.ToInt32(lb, i);
                    var confidence = Utilities.BytesToFloat(lb[i + 4], lb[i + 5]);
                    DeltaBag.Add(new Bag(frameIndex, confidence));
                }
            }
        }

        [XmlIgnore]
        public List<Bag> DeltaList
        {
            get
            {
                return DeltaBag.ToList();
            }
        }

        override public string ToString()
        {
            if (!string.IsNullOrWhiteSpace(Name))
                return Name;
            else
                return FileName;
        }

    }

}
