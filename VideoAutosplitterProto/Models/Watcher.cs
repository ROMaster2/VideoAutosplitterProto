using ImageMagick;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace VideoAutosplitterProto.Models
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

        public double Frequency = 1d;

        public ColorSpace ColorSpace = ColorSpace.RGB;
        public int Channel = -1;
        public bool Equalize = true;

        public ErrorMetric ErrorMetric = ErrorMetric.NormalizedCrossCorrelation;

        public bool DupeFrameCheck = false;
        //public CompositeOperator Compositer; // Todo

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
