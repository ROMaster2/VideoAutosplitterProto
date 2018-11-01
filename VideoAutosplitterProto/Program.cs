using System;
using System.Windows.Forms;
using VideoAutosplitterProto.Forms;
using VideoAutosplitterProto.Models;

namespace VideoAutosplitterProto
{
    static class Program
    {
        public static float[] floatArray = new float[32];
        public static long timeDelta = 0;
        public static long count = 0;

        [STAThread]
        static void Main()
        {
            floatArray[19] = 456.789F; // For signature scanning for now.

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainWindow());
        }
    }
}
