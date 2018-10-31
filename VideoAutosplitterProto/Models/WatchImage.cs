﻿using ImageMagick;
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
    public class WatchImage
    {
        internal WatchImage(Watcher watcher, string filePath)
        {
            Watcher = watcher;
            FilePath = filePath;
        }

        // Todo: Add exception handling when file does not exist anymore.

        internal WatchImage() { }

        public string FilePath;

        private string _Name;
        public string Name
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_Name))
                    return _Name;
                else
                    return FileName;
            }
            set
            {
                _Name = value;
            }
        }

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
        public Watcher Watcher { get; internal set; }

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
            _Name = screen.Name + "/" + watchZone.Name + "/" + watcher.Name + " - " + FileName;
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
                    (int)Math.Round(WatchZone.CropGeometry.Width),
                    (int)Math.Round(WatchZone.CropGeometry.Height))
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
                var mGeo = Scanner.CropGeometry.ToMagick(false);
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
