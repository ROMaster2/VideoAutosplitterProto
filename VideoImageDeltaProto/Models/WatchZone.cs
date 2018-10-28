using ImageMagick;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace VideoImageDeltaProto.Models
{
    public class WatchZone : IGeometry
    {
        internal WatchZone(Screen screen, string name, ScaleType scaleType, Geometry geometry)
        {
            Parent = screen;
            Name = name;
            ScaleType = scaleType;
            Geometry = geometry;
        }

        internal WatchZone() { }

        public ScaleType ScaleType { get; set; }
        public List<Watcher> Watches { get; set; } = new List<Watcher>();

        [XmlIgnore]
        public Screen Screen { get { return (Screen)Parent; } }
        [XmlIgnore]
        public List<WatchImage> WatchImages
        { get { var a = new List<WatchImage>(); a.AddRange(Watches.SelectMany(w => w.WatchImages)); return a; } }


        public Watcher AddWatcher(string name, double frequency = 1d, ColorSpace colorSpace = ColorSpace.RGB)
        {
            var watcher = new Watcher(this, name, frequency, colorSpace);
            Watches.Add(watcher);
            return watcher;
        }

        public void ReSyncRelationships()
        {
            if (Watches.Count > 0)
            {
                foreach (var w in Watches)
                {
                    w.Parent = this;
                    w.ReSyncRelationships();
                }
            }
        }

        public Geometry WithoutScale(Geometry screenGeo, Geometry feedGeo, Geometry gameGeo)
        {
            Geometry GameGeometry = screenGeo;
            if (gameGeo.HasSize)
            {
                GameGeometry = gameGeo;
            }

            double xScale = feedGeo.Width / GameGeometry.Width;
            double yScale = feedGeo.Height / GameGeometry.Height;

            double _x = Geometry.X;
            double _y = Geometry.Y;
            double _width = Geometry.Width;
            double _height = Geometry.Height;

            if (ScaleType == ScaleType.KeepRatio)
            {
                double scale = Math.Min(xScale, yScale); // Min or Max?
                xScale = scale;
                yScale = scale;
            }
            if (ScaleType != ScaleType.NoScale)
            {
                _x *= xScale;
                _y *= yScale;
                _width *= xScale;
                _height *= yScale;
            }

            return new Geometry(_x, _y, _width, _height, Geometry.Anchor)/*.WithoutAnchor(GameGeometry)*/;
        }

        override public string ToString()
        {
            return Name;
        }

    }

    public enum ScaleType
    {
        Undefined = 0,
        NoScale = 1,
        KeepRatio = 2,
        Scale = 3,
    }

}
