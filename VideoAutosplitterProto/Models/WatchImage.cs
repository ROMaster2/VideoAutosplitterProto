using ImageMagick;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Xml.Serialization;

namespace VideoAutosplitterProto.Models
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

        private Image _Image;
        public Image Image
        {
            get
            {
                if (_Image == null)
                {
                    if (File.Exists(FilePath))
                    {
                        _Image = Image.FromFile(FilePath);
                    }
                    else
                    {
                        throw new FileNotFoundException("Image not located at " + FilePath);
                    }
                }
                return _Image;
            }
        }

        [XmlIgnore]
        public MagickImage MagickImage { get; internal set; }

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
            var mi = new MagickImage((Bitmap)Image)
            {
                ColorSpace = Watcher.ColorSpace
            };
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
                underlay.RePage();
                var mGeo = Screen.ThumbnailGeometry.ToMagick();
                mGeo.IgnoreAspectRatio = true;
                underlay.Resize(mGeo);
                underlay.RePage();
                underlay.Trim();
                mi = underlay;
            }
            if (Watcher.Equalize)
            {
                mi.Equalize();
            }
            MagickImage = mi;
            Clear();
        }

        public void Clear()
        {
            _Image = null;
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
