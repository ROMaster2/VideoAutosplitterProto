using ImageMagick;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace VideoImageDeltaProto.Models
{
    public class Watcher : IGeometry
    {
        internal Watcher(WatchZone watchZone, string name, double frequency = 1d, ColorSpace colorSpace = ColorSpace.RGB)
        {
            Parent = watchZone;
            Name = name;
            ColorSpace = colorSpace;
            Frequency = frequency;
        }

        internal Watcher() { }

        public double Frequency;
        public ColorSpace ColorSpace = ColorSpace.RGB;
        public sbyte Channel = -1;
        //public CompositeOperator Compositer; // Why did I want this added?
        public bool DupeFrameCheck = false;
        public List<WatchImage> WatchImages = new List<WatchImage>();

        [XmlIgnore]
        public Screen Screen { get { return WatchZone.Screen; } }
        [XmlIgnore]
        public WatchZone WatchZone { get { return (WatchZone)Parent; } }


        public WatchImage AddWatchImage(string filePath)
        {
            var watchImage = new WatchImage(this, filePath);
            WatchImages.Add(watchImage);
            return watchImage;
        }

        public void ReSyncRelationships()
        {
            if (WatchImages.Count > 0)
            {
                foreach (var wi in WatchImages)
                {
                    wi.Parent = this;
                }
            }
        }

        override public string ToString()
        {
            return Name;
        }

    }

}
