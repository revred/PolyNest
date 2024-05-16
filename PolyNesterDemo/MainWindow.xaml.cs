﻿using System.Diagnostics;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using PolyNester;
using System.ComponentModel;

namespace PolyNesterDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static string NEST = "Nest";
        private static string STOP = "Stop";

        private int[] _handles;
        private PolyNester.Nester _nester;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_play_Click(object sender, RoutedEventArgs e)
        {
            // if the nester is working cancel the work
            if (_nester != null && _nester.IsBusy())
            {
                _nester.CancelExecute();
                Debug.WriteLine("cancelling...");
                return;
            }

            // get the path of the .obj
            string file_path = string.Empty;

            OpenFileDialog open_file_dialog = new OpenFileDialog();
            open_file_dialog.Filter = "Object files (*.obj; *.OBJ)|*.obj;*.OBJ";
            if (open_file_dialog.ShowDialog() == true)
                file_path = open_file_dialog.FileName;

            // if no path then return
            if (string.IsNullOrEmpty(file_path))
                return;

            // try to get the model data
            UVObjData data = null;
            try
            {
                data = ObjHandler.GetData(file_path);
            }
            catch { return; }

            // if data is null return
            if (data == null)
                return;

            // get the uv verts and triangles
            Vector64[] pts = data.uvs.Select(p => new Vector64(p.X, p.Y)).ToArray();
            int[] tris = data.tris;

            // get the canvas container
            Rect64 container = new Rect64(0, canvas_main.Height, canvas_main.Width, 0);

            // create a new nester object and populate its data
            _nester = new PolyNester.Nester();
            _handles = _nester.AddUVPolygons(pts, tris, 0.0);
            
            // set the nesting commands
            _nester.ClearCommandBuffer();
            _nester.ResetTransformLib();

            canvas_main.Children.Clear();

            _nester.OddOptimalRotation(null);

            _nester.OddCmdNest(null, NestQuality.Full);

            _nester.OddCmdRefit(container, false, null);

            // change the button text and execute work
            button_play.Content = STOP;

            _nester.ExecuteCmdBuffer(NesterProgress, NesterComplete);
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

            for (int i = 0; i < _nester.PolySpace; i++)
                canvas_main.AddNgon(_nester.GetTransformedPoly(i), WPFHelper.RandomColor());
        }
    }
}
