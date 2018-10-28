using System.Xml.Serialization;

namespace VideoImageDeltaProto.Models
{
    public struct Bag
    {
        public Bag(int frameIndex, float delta, string timeStamp = null)
        {
            FrameIndex = frameIndex;
            Confidence = delta;
            TimeStamp = timeStamp;
        }

        [XmlAttribute("I")]
        public int FrameIndex;
        [XmlAttribute("C")]
        public float Confidence;
        [XmlAttribute("T")]
        public string TimeStamp;
    }

}
