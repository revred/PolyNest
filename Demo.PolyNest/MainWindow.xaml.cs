using Microsoft.Win32;
using PolyNester;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;

namespace PolyNesterDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static string NEST = "Nest";
        private static string STOP = "Stop";

        private int[] handles_ = default!;
        private Nester nester_ = default!;

        public MainWindow()
        {
            InitializeComponent();
        }

        private UVObjInfo? FetchUVobjInfo()
        {
            string file_path = string.Empty;

            OpenFileDialog open_file_dialog = new OpenFileDialog();
            open_file_dialog.Filter = "Object files (*.obj; *.OBJ)|*.obj;*.OBJ";
            if (open_file_dialog.ShowDialog() == true)
                file_path = open_file_dialog.FileName;

            // if no path then return
            if (string.IsNullOrEmpty(file_path))
                return null;

            // try to get the model data
            UVObjInfo? info = null;
            try
            {
                info = ObjHandler.GetData(file_path);
            }
            catch { info = null; }
            return info;
        }

        private void Button_play_Click(object sender, RoutedEventArgs e)
        {
            // if the nester is working cancel the work
            if (nester_ != null && nester_.IsBusy())
            {
                nester_.CancelExecute();
                Debug.WriteLine("cancelling...");
                return;
            }

            // get the path of the .obj
            var info = FetchUVobjInfo();
            // if info is null return
            if (info == null) return;

            // get the uv verts and triangles
            Vector64[] pts = info.uvs.Select(p => new Vector64(p.X, p.Y)).ToArray();
            int[] tris = info.tris;

            // get the canvas container
            Rect64 container = new Rect64(0, canvas_main.Height, canvas_main.Width, 0);

            // create a new nester object and populate its data
            nester_ = new PolyNester.Nester();
            handles_ = nester_.AddUVPolygons(pts, tris, 0.0);
            
            // set the nesting commands
            nester_.ClearCommandBuffer();
            nester_.ResetTransformLib();

            canvas_main.Children.Clear();

            nester_.OddOptimalRotation(null);

            nester_.OddCmdNest(null, NestQuality.Full);

            nester_.OddCmdRefit(container, false, null);

            // change the button text and execute work
            button_play.Content = STOP;

            nester_.ExecuteCmdBuffer(NesterProgress, NesterComplete);
        }

        void NesterProgress(ProgressChangedEventArgs e)
        {
            Debug.WriteLine(e.ProgressPercentage);
        }

        void NesterComplete(AsyncCompletedEventArgs e)
        {
            button_play.Content = NEST;

            if (e.Cancelled)
            {
                Debug.WriteLine("cancelled");
                return;
            }

            for (int i = 0; i < nester_.PolySpace; i++)
                canvas_main.AddNgon(nester_.GetTransformedPoly(i), WPFHelper.RandomColor());
        }
    }
}
