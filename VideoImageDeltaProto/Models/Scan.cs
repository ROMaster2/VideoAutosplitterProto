using System;
using System.Drawing;

namespace VideoImageDeltaProto
{
    public struct Scan
    {
        public Frame CurrentFrame;
        public Frame PreviousFrame;
        public Scan(Frame currentFrame, Frame previousFrame)
        {
            CurrentFrame = currentFrame;
            PreviousFrame = previousFrame;
        }

        public static readonly Scan Blank = new Scan(Frame.Blank, Frame.Blank);

        public void Update(Frame newFrame)
        {
            PreviousFrame.Bitmap.Dispose();
            PreviousFrame = CurrentFrame;
            CurrentFrame = newFrame;
        }
        public void Clean()
        {
            CurrentFrame.Bitmap.Dispose();
            PreviousFrame.Bitmap.Dispose();
        }
        public bool IsCleaned()
        {
            try
            {
                var x = PreviousFrame.Bitmap.Height;
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }

    public struct Frame // Bad name idea?
    {
        public DateTime DateTime;
        public Bitmap Bitmap;

        public Frame(DateTime dateTime, Bitmap bitmap)
        {
            DateTime = dateTime;
            Bitmap = bitmap;
        }

        public static readonly Frame Blank = new Frame(new DateTime(0), new Bitmap(1, 1));

        public Frame Clone()
        {
            return new Frame(DateTime, (Bitmap)Bitmap.Clone());
        }
    }
}
