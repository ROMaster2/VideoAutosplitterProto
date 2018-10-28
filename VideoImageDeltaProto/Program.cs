using Accord.Video;
using Accord.Video.DirectShow;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using VideoImageDeltaProto.Forms;
using VideoImageDeltaProto.Models;

namespace VideoImageDeltaProto
{
    static class Program
    {
        public static readonly long a = 123456789;
        public static bool[] boolArray = new bool[20];
        public static float[] floatArray = new float[20];
        public static MainWindow MainWindow;

        [STAThread]
        static void Main()
        {
            floatArray[19] = 456.789F;

            //Test3.Run();
            //Test3.Math1();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            MainWindow = new MainWindow();
            //Task.Run(() => Scanner.RunOld());
            Application.Run(MainWindow);
        }
    }
}
