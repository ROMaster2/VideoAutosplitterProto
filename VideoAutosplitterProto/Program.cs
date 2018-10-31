using System;
using System.Windows.Forms;
using VideoAutosplitterProto.Forms;
using VideoAutosplitterProto.Models;

namespace VideoAutosplitterProto
{
    static class Program
    {
        public static float[] floatArray = new float[32];
        public static int timeDelta = 0;

        [STAThread]
        static void Main()
        {
            floatArray[19] = 456.789F; // For signature scanning for now.

            /*Scanner.CropGeometry = new Geometry(
                Properties.Settings.Default.CropX,
                Properties.Settings.Default.CropY,
                Properties.Settings.Default.CropWidth,
                Properties.Settings.Default.CropHeight);

            Test5.Run();*/

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainWindow());
        }
    }
}
