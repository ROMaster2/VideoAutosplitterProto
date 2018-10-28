using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace VideoImageDeltaProto.Models
{
    public class GameProfile : IGeometry
    {
        public GameProfile(string name)
        {
            Name = name;
        }

        internal GameProfile() { }

        public List<Screen> Screens { get; set; } = new List<Screen>();

        [XmlIgnore]
        public List<WatchZone> WatchZones
        { get { var a = new List<WatchZone>();  a.AddRange(Screens.SelectMany(s => s.WatchZones));   return a; } }
        [XmlIgnore]
        public List<Watcher> Watches
        { get { var a = new List<Watcher>();    a.AddRange(WatchZones.SelectMany(wz => wz.Watches)); return a; } }
        [XmlIgnore]
        public List<WatchImage> WatchImages
        { get { var a = new List<WatchImage>(); a.AddRange(Watches.SelectMany(w => w.WatchImages));  return a; } }

        public Screen AddScreen(string name, bool useAdvanced, Geometry geometry)
        {
            var screen = new Screen(this, name, useAdvanced, geometry);
            Screens.Add(screen);
            return screen;
        }

        public void ReSyncRelationships()
        {
            if (Screens.Count > 0) {
                foreach (var s in Screens)
                {
                    s.Parent = this;
                    s.ReSyncRelationships();
                }
            }
        }

        override public string ToString()
        {
            return Name;
        }

        public static GameProfile FromXml(string filePath)
        {
            using (StreamReader reader = new StreamReader(filePath))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(GameProfile));
                GameProfile gp = null;

                try
                {
                    gp = (GameProfile)serializer.Deserialize(reader);
                    gp.ReSyncRelationships();
                }
                catch (Exception) { return null; }

                return gp;
            }
        }

    }

}
