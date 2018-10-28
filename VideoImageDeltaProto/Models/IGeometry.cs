using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace VideoImageDeltaProto.Models
{
    public abstract class IGeometry
    {
        public string Name;
        public Geometry Geometry;
        public Geometry ThumbnailGeometry;

        [XmlIgnore]
        public IGeometry Parent;
    }
}
